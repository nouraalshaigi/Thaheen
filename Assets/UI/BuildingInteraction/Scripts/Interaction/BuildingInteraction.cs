using System.Collections;
using UnityEngine;

namespace BuildingInteractionSystem
{
    // Attach to an interactive building's root GameObject. Purely passive: it exposes the
    // building's data and plays subtle hover/tap feedback. All click/tap detection, "ignore
    // over UI", and "ignore while the camera is being dragged" logic lives centrally in
    // BuildingInteractionManager so it stays consistent across every building.
    public class BuildingInteraction : MonoBehaviour
    {
        [SerializeField] private BuildingData buildingData;

        [Header("Feedback")]
        [SerializeField] private float hoverScaleMultiplier = 1.025f;
        [SerializeField] private float tapScaleMultiplier = 0.965f;
        [SerializeField] private float feedbackLerpSpeed = 10f;
        [SerializeField] private float tapPulseDuration = 0.12f;

        public BuildingData Data => buildingData;

        private Vector3 baseScale;
        private float targetScaleMultiplier = 1f;
        private float currentScaleMultiplier = 1f;
        private bool isHovered;
        private Coroutine tapRoutine;

        private void Awake()
        {
            EnsureCollider();
            baseScale = transform.localScale;
        }

        // Adds a collider only if this building doesn't already have one - most imported
        // FBX meshes don't get one automatically, but we never touch/replace an existing one.
        private void EnsureCollider()
        {
            if (GetComponent<Collider>() != null) return;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                worldBounds.Encapsulate(renderers[i].bounds);

            Vector3 lossyScale = transform.lossyScale;
            Vector3 safeScale = new Vector3(
                Mathf.Abs(lossyScale.x) > 0.0001f ? lossyScale.x : 1f,
                Mathf.Abs(lossyScale.y) > 0.0001f ? lossyScale.y : 1f,
                Mathf.Abs(lossyScale.z) > 0.0001f ? lossyScale.z : 1f);

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(worldBounds.center);
            box.size = new Vector3(
                worldBounds.size.x / safeScale.x,
                worldBounds.size.y / safeScale.y,
                worldBounds.size.z / safeScale.z);
        }

        private void Update()
        {
            if (Mathf.Approximately(currentScaleMultiplier, targetScaleMultiplier)) return;

            currentScaleMultiplier = Mathf.Lerp(currentScaleMultiplier, targetScaleMultiplier, Time.unscaledDeltaTime * feedbackLerpSpeed);
            transform.localScale = baseScale * currentScaleMultiplier;
        }

        // Laptop: subtle hover feedback.
        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            if (tapRoutine == null)
                targetScaleMultiplier = hovered ? hoverScaleMultiplier : 1f;
        }

        // Mobile/iPad (and mouse press): subtle tap feedback.
        public void PlayTapFeedback()
        {
            if (tapRoutine != null) StopCoroutine(tapRoutine);
            tapRoutine = StartCoroutine(TapPulseRoutine());
        }

        private IEnumerator TapPulseRoutine()
        {
            targetScaleMultiplier = tapScaleMultiplier;
            yield return new WaitForSecondsRealtime(tapPulseDuration);
            tapRoutine = null;
            targetScaleMultiplier = isHovered ? hoverScaleMultiplier : 1f;
        }
    }
}
