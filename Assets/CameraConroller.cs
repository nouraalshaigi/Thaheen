using UnityEngine;
using UnityEngine.EventSystems;

public class CityCameraController : MonoBehaviour
{
    [Header("City Target")]
    [Tooltip("Empty GameObject positioned at the center of the whole city.")]
    public Transform cityCenter;

    [Header("Starting View")]
    public float startingDistance = 45f;
    public float startingYaw = 45f;
    public float startingPitch = 35f;

    [Header("Rotation")]
    public float mouseRotationSpeed = 4f;
    public float touchRotationSpeed = 0.15f;
    public float minPitch = 15f;
    public float maxPitch = 75f;

    [Header("Zoom")]
    public float mouseZoomSpeed = 8f;
    public float touchZoomSpeed = 0.08f;
    public float minDistance = 12f;
    public float maxDistance = 90f;

    [Header("Movement")]
    public float mousePanSpeed = 0.04f;
    public float touchPanSpeed = 0.015f;

    [Header("City Movement Limits")]
    public bool useMovementLimits = true;
    public float minX = -35f;
    public float maxX = 35f;
    public float minZ = -35f;
    public float maxZ = 35f;

    [Header("Smoothing")]
    public float movementSmoothness = 12f;
    public float rotationSmoothness = 12f;
    public float zoomSmoothness = 12f;

    private float targetYaw;
    private float targetPitch;
    private float targetDistance;

    private float currentYaw;
    private float currentPitch;
    private float currentDistance;

    private Vector3 targetCenterPosition;
    private Vector3 currentCenterPosition;

    private Vector3 previousMousePosition;

    private Vector2 previousTouchCenter;
    private float previousTouchDistance;
    private bool twoFingerGestureStarted;

    private void Start()
    {
        if (cityCenter == null)
        {
            GameObject centerObject = new GameObject("City Camera Center");
            centerObject.transform.position = Vector3.zero;
            cityCenter = centerObject.transform;

            Debug.LogWarning(
                "City Center was not assigned. A temporary center was created at position 0,0,0."
            );
        }

        targetYaw = startingYaw;
        targetPitch = startingPitch;
        targetDistance = startingDistance;

        currentYaw = targetYaw;
        currentPitch = targetPitch;
        currentDistance = targetDistance;

        targetCenterPosition = cityCenter.position;
        currentCenterPosition = targetCenterPosition;

        UpdateCameraImmediately();
    }

    private void Update()
    {
        HandleMouseControls();
        HandleTouchControls();
        ClampValues();
    }

    private void LateUpdate()
    {
        ApplySmoothCameraMovement();
    }

    private void HandleMouseControls()
    {
        // Ignore mouse controls while touching the screen.
        if (Input.touchCount > 0)
            return;

        // Right mouse button: rotate around the city.
        if (Input.GetMouseButtonDown(1))
            previousMousePosition = Input.mousePosition;

        if (Input.GetMouseButton(1))
        {
            Vector3 mouseDelta = Input.mousePosition - previousMousePosition;

            targetYaw += mouseDelta.x * mouseRotationSpeed * 0.1f;
            targetPitch -= mouseDelta.y * mouseRotationSpeed * 0.1f;

            previousMousePosition = Input.mousePosition;
        }

        // Middle mouse button or Shift + left mouse button: move the view.
        bool panStarted =
            Input.GetMouseButtonDown(2) ||
            (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonDown(0));

        bool panning =
            Input.GetMouseButton(2) ||
            (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButton(0));

        if (panStarted)
            previousMousePosition = Input.mousePosition;

        if (panning)
        {
            Vector3 mouseDelta = Input.mousePosition - previousMousePosition;
            PanCamera(mouseDelta, mousePanSpeed);

            previousMousePosition = Input.mousePosition;
        }

        // Mouse wheel: zoom.
        float scroll = Input.mouseScrollDelta.y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetDistance -= scroll * mouseZoomSpeed;
        }
    }

    private void HandleTouchControls()
    {
        if (Input.touchCount == 0)
        {
            twoFingerGestureStarted = false;
            return;
        }

        // One finger: rotate.
        if (Input.touchCount == 1)
        {
            twoFingerGestureStarted = false;

            Touch touch = Input.GetTouch(0);

            if (IsTouchOverUI(touch.fingerId))
                return;

            if (touch.phase == TouchPhase.Moved)
            {
                targetYaw += touch.deltaPosition.x * touchRotationSpeed;
                targetPitch -= touch.deltaPosition.y * touchRotationSpeed;
            }

            return;
        }

        // Two fingers: pinch zoom and move the city view.
        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);

        if (IsTouchOverUI(touchZero.fingerId) || IsTouchOverUI(touchOne.fingerId))
            return;

        Vector2 currentTouchCenter =
            (touchZero.position + touchOne.position) * 0.5f;

        float currentTouchDistance =
            Vector2.Distance(touchZero.position, touchOne.position);

        if (!twoFingerGestureStarted ||
            touchZero.phase == TouchPhase.Began ||
            touchOne.phase == TouchPhase.Began)
        {
            previousTouchCenter = currentTouchCenter;
            previousTouchDistance = currentTouchDistance;
            twoFingerGestureStarted = true;
            return;
        }

        // Pinch zoom.
        float pinchDifference =
            currentTouchDistance - previousTouchDistance;

        targetDistance -= pinchDifference * touchZoomSpeed;

        // Move using the center point of both fingers.
        Vector2 centerDifference =
            currentTouchCenter - previousTouchCenter;

        PanCamera(centerDifference, touchPanSpeed);

        previousTouchCenter = currentTouchCenter;
        previousTouchDistance = currentTouchDistance;
    }

    private void PanCamera(Vector2 inputDelta, float speed)
    {
        Quaternion horizontalRotation =
            Quaternion.Euler(0f, targetYaw, 0f);

        Vector3 cameraRight =
            horizontalRotation * Vector3.right;

        Vector3 cameraForward =
            horizontalRotation * Vector3.forward;

        Vector3 movement =
            (-cameraRight * inputDelta.x -
             cameraForward * inputDelta.y) *
            speed *
            Mathf.Max(1f, targetDistance * 0.05f);

        targetCenterPosition += movement;
    }

    private void ClampValues()
    {
        targetPitch = Mathf.Clamp(
            targetPitch,
            minPitch,
            maxPitch
        );

        targetDistance = Mathf.Clamp(
            targetDistance,
            minDistance,
            maxDistance
        );

        if (useMovementLimits)
        {
            targetCenterPosition.x = Mathf.Clamp(
                targetCenterPosition.x,
                minX,
                maxX
            );

            targetCenterPosition.z = Mathf.Clamp(
                targetCenterPosition.z,
                minZ,
                maxZ
            );
        }
    }

    private void ApplySmoothCameraMovement()
    {
        float rotationLerp =
            1f - Mathf.Exp(-rotationSmoothness * Time.deltaTime);

        float zoomLerp =
            1f - Mathf.Exp(-zoomSmoothness * Time.deltaTime);

        float movementLerp =
            1f - Mathf.Exp(-movementSmoothness * Time.deltaTime);

        currentYaw = Mathf.LerpAngle(
            currentYaw,
            targetYaw,
            rotationLerp
        );

        currentPitch = Mathf.Lerp(
            currentPitch,
            targetPitch,
            rotationLerp
        );

        currentDistance = Mathf.Lerp(
            currentDistance,
            targetDistance,
            zoomLerp
        );

        currentCenterPosition = Vector3.Lerp(
            currentCenterPosition,
            targetCenterPosition,
            movementLerp
        );

        Quaternion rotation =
            Quaternion.Euler(currentPitch, currentYaw, 0f);

        Vector3 cameraOffset =
            rotation * new Vector3(0f, 0f, -currentDistance);

        transform.position =
            currentCenterPosition + cameraOffset;

        transform.rotation = rotation;
    }

    private void UpdateCameraImmediately()
    {
        Quaternion rotation =
            Quaternion.Euler(currentPitch, currentYaw, 0f);

        transform.position =
            currentCenterPosition +
            rotation * new Vector3(0f, 0f, -currentDistance);

        transform.rotation = rotation;
    }

    private bool IsTouchOverUI(int fingerId)
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject(fingerId);
    }
}