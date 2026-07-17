using System;

namespace BuildingInteractionSystem
{
    [Serializable]
    public struct BuildingAIRequest
    {
        public BuildingId buildingId;
        public string buildingName;
        public string playerQuestion;
        public PlayerFinancialContext financialContext;
    }
}
