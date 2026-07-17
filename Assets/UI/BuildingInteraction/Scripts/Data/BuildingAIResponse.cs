using System;

namespace BuildingInteractionSystem
{
    [Serializable]
    public struct BuildingAIResponse
    {
        public bool success;
        public string aiResponse;
        public string suggestedAction;
        public string errorMessage;

        public static BuildingAIResponse Failure(string error) => new BuildingAIResponse
        {
            success = false,
            errorMessage = error,
            aiResponse = string.Empty,
            suggestedAction = string.Empty
        };
    }
}
