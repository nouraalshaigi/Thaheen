using UnityEngine;

namespace BuildingInteractionSystem
{
    // Single place that decides which IBuildingAIService implementation is active. When a
    // real provider is ready: implement it (e.g. RealBuildingAIService : IBuildingAIService),
    // return it below when providerMode == RealApi, and nothing in the popup UI needs to change.
    public static class AIServiceLocator
    {
        public static IBuildingAIService Resolve(AIServiceConfig config)
        {
            int delay = config != null ? config.mockResponseDelayMs : 700;

            if (config != null && config.providerMode == AIProviderMode.RealApi)
            {
                // Future real-provider integration point:
                // return new RealBuildingAIService(config);
                Debug.LogWarning("BuildingInteraction: AIProviderMode.RealApi is not implemented yet - falling back to MockBuildingAIService.");
            }

            return new MockBuildingAIService(delay);
        }
    }
}
