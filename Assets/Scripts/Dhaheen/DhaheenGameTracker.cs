using System;
using System.Collections.Generic;
using BuildingInteractionSystem;
using CityHud;
using InvestmentTowerUI;
using StartFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dhaheen
{
    // Records the player's real financial decisions (never hard-coded values) and drives the AI
    // Report button end to end: open panel -> loading -> call the API -> display result/error.
    //
    // Self-creating DontDestroyOnLoad singleton (same pattern as StartFlow.PlayerDataManager),
    // so exactly one tracker exists for the whole session regardless of scene changes - never a
    // duplicate manager. It only OBSERVES already-completed actions via events added to
    // TransactionPopupView and InvestmentTowerUIController; it never mutates wallet/balance
    // fields itself - those systems remain solely responsible for their own money.
    public class DhaheenGameTracker : MonoBehaviour
    {
        public static DhaheenGameTracker Instance { get; private set; }

        [Header("Used only if the project doesn't collect player age during onboarding yet")]
        [SerializeField] private int fallbackAge = 12;

        private const string PlayerIdPrefsKey = "Dhaheen.PlayerId";

        // The shop has no need/want/luxury categories and never will via this integration - a
        // fixed, API-only value so item_type is never empty for a "shopping" decision. Never
        // shown in any UI.
        private const string ShoppingItemType = "want";

        private readonly List<DhaheenDecision> decisions = new List<DhaheenDecision>();
        private readonly List<TransactionPopupView> subscribedPopups = new List<TransactionPopupView>();
        private InvestmentTowerUIController subscribedInvestmentController;
        private CityHudController subscribedHud;

        public static DhaheenGameTracker GetOrCreate()
        {
            if (Instance != null) return Instance;
            GameObject go = new GameObject("DhaheenGameTracker");
            return go.AddComponent<DhaheenGameTracker>();
        }

        // ROOT CAUSE of "AI Report opens but analysis never appears": nothing was ever calling
        // GetOrCreate() for this tracker, so Instance stayed null forever, Awake()/Start() never
        // ran, and CityHudController.AiReportRequested had zero subscribers - clicking the button
        // opened the panel and fired the event into the void. RuntimeInitializeOnLoadMethod
        // guarantees exactly one tracker exists early in the session (guarded by the Awake()
        // dedup above, so this can never create a duplicate even if called more than once).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => GetOrCreate();

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
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Start() => TryConnectToGameplaySystems();

        // Forces a full re-scan on every scene load rather than relying on Update()'s cheaper
        // per-field null checks, since a reload can leave subscribedPopups non-empty but full of
        // now-destroyed references.
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            subscribedHud = null;
            subscribedInvestmentController = null;
            subscribedPopups.Clear();
            TryConnectToGameplaySystems();
        }

        // The gameplay objects this tracker observes (CityHudController, the 3
        // TransactionPopupView instances, InvestmentTowerUIController) are scene-local, not
        // singletons reachable the moment this DontDestroyOnLoad tracker is created - retried
        // every frame until found (cheap null checks) and re-attempted on every scene load so a
        // scene reload re-wires against the freshly created instances.
        private void Update()
        {
            if (subscribedHud == null || subscribedInvestmentController == null || subscribedPopups.Count == 0)
                TryConnectToGameplaySystems();
        }

        private void TryConnectToGameplaySystems()
        {
            if (subscribedHud == null && CityHudController.Instance != null)
            {
                subscribedHud = CityHudController.Instance;
                subscribedHud.AiReportRequested += RunAnalysisAndShowReport;
            }

            if (subscribedInvestmentController == null && InvestmentTowerUIController.Instance != null)
            {
                subscribedInvestmentController = InvestmentTowerUIController.Instance;
                subscribedInvestmentController.PurchaseConfirmed += HandleInvestmentPurchaseConfirmed;
            }

            if (subscribedPopups.Count == 0)
            {
                TransactionPopupView[] popups = FindObjectsByType<TransactionPopupView>(FindObjectsSortMode.None);
                foreach (TransactionPopupView popup in popups)
                {
                    popup.Confirmed += HandleTransactionConfirmed;
                    subscribedPopups.Add(popup);
                }
            }
        }

        // Called exactly when StartFlow hands off to a brand-new game (see StartFlowController.
        // LoadGameScene) - never on an ordinary mid-game scene change, so re-entering OGscene (or
        // any other scene transition within the same game) never loses already-recorded
        // decisions. player_id is intentionally left untouched (it identifies the device/player,
        // not the session).
        public void StartNewSession()
        {
            int cleared = decisions.Count;
            decisions.Clear();
            Debug.Log($"DhaheenGameTracker: new session started - cleared {cleared} decision(s) from the previous session.");
        }

        // ------------------------------------------------------------------------ recording (observe only, never mutate money)

        private void HandleTransactionConfirmed(TransactionPopupView.Kind kind, float amount, float responseTimeSeconds)
        {
            switch (kind)
            {
                case TransactionPopupView.Kind.Savings:
                    RecordDecision(new DhaheenDecision
                    {
                        scenario_id = NewScenarioId(),
                        scenario_type = "income",
                        choice = "save",
                        amount = amount,
                        response_time_seconds = responseTimeSeconds
                    });
                    break;

                case TransactionPopupView.Kind.Mall:
                    RecordDecision(new DhaheenDecision
                    {
                        scenario_id = NewScenarioId(),
                        scenario_type = "shopping",
                        choice = "spend",
                        amount = amount,
                        response_time_seconds = responseTimeSeconds,
                        // The shop has no need/want/luxury categorization - the player only
                        // chooses an amount. "want" is a fixed, backend-only value so item_type
                        // is never empty for a "shopping" decision; it's never surfaced in the UI.
                        item_type = ShoppingItemType
                    });
                    break;

                case TransactionPopupView.Kind.Charity:
                    // The API has no "charity" scenario_type yet. PlayerDataManager.Data.
                    // donatedAmount already tracks this locally for any UI that needs it -
                    // intentionally not sent here rather than inventing an unsupported value.
                    break;
            }
        }

        private void HandleInvestmentPurchaseConfirmed(InvestmentCompany company, int quantity, float responseTimeSeconds)
        {
            RecordDecision(new DhaheenDecision
            {
                scenario_id = NewScenarioId(),
                scenario_type = "investment",
                choice = "invest",
                amount = company.price * quantity,
                response_time_seconds = responseTimeSeconds,
                risk_level = MapRiskLevel(company.riskLevelArabic)
            });
        }

        private void RecordDecision(DhaheenDecision decision)
        {
            decisions.Add(decision);
            Debug.Log($"DhaheenGameTracker: recorded {decision.scenario_type}/{decision.choice} amount={decision.amount:0.##}");
        }

        private static string MapRiskLevel(string riskLevelArabic)
        {
            if (riskLevelArabic == InvestmentSampleData.RiskHigh) return "high";
            if (riskLevelArabic == InvestmentSampleData.RiskMedium) return "medium";
            return "low";
        }

        private static string NewScenarioId() => "S-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        // ------------------------------------------------------------------------ AI Report button flow

        public void RunAnalysisAndShowReport()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float walletForLog = PlayerDataManager.GetOrCreate().Data.currentAvailableMoney;
            Debug.Log($"DhaheenGameTracker: AI Report requested - decisions={decisions.Count}, wallet={walletForLog:0.##}");
#endif

            DhaheenResultsUI resultsUI = subscribedHud != null
                ? subscribedHud.GetComponentInChildren<DhaheenResultsUI>(true)
                : null;
            if (resultsUI == null)
            {
                Debug.LogError("DhaheenGameTracker: no DhaheenResultsUI found under the City HUD's AI Report panel " +
                    "(subscribedHud=" + (subscribedHud != null ? subscribedHud.name : "null") + ").");
                return;
            }

            if (decisions.Count == 0)
            {
                resultsUI.ShowMessage("اتخذ بعض القرارات المالية أولًا حتى نتمكن من تحليل سلوكك.");
                return;
            }

            DhaheenApiClient client = DhaheenApiClient.GetOrCreate();
            if (client.IsAnalyzing) return;

            SetReportButtonInteractable(false);
            resultsUI.ShowLoading();

            DhaheenGameSession session = BuildSessionForSubmission();
            client.AnalyzeGameSession(
                session,
                "Real Gameplay (AI Report Button)",
                result =>
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log("DhaheenGameTracker: success callback reached.");
#endif
                    SetReportButtonInteractable(true);
                    resultsUI.DisplayResult(result);
                },
                (kind, rawError) =>
                {
                    SetReportButtonInteractable(true);
                    resultsUI.ShowMessage(TranslateErrorKind(kind));
                });
        }

        // Everything is read fresh from PlayerDataManager right before sending (including
        // ending_balance, per the integration requirements) rather than cached earlier in the
        // session, so it can never drift out of sync with the real wallet. starting_balance comes
        // straight from data.monthlyMoney - the amount the player actually chose on
        // MonthlyMoneyScreenController (validated there to the 50-1000 range) - never a literal.
        private DhaheenGameSession BuildSessionForSubmission()
        {
            PlayerSetupData data = PlayerDataManager.GetOrCreate().Data;

            // Decision cleanup/validation (item_type, risk_level, emergency_success) is now
            // centralized in DhaheenRequestSerializer, run from DhaheenApiClient.
            // SendAnalysisRequest for every outgoing session regardless of source - not
            // duplicated here, so there's exactly one place to keep correct.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"DhaheenGameTracker: selected monthly amount (StartFlow) = {data.monthlyMoney:0.##}");
#endif

            DhaheenGameSession session = new DhaheenGameSession
            {
                player_id = GetOrCreatePlayerId(),
                age = fallbackAge,
                language = "ar",
                goal = new DhaheenGoal
                {
                    // The player's goal is free text (PlayerSetupData.financialGoal) with no
                    // structured category yet - "saving" is the nearest supported default.
                    type = "saving",
                    target_amount = data.goalTargetAmount
                },
                starting_balance = data.monthlyMoney,
                ending_balance = data.currentAvailableMoney,
                decisions = decisions
            };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"DhaheenGameTracker: starting_balance={session.starting_balance:0.##}, ending_balance={session.ending_balance:0.##}");
            for (int i = 0; i < decisions.Count; i++)
            {
                DhaheenDecision d = decisions[i];
                Debug.Log($"DhaheenGameTracker: decision[{i}] scenario_type={d.scenario_type}, choice={d.choice}, item_type='{d.item_type}', amount={d.amount:0.##}");
            }
#endif

            return session;
        }

        private static string GetOrCreatePlayerId()
        {
            string existing = PlayerPrefs.GetString(PlayerIdPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(existing)) return existing;

            string newId = "player_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            PlayerPrefs.SetString(PlayerIdPrefsKey, newId);
            PlayerPrefs.Save();
            return newId;
        }

        private void SetReportButtonInteractable(bool interactable)
        {
            if (subscribedHud != null && subscribedHud.AiReportButton != null)
                subscribedHud.AiReportButton.interactable = interactable;
        }

        private static string TranslateErrorKind(DhaheenErrorKind kind)
        {
            switch (kind)
            {
                case DhaheenErrorKind.Timeout:
                    return "يستغرق المحلل وقتًا أطول من المعتاد. حاول مرة أخرى بعد لحظات.";
                case DhaheenErrorKind.Network:
                    return "تعذر الاتصال بالمحلل المالي. تحقق من اتصال الإنترنت وحاول مرة أخرى.";
                default:
                    return "تعذر قراءة نتيجة التحليل. حاول مرة أخرى.";
            }
        }
    }
}
