using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class HayDayCameraController : MonoBehaviour
{
    [Header("Fixed Pitch (locked - not player controllable)")]
    [Tooltip("Vertical tilt in degrees, fixed for the whole session. 45-55 is the Hay Day-style range.")]
    [SerializeField] private float pitch = 52f;
    [SerializeField] private float fieldOfView = 30f;

    [Header("Focus Point")]
    [Tooltip("Assign directly, or leave empty and the script finds a GameObject named 'Cityfocus' at startup. Falls back to the auto-detected city center if neither exists.")]
    [SerializeField] private Transform cityFocus;

    [Header("Starting Focus (one-shot - only affects the very first frame)")]
    [Tooltip("What the camera starts focused on. Takes priority over Focus Point above, but only for the initial view - after launch the player pans/rotates/zooms freely and this is never consulted again.")]
    [SerializeField] private Transform startFocusOverride;
    [Tooltip("Used only if Start Focus Override above is empty: a GameObject found by this exact name at startup.")]
    [SerializeField] private string startFocusObjectName = "Meshy_AI_Temple_Bank_0711203759_texture";

    [Header("City Bounds & Safe Padding")]
    [Tooltip("When enabled, bounds are calculated from every Renderer in the scene at startup, so no manual setup is required.")]
    [SerializeField] private bool autoDetectBounds = true;
    [SerializeField] private Bounds manualBounds = new Bounds(Vector3.zero, new Vector3(80f, 40f, 80f));
    [Tooltip("Inward safety margin (world units) kept between the visible ground edge and the real city edge, at every zoom level and rotation angle.")]
    [SerializeField] private float edgePadding = 3f;

    [Header("Zoom")]
    [Tooltip("Leave at -1 to derive automatically. If set, only ever tightens the auto-computed safe limit, never loosens it.")]
    [SerializeField] private float minZoomDistanceOverride = -1f;
    [SerializeField] private float maxZoomDistanceOverride = -1f;
    [Tooltip("Fraction of the city's footprint the max zoom-out is allowed to fill, centered on the city - keeps the zoomed-out view from being mostly road/empty fringe.")]
    [SerializeField, Range(0.3f, 1f)] private float maxZoomCoverageFraction = 0.6f;
    [Tooltip("Vertical clearance kept above the tallest detected building at minimum zoom.")]
    [SerializeField] private float heightSafetyMargin = 4f;
    [Tooltip("Fraction of the zoom range covered by one mouse-wheel notch.")]
    [SerializeField] private float mouseZoomSpeed = 0.08f;
    [Tooltip("Fraction of the zoom range covered by a pinch spanning the full screen.")]
    [SerializeField] private float touchZoomSpeed = 1f;
    [SerializeField] private float zoomSmoothTime = 0.18f;

    [Header("Starting View")]
    [SerializeField] private float startYaw = 42f;
    [Tooltip("0 = start close, zoomed in. 1 = start fully zoomed out.")]
    [SerializeField, Range(0f, 1f)] private float startingZoomNormalized = 0.35f;

    [Header("Pan")]
    [SerializeField] private float panSmoothTime = 0.12f;

    [Header("Rotation (Y axis only, around the focus point)")]
    [Tooltip("Degrees of yaw per full-screen-width mouse drag.")]
    [SerializeField] private float mouseRotateSpeed = 180f;
    [Tooltip("Multiplier applied to the raw two-finger twist angle.")]
    [SerializeField] private float touchRotateSensitivity = 1f;
    [SerializeField] private float rotateSmoothTime = 0.1f;

    private Camera cam;
    private Bounds cityBounds;
    private float groundY;
    private float pitchSin;

    private float lastAspect = -1f;
    private float lastYawForLimits = float.NaN;

    private float minZoomDistance;
    private float maxZoomDistance;
    private float targetZoomDistance;
    private float currentZoomDistance;
    private float zoomVelocityRef;

    private float targetYaw;
    private float currentYaw;
    private float yawVelocityRef;

    private Vector3 targetFocusPoint;
    private Vector3 currentFocusPoint;
    private Vector3 focusVelocityRef;

    private bool mousePanning;
    private Vector3 lastMousePos;
    private bool mouseRotating;
    private Vector3 lastMouseRotPos;

    private bool singleTouchDragging;
    private int dragFingerId = -1;
    private Vector2 lastTouchPos;

    private bool multiTouchActive;
    private float lastPinchDistance;
    private Vector2 lastTwistDir;
    private int previousTouchCount;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = fieldOfView;

        pitchSin = Mathf.Sin(pitch * Mathf.Deg2Rad);

        CalculateCityBounds();

        targetYaw = currentYaw = startYaw;
        RefreshLimits(true);

        Vector3 focusStart = ResolveCityFocus();
        targetZoomDistance = Mathf.Lerp(minZoomDistance, maxZoomDistance, startingZoomNormalized);
        targetFocusPoint = ClampFocusToBounds(focusStart, targetYaw, targetZoomDistance);

        currentFocusPoint = targetFocusPoint;
        currentZoomDistance = targetZoomDistance;

        ApplyCameraTransform();
    }

    private void Update()
    {
        RefreshLimits(false);

        if (Input.touchCount > 0)
        {
            mousePanning = false;
            mouseRotating = false;
            HandleTouchInput();
        }
        else
        {
            ResetTouchState();
            HandleMouseInput();
        }

        targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoomDistance, maxZoomDistance);
        targetFocusPoint = ClampFocusToBounds(targetFocusPoint, targetYaw, targetZoomDistance);

        currentFocusPoint = Vector3.SmoothDamp(currentFocusPoint, targetFocusPoint, ref focusVelocityRef, panSmoothTime);
        currentZoomDistance = Mathf.SmoothDamp(currentZoomDistance, targetZoomDistance, ref zoomVelocityRef, zoomSmoothTime);
        currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocityRef, rotateSmoothTime);

        // Final safety pass using the actually-rendered yaw/zoom, so damping lag can never
        // momentarily reveal ground beyond the city edge.
        currentFocusPoint = ClampFocusToBounds(currentFocusPoint, currentYaw, currentZoomDistance);

        ApplyCameraTransform();
    }

    private Vector3 ResolveCityFocus()
    {
        Transform t = startFocusOverride;

        if (t == null && !string.IsNullOrEmpty(startFocusObjectName))
        {
            GameObject namedStart = GameObject.Find(startFocusObjectName);
            if (namedStart != null) t = namedStart.transform;
        }

        if (t == null) t = cityFocus;

        if (t == null)
        {
            GameObject found = GameObject.Find("Cityfocus");
            if (found != null) t = found.transform;
        }

        Vector3 p = t != null ? t.position : cityBounds.center;
        p.y = groundY;
        return p;
    }

    private void CalculateCityBounds()
    {
        if (!autoDetectBounds)
        {
            cityBounds = manualBounds;
            groundY = cityBounds.min.y;
            return;
        }

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var candidates = new System.Collections.Generic.List<Renderer>(renderers.Length);

        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            candidates.Add(r);
        }

        if (candidates.Count == 0)
        {
            cityBounds = manualBounds;
            groundY = cityBounds.min.y;
            return;
        }

        cityBounds = ComputeInlierBounds(candidates);
        groundY = cityBounds.min.y;
    }

    // A handful of stray/mis-placed instances (leftover test props, a piece dragged far off
    // the map, etc.) can still be "active GameObjects actually instantiated in the scene"
    // while not being part of the real playable city. Blindly encapsulating every renderer
    // lets a single such outlier blow the bounds up by hundreds of units. Instead: find the
    // ground-plane median position of all candidates (robust to a few outliers by
    // construction), then only encapsulate renderers within a safety margin above the
    // 90th-percentile spread of the dense majority cluster - i.e. the actual city.
    private static Bounds ComputeInlierBounds(System.Collections.Generic.List<Renderer> candidates)
    {
        int n = candidates.Count;
        float[] xs = new float[n];
        float[] zs = new float[n];

        for (int i = 0; i < n; i++)
        {
            Vector3 c = candidates[i].bounds.center;
            xs[i] = c.x;
            zs[i] = c.z;
        }

        float medianX = Median(xs);
        float medianZ = Median(zs);

        float[] dists = new float[n];
        for (int i = 0; i < n; i++)
        {
            float dx = xs[i] - medianX;
            float dz = zs[i] - medianZ;
            dists[i] = Mathf.Sqrt(dx * dx + dz * dz);
        }

        float[] sortedDists = (float[])dists.Clone();
        System.Array.Sort(sortedDists);
        int p90Index = Mathf.Clamp(Mathf.FloorToInt(sortedDists.Length * 0.9f), 0, sortedDists.Length - 1);
        float outlierThreshold = Mathf.Max(sortedDists[p90Index] * 1.5f, 15f);

        Bounds combined = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        int inlierCount = 0;

        for (int i = 0; i < n; i++)
        {
            if (dists[i] > outlierThreshold) continue;
            inlierCount++;

            if (!hasBounds) { combined = candidates[i].bounds; hasBounds = true; }
            else combined.Encapsulate(candidates[i].bounds);
        }

        // Safety net: if the threshold somehow rejected almost everything, trust the raw data.
        if (inlierCount < Mathf.Max(3, n / 10))
        {
            hasBounds = false;
            for (int i = 0; i < n; i++)
            {
                if (!hasBounds) { combined = candidates[i].bounds; hasBounds = true; }
                else combined.Encapsulate(candidates[i].bounds);
            }
        }

        return combined;
    }

    private static float Median(float[] values)
    {
        float[] sorted = (float[])values.Clone();
        System.Array.Sort(sorted);
        int mid = sorted.Length / 2;
        return (sorted.Length % 2 == 0) ? (sorted[mid - 1] + sorted[mid]) * 0.5f : sorted[mid];
    }

    private void RefreshLimits(bool force)
    {
        float aspect = cam.aspect;
        bool aspectChanged = Mathf.Abs(aspect - lastAspect) > 0.0005f;
        bool yawChanged = float.IsNaN(lastYawForLimits) || Mathf.Abs(Mathf.DeltaAngle(currentYaw, lastYawForLimits)) > 0.5f;

        if (!force && !aspectChanged && !yawChanged) return;

        lastAspect = aspect;
        lastYawForLimits = currentYaw;
        CalculateZoomLimits(currentYaw);
    }

    // Both min and max zoom are solved from the camera's *actual* projection (via real
    // raycasts against the ground plane), not hand-derived trig, so they stay correct at
    // any yaw, aspect, FOV or pitch without re-deriving formulas by hand.
    private void CalculateZoomLimits(float yaw)
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = cityBounds.center;
        focus.y = groundY;

        float heightSafety = pitchSin > 0.0001f
            ? (cityBounds.max.y - groundY + heightSafetyMargin) / pitchSin
            : 10f;

        // Hard ceiling: the largest zoom-out that still keeps the whole visible ground
        // inside the real (padded) city bounds - this is the non-negotiable "never reveal
        // outside the city" guarantee.
        float hardMax = SolveMaxZoomForBox(rot, focus,
            cityBounds.min.x + edgePadding, cityBounds.max.x - edgePadding,
            cityBounds.min.z + edgePadding, cityBounds.max.z - edgePadding);

        // Preferred ceiling: a tighter, coverage-scaled box centered on the city, so the
        // default zoomed-out view shows a building-rich portion rather than the full
        // sprawling road network out to the fringes.
        float targetHalfX = Mathf.Max((cityBounds.extents.x - edgePadding) * maxZoomCoverageFraction, 5f);
        float targetHalfZ = Mathf.Max((cityBounds.extents.z - edgePadding) * maxZoomCoverageFraction, 5f);
        float preferredMax = SolveMaxZoomForBox(rot, focus,
            cityBounds.center.x - targetHalfX, cityBounds.center.x + targetHalfX,
            cityBounds.center.z - targetHalfZ, cityBounds.center.z + targetHalfZ);

        maxZoomDistance = maxZoomDistanceOverride > 0f ? Mathf.Min(preferredMax, maxZoomDistanceOverride) : preferredMax;

        float autoMin = Mathf.Max(maxZoomDistance * 0.18f, 8f);
        minZoomDistance = minZoomDistanceOverride > 0f
            ? Mathf.Max(minZoomDistanceOverride, heightSafety)
            : Mathf.Max(autoMin, heightSafety);

        // If height clearance genuinely needs more room than the coverage-limited max, let
        // max expand to match - but never past the hard "stay inside the city" ceiling.
        if (minZoomDistance > maxZoomDistance)
            maxZoomDistance = Mathf.Min(minZoomDistance * 1.05f, hardMax);

        minZoomDistance = Mathf.Min(minZoomDistance, hardMax * 0.95f);

        if (minZoomDistance >= maxZoomDistance)
            minZoomDistance = maxZoomDistance * 0.6f;
    }

    private float SolveMaxZoomForBox(Quaternion rot, Vector3 focus, float bMinX, float bMaxX, float bMinZ, float bMaxZ)
    {
        Vector3 viewDir = rot * Vector3.forward;
        float lo = 2f;
        float hi = 900f;

        for (int i = 0; i < 18; i++)
        {
            float mid = (lo + hi) * 0.5f;
            Vector3 camPos = focus - viewDir * mid;

            bool fits = TryComputeFootprintAABB(camPos, rot, out float minX, out float maxX, out float minZ, out float maxZ)
                && minX >= bMinX - 0.01f && maxX <= bMaxX + 0.01f
                && minZ >= bMinZ - 0.01f && maxZ <= bMaxZ + 0.01f;

            if (fits) lo = mid; else hi = mid;
        }

        return lo * 0.99f;
    }

    // Clamps a candidate ground-focus point so the entire visible ground rectangle at the
    // given yaw/zoom stays within (cityBounds - edgePadding). Correct at any rotation angle
    // because it uses the real camera projection, not a precomputed fixed-yaw formula.
    private Vector3 ClampFocusToBounds(Vector3 candidateFocus, float yaw, float zoomDistance)
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 camPos = candidateFocus - (rot * Vector3.forward) * zoomDistance;

        if (!TryComputeFootprintAABB(camPos, rot, out float minX, out float maxX, out float minZ, out float maxZ))
        {
            candidateFocus.y = groundY;
            return candidateFocus;
        }

        float allowedMinX = cityBounds.min.x + edgePadding;
        float allowedMaxX = cityBounds.max.x - edgePadding;
        float allowedMinZ = cityBounds.min.z + edgePadding;
        float allowedMaxZ = cityBounds.max.z - edgePadding;

        float correctedX = CorrectAxis(candidateFocus.x, minX, maxX, allowedMinX, allowedMaxX, cityBounds.center.x);
        float correctedZ = CorrectAxis(candidateFocus.z, minZ, maxZ, allowedMinZ, allowedMaxZ, cityBounds.center.z);

        return new Vector3(correctedX, groundY, correctedZ);
    }

    private static float CorrectAxis(float focusValue, float footMin, float footMax, float allowedMin, float allowedMax, float centerFallback)
    {
        if (footMax - footMin >= allowedMax - allowedMin) return centerFallback;
        if (footMin < allowedMin) return focusValue + (allowedMin - footMin);
        if (footMax > allowedMax) return focusValue - (footMax - allowedMax);
        return focusValue;
    }

    // Computes the world-space ground rectangle visible from a hypothetical camera pose by
    // temporarily moving the (already-disabled-from-rendering-mid-frame) transform and using
    // Unity's own ViewportPointToRay - safe because Update() runs fully before any rendering
    // happens, so no other script or the GPU ever observes the transient pose.
    private bool TryComputeFootprintAABB(Vector3 candidatePos, Quaternion candidateRot, out float minX, out float maxX, out float minZ, out float maxZ)
    {
        Vector3 savedPos = transform.position;
        Quaternion savedRot = transform.rotation;
        transform.SetPositionAndRotation(candidatePos, candidateRot);

        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        minX = float.PositiveInfinity; maxX = float.NegativeInfinity;
        minZ = float.PositiveInfinity; maxZ = float.NegativeInfinity;

        bool ok = true;
        ok &= TryAccumulateCorner(ground, 0f, 0f, ref minX, ref maxX, ref minZ, ref maxZ);
        ok &= TryAccumulateCorner(ground, 1f, 0f, ref minX, ref maxX, ref minZ, ref maxZ);
        ok &= TryAccumulateCorner(ground, 0f, 1f, ref minX, ref maxX, ref minZ, ref maxZ);
        ok &= TryAccumulateCorner(ground, 1f, 1f, ref minX, ref maxX, ref minZ, ref maxZ);

        transform.SetPositionAndRotation(savedPos, savedRot);
        return ok;
    }

    private bool TryAccumulateCorner(Plane ground, float vx, float vy, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
        if (!ground.Raycast(ray, out float t) || t <= 0f) return false;

        Vector3 p = ray.GetPoint(t);
        if (p.x < minX) minX = p.x;
        if (p.x > maxX) maxX = p.x;
        if (p.z < minZ) minZ = p.z;
        if (p.z > maxZ) maxZ = p.z;
        return true;
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI(-1))
        {
            mousePanning = true;
            lastMousePos = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(0)) mousePanning = false;

        if (mousePanning && Input.GetMouseButton(0))
        {
            PanByScreenPositions(lastMousePos, Input.mousePosition);
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButtonDown(1) && !IsPointerOverUI(-1))
        {
            mouseRotating = true;
            lastMouseRotPos = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(1)) mouseRotating = false;

        if (mouseRotating && Input.GetMouseButton(1))
        {
            float dx = Input.mousePosition.x - lastMouseRotPos.x;
            float normalizedDx = dx / Mathf.Max(Screen.width, 1);
            targetYaw += normalizedDx * mouseRotateSpeed;
            lastMouseRotPos = Input.mousePosition;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            float range = maxZoomDistance - minZoomDistance;
            targetZoomDistance = Mathf.Clamp(targetZoomDistance - scroll * mouseZoomSpeed * range, minZoomDistance, maxZoomDistance);
        }
    }

    private void HandleTouchInput()
    {
        int touchCount = Input.touchCount;

        // Any change in finger count starts a fresh gesture baseline instead of reusing a
        // stale delta from the previous configuration - this is what prevents sudden jumps
        // when a second finger touches down or one finger lifts mid-gesture.
        if (touchCount != previousTouchCount)
        {
            singleTouchDragging = false;
            multiTouchActive = false;
        }

        if (touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (!singleTouchDragging)
            {
                if (!IsPointerOverUI(touch.fingerId) &&
                    touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                {
                    singleTouchDragging = true;
                    dragFingerId = touch.fingerId;
                    lastTouchPos = touch.position;
                }
            }
            else if (touch.fingerId == dragFingerId)
            {
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    PanByScreenPositions(lastTouchPos, touch.position);
                    lastTouchPos = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    singleTouchDragging = false;
                    dragFingerId = -1;
                }
            }
        }
        else if (touchCount >= 2)
        {
            singleTouchDragging = false;
            dragFingerId = -1;

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            if (IsPointerOverUI(t0.fingerId) || IsPointerOverUI(t1.fingerId))
            {
                multiTouchActive = false;
            }
            else
            {
                float currentDistance = Vector2.Distance(t0.position, t1.position);
                Vector2 currentDir = t1.position - t0.position;

                if (!multiTouchActive)
                {
                    multiTouchActive = true;
                    lastPinchDistance = currentDistance;
                    lastTwistDir = currentDir;
                }
                else
                {
                    float pinchDelta = currentDistance - lastPinchDistance;
                    float normalizedPinch = pinchDelta / Mathf.Max(Mathf.Min(Screen.width, Screen.height), 1);
                    float range = maxZoomDistance - minZoomDistance;
                    targetZoomDistance = Mathf.Clamp(
                        targetZoomDistance - normalizedPinch * touchZoomSpeed * range,
                        minZoomDistance, maxZoomDistance);

                    if (currentDir.sqrMagnitude > 4f && lastTwistDir.sqrMagnitude > 4f)
                    {
                        float angleDelta = Vector2.SignedAngle(lastTwistDir, currentDir);
                        targetYaw += angleDelta * touchRotateSensitivity;
                    }

                    lastPinchDistance = currentDistance;
                    lastTwistDir = currentDir;
                }
            }
        }

        previousTouchCount = touchCount;
    }

    private void ResetTouchState()
    {
        singleTouchDragging = false;
        multiTouchActive = false;
        dragFingerId = -1;
        previousTouchCount = 0;
    }

    // Exact "grab the ground and drag" panning: finds the world point under the pointer
    // before and after the move (via the camera's real projection) and shifts the focus by
    // the difference, so the point under the cursor/finger stays under it. Correct in every
    // direction (forward/back/left/right) at any yaw, and resolution-independent because it
    // uses actual screen coordinates through the camera's own projection matrix.
    private void PanByScreenPositions(Vector2 prevScreenPos, Vector2 currScreenPos)
    {
        Plane ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        Ray rayPrev = cam.ScreenPointToRay(prevScreenPos);
        Ray rayCurr = cam.ScreenPointToRay(currScreenPos);

        if (ground.Raycast(rayPrev, out float tPrev) && ground.Raycast(rayCurr, out float tCurr) && tPrev > 0f && tCurr > 0f)
        {
            Vector3 worldPrev = rayPrev.GetPoint(tPrev);
            Vector3 worldCurr = rayCurr.GetPoint(tCurr);
            targetFocusPoint += (worldPrev - worldCurr);
        }
    }

    private void ApplyCameraTransform()
    {
        Quaternion rot = Quaternion.Euler(pitch, currentYaw, 0f);
        Vector3 viewDir = rot * Vector3.forward;
        transform.SetPositionAndRotation(currentFocusPoint - viewDir * currentZoomDistance, rot);
    }

    private bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        return fingerId < 0
            ? EventSystem.current.IsPointerOverGameObject()
            : EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(cityBounds.center, cityBounds.size);

        Gizmos.color = Color.cyan;
        Vector3 safeCenter = new Vector3(cityBounds.center.x, cityBounds.min.y, cityBounds.center.z);
        Vector3 safeSize = new Vector3(
            Mathf.Max(cityBounds.size.x - edgePadding * 2f, 0f),
            0.1f,
            Mathf.Max(cityBounds.size.z - edgePadding * 2f, 0f));
        Gizmos.DrawWireCube(safeCenter, safeSize);

        if (cityFocus != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(cityFocus.position, 1f);
        }
    }
}
