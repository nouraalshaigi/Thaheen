using UnityEngine;
using UnityEngine.EventSystems;

namespace BuildingInteractionSystem
{
    // Central owner of "which building was clicked/tapped" and "the one popup instance".
    // Deliberately does not read anything from the camera script (camera system must not be
    // touched) - a click/tap is instead distinguished from a camera drag/rotate/pinch purely
    // by its own movement and duration, which works regardless of what the camera does.
    public class BuildingInteractionManager : MonoBehaviour
    {
        public static BuildingInteractionManager Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Canvas popupCanvas;
        [SerializeField] private BuildingPopupController popupPrefab;
        [SerializeField] private LayerMask buildingLayerMask = ~0;
        [SerializeField] private float maxRaycastDistance = 500f;

        [Header("Click vs. Drag/Rotate Detection")]
        [Tooltip("A press must release within this long, and within the movement threshold below, to count as a click/tap rather than a camera drag.")]
        [SerializeField] private float clickMaxDuration = 0.35f;
        [SerializeField] private float clickMaxMovementPixels = 14f;

        private BuildingPopupController activePopup;
        private BuildingInteraction hoveredBuilding;

        private bool mouseDownOnBuilding;
        private Vector2 mouseDownPos;
        private float mouseDownTime;
        private BuildingInteraction mouseDownTarget;

        private bool touchDownOnBuilding;
        private Vector2 touchDownPos;
        private float touchDownTime;
        private BuildingInteraction touchDownTarget;
        private int touchDownFingerId = -1;
        private bool touchGestureInvalidated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void Update()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return;

            if (Input.touchCount > 0)
            {
                HandleTouch();
                ClearHover();
            }
            else
            {
                touchDownOnBuilding = false;
                touchDownTarget = null;
                touchDownFingerId = -1;
                touchGestureInvalidated = false;

                HandleMouse();
                UpdateHover();
            }
        }

        private void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (!IsPointerOverUI(-1))
                {
                    BuildingInteraction hit = RaycastBuilding(Input.mousePosition);
                    mouseDownOnBuilding = hit != null;
                    mouseDownTarget = hit;
                    mouseDownPos = Input.mousePosition;
                    mouseDownTime = Time.unscaledTime;
                    if (hit != null) hit.PlayTapFeedback();
                }
                else
                {
                    mouseDownOnBuilding = false;
                    mouseDownTarget = null;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (mouseDownOnBuilding && !IsPointerOverUI(-1))
                {
                    float dt = Time.unscaledTime - mouseDownTime;
                    float dist = Vector2.Distance(Input.mousePosition, mouseDownPos);

                    if (dt <= clickMaxDuration && dist <= clickMaxMovementPixels)
                    {
                        BuildingInteraction hitNow = RaycastBuilding(Input.mousePosition);
                        if (hitNow == mouseDownTarget) OpenPopup(mouseDownTarget.Data);
                    }
                }

                mouseDownOnBuilding = false;
                mouseDownTarget = null;
            }
        }

        private void HandleTouch()
        {
            int touchCount = Input.touchCount;

            if (touchCount > 1)
                touchGestureInvalidated = true;

            Touch tracked = default;
            bool found = false;
            for (int i = 0; i < touchCount; i++)
            {
                Touch candidate = Input.GetTouch(i);
                if (touchDownFingerId == -1 || candidate.fingerId == touchDownFingerId)
                {
                    tracked = candidate;
                    found = true;
                    break;
                }
            }
            if (!found) return;

            if (tracked.phase == TouchPhase.Began && touchDownFingerId == -1)
            {
                touchGestureInvalidated = touchCount > 1;

                if (!IsPointerOverUI(tracked.fingerId))
                {
                    BuildingInteraction hit = RaycastBuilding(tracked.position);
                    touchDownOnBuilding = hit != null;
                    touchDownTarget = hit;
                    touchDownPos = tracked.position;
                    touchDownTime = Time.unscaledTime;
                    touchDownFingerId = tracked.fingerId;
                    if (hit != null) hit.PlayTapFeedback();
                }
            }
            else if ((tracked.phase == TouchPhase.Ended || tracked.phase == TouchPhase.Canceled) && tracked.fingerId == touchDownFingerId)
            {
                if (tracked.phase == TouchPhase.Ended && touchDownOnBuilding && !touchGestureInvalidated && !IsPointerOverUI(tracked.fingerId))
                {
                    float dt = Time.unscaledTime - touchDownTime;
                    float dist = Vector2.Distance(tracked.position, touchDownPos);

                    if (dt <= clickMaxDuration && dist <= clickMaxMovementPixels)
                    {
                        BuildingInteraction hitNow = RaycastBuilding(tracked.position);
                        if (hitNow == touchDownTarget) OpenPopup(touchDownTarget.Data);
                    }
                }

                touchDownOnBuilding = false;
                touchDownTarget = null;
                touchDownFingerId = -1;
                touchGestureInvalidated = false;
            }
        }

        private void UpdateHover()
        {
            BuildingInteraction target = !IsPointerOverUI(-1) ? RaycastBuilding(Input.mousePosition) : null;

            if (target == hoveredBuilding) return;

            if (hoveredBuilding != null) hoveredBuilding.SetHovered(false);
            hoveredBuilding = target;
            if (hoveredBuilding != null) hoveredBuilding.SetHovered(true);
        }

        private void ClearHover()
        {
            if (hoveredBuilding == null) return;
            hoveredBuilding.SetHovered(false);
            hoveredBuilding = null;
        }

        private BuildingInteraction RaycastBuilding(Vector2 screenPos)
        {
            Ray ray = worldCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, buildingLayerMask, QueryTriggerInteraction.Collide))
                return hit.collider.GetComponentInParent<BuildingInteraction>();
            return null;
        }

        private bool IsPointerOverUI(int fingerId)
        {
            if (EventSystem.current == null) return false;
            return fingerId < 0
                ? EventSystem.current.IsPointerOverGameObject()
                : EventSystem.current.IsPointerOverGameObject(fingerId);
        }

        public void OpenPopup(BuildingData data)
        {
            if (data == null) return;
            EnsurePopupInstance();
            activePopup.Show(data);
        }

        public void ClosePopup()
        {
            if (activePopup != null) activePopup.Hide();
        }

        private void EnsurePopupInstance()
        {
            if (activePopup != null) return;

            Transform parent = popupCanvas != null ? popupCanvas.transform : FindOrCreateOverlayCanvas().transform;
            activePopup = Instantiate(popupPrefab, parent);
            activePopup.Initialize(this);
        }

        private Canvas FindOrCreateOverlayCanvas()
        {
            GameObject go = new GameObject("BuildingInteraction_Canvas", typeof(RectTransform));
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<UnityEngine.UI.CanvasScaler>();
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            popupCanvas = canvas;
            return canvas;
        }
    }
}
