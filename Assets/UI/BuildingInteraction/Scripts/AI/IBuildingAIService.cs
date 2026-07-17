using System.Threading.Tasks;

namespace BuildingInteractionSystem
{
    // Implement this against a real provider later (e.g. RealBuildingAIService) and switch
    // AIServiceConfig.providerMode - no UI code needs to change.
    public interface IBuildingAIService
    {
        Task<BuildingAIResponse> AskAsync(BuildingAIRequest request);
    }
}
