using System;

namespace StartFlow
{
    // Plain data payload collected across the onboarding flow. Deliberately not a
    // ScriptableObject asset (which would leak state between Editor play sessions) - it's
    // carried in memory by PlayerDataManager instead.
    [Serializable]
    public class PlayerSetupData
    {
        public string playerName = string.Empty;
        public string financialGoal = string.Empty;
        public float goalTargetAmount;
        public float monthlyMoney;
        public float currentAvailableMoney;
        public float savedAmount;
        public float investedAmount;
        public float spentAmount;
        public float donatedAmount;
    }
}
