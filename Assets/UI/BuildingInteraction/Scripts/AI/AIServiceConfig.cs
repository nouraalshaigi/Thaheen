using UnityEngine;

namespace BuildingInteractionSystem
{
    public enum AIProviderMode
    {
        Mock,
        RealApi // reserved for a future paid provider - not implemented yet
    }

    // Deliberately a ScriptableObject asset, not a scene object or MonoBehaviour field, so
    // future API configuration lives in one asset separate from any scene/prefab. Never put
    // a real API key in this asset - it would ship inside the built game.
    [CreateAssetMenu(menuName = "Building Interaction/AI Service Config", fileName = "AIServiceConfig")]
    public class AIServiceConfig : ScriptableObject
    {
        [Header("Provider Selection")]
        public AIProviderMode providerMode = AIProviderMode.Mock;

        [Header("Future Real API Settings (placeholder only)")]
        [Tooltip("Never store a real API key here or in any versioned asset. Load it at runtime from a secure, non-versioned source (env var, secure server-side proxy, etc.) once a real provider is implemented.")]
        public string apiEndpointPlaceholder = string.Empty;

        [Header("Mock Settings")]
        [Tooltip("Simulated response delay in milliseconds, used only by MockBuildingAIService.")]
        public int mockResponseDelayMs = 700;
    }
}
