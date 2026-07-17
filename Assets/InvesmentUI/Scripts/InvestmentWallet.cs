namespace InvestmentTowerUI
{
    // Bridges the Investment Tower's "Available Balance" to the player's real wallet -
    // StartFlow.PlayerDataManager.Data.currentAvailableMoney - the exact same field
    // BuildingInteractionSystem.TransactionPopupView already reads/writes for the Charity/Mall/
    // Savings popups, so every wallet-affecting screen in the game shares one source of truth.
    // currentAvailableMoney starts out equal to the monthly amount the player entered during
    // onboarding (see StartFlow.MonthlyMoneyScreenController), so no separate initialization is
    // needed here.
    //
    // Falls back to a plain in-memory value only while StartFlow.PlayerDataManager has not been
    // created yet (e.g. opening this scene directly, without going through StartFlowScene first).
    // The fallback is seeded once from InvestmentTowerUIController's serialized "Starting
    // Balance" field. As soon as a PlayerDataManager exists - which happens automatically once
    // onboarding runs, since it survives the scene load via DontDestroyOnLoad - every read/write
    // here transparently switches to the real data with no extra wiring.
    public static class InvestmentWallet
    {
        private static float fallbackBalance;
        private static bool fallbackSeeded;

        public static void SeedFallback(float startingBalance)
        {
            if (fallbackSeeded) return;
            fallbackSeeded = true;
            fallbackBalance = startingBalance;
        }

        public static float CurrentBalance
        {
            get => StartFlow.PlayerDataManager.Instance != null
                ? StartFlow.PlayerDataManager.Instance.Data.currentAvailableMoney
                : fallbackBalance;
            set
            {
                if (StartFlow.PlayerDataManager.Instance != null)
                    StartFlow.PlayerDataManager.Instance.Data.currentAvailableMoney = value;
                else
                    fallbackBalance = value;
            }
        }
    }
}
