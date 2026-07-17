using System;

namespace BuildingInteractionSystem
{
    // Deliberately generic and non-identifying: no player name, ID, or account data.
    // This struct only ever lives in memory for the duration of a single AI request and
    // is never written to disk, PlayerPrefs, or sent anywhere except a future AI service.
    [Serializable]
    public struct PlayerFinancialContext
    {
        public float availableBalance;
        public float monthlyIncome;
        public float monthlyEssentialExpenses;
        public float currentSavingsGoal;

        public static PlayerFinancialContext Empty => new PlayerFinancialContext();
    }
}
