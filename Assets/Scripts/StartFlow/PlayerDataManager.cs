using System;
using UnityEngine;

namespace StartFlow
{
    // Survives the StartFlowScene -> OGscene load via DontDestroyOnLoad, so onboarding
    // choices reach the game without touching disk. This is the single source of truth
    // while the app is running. PlayerPrefs is only ever used as an optional, best-effort
    // fallback (see TrySaveFallback/TryLoadFallback) - never the primary path, and never for
    // anything sensitive.
    public class PlayerDataManager : MonoBehaviour
    {
        private const string FallbackPrefsKey = "StartFlow.PlayerSetupData.Fallback";

        public static PlayerDataManager Instance { get; private set; }

        public PlayerSetupData Data { get; private set; } = new PlayerSetupData();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        // Safe to call from any screen controller even before this component exists in the
        // scene yet - creates it on first use.
        public static PlayerDataManager GetOrCreate()
        {
            if (Instance != null) return Instance;

            GameObject go = new GameObject("PlayerDataManager");
            return go.AddComponent<PlayerDataManager>();
        }

        // ------------------------------------------------------------------------ wallet (single source of truth)
        //
        // Every system that spends or earns the player's money (TransactionPopupView's Charity/
        // Mall/Savings popups, InvestmentSampleData's buy/sell) must go through these methods
        // instead of touching Data.currentAvailableMoney directly, so there is exactly one place
        // that enforces "never negative" and exactly one place that refreshes the HUD.

        public bool CanAfford(float amount) => amount > 0f && amount <= Data.currentAvailableMoney;

        // Returns false (and changes nothing) if the amount is invalid or unaffordable - callers
        // must not proceed with the transaction when this returns false.
        public bool TrySpendMoney(float amount)
        {
            if (!CanAfford(amount)) return false;

            Data.currentAvailableMoney -= amount;
            RefreshMoneyUI();
            return true;
        }

        public void AddMoney(float amount)
        {
            if (amount <= 0f) return;

            Data.currentAvailableMoney += amount;
            RefreshMoneyUI();
        }

        // Pushes the current balance to the existing City HUD immediately - never rebuilds or
        // resizes it, just re-reads the already-serialized text fields (see
        // CityHud.CityHudController.RefreshFromPlayerData). A no-op if the HUD isn't loaded
        // (e.g. StartFlowScene, or OGscene before City_HUD's Awake has run yet).
        public void RefreshMoneyUI()
        {
            if (CityHud.CityHudController.Instance != null)
                CityHud.CityHudController.Instance.RefreshFromPlayerData();
        }

        public void TrySaveFallback()
        {
            try
            {
                string json = JsonUtility.ToJson(Data);
                PlayerPrefs.SetString(FallbackPrefsKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PlayerDataManager: fallback save failed - {ex.Message}");
            }
        }

        // Only meaningful if a scene is entered directly (e.g. testing OGscene on its own)
        // without ever going through StartFlowScene.
        public bool TryLoadFallback()
        {
            if (!PlayerPrefs.HasKey(FallbackPrefsKey)) return false;

            try
            {
                string json = PlayerPrefs.GetString(FallbackPrefsKey);
                PlayerSetupData loaded = JsonUtility.FromJson<PlayerSetupData>(json);
                if (loaded != null)
                {
                    Data = loaded;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PlayerDataManager: fallback load failed - {ex.Message}");
            }

            return false;
        }
    }
}
