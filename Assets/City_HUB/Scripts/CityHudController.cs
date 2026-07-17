using System;
using System.Globalization;
using InvestmentTowerUI;
using StartFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CityHud
{
    // Permanent top HUD shown while the player is exploring the city. Lives as a sibling of
    // AI_Alinma_Investment_Tower_UI under BuildingInteraction_Canvas (see CityHudBuilder) and,
    // like that controller, must stay always-active so Awake()/Instance wiring runs on scene
    // load - only the AI Report panel toggles visibility, never this GameObject itself.
    public class CityHudController : MonoBehaviour
    {
        public static CityHudController Instance { get; private set; }

        [Header("Center Stats - do not hardcode, update via the public Set* methods")]
        [SerializeField] private TMP_Text remainingMoneyText;
        [SerializeField] private TMP_Text goalProgressText;
        [SerializeField] private TMP_Text playerNameText;

        [Header("Buttons")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button aiReportButton;
        [SerializeField] private Button exitButton;

        [Header("AI Report Panel")]
        [SerializeField] private GameObject aiReportPanel;
        [SerializeField] private Button aiReportCloseButton;

        // Exposes the button for callers that need to disable it while a request is running
        // (see Dhaheen.DhaheenGameTracker) without this controller needing to know why.
        public Button AiReportButton => aiReportButton;

        // Fired every time the AI Report button is clicked, after the panel is opened. Lets
        // external systems (see Dhaheen.DhaheenGameTracker) drive what happens next without this
        // controller referencing Dhaheen types at all.
        public event Action AiReportRequested;

        private bool bound;

        private void Awake()
        {
            // Duplicate-protection, consistent with every other persistent manager in the
            // project (PlayerDataManager, DhaheenGameTracker, DhaheenApiClient,
            // BuildingInteractionManager). City_HUD is scene-scoped (not DontDestroyOnLoad) by
            // design - it belongs to OGscene specifically - this guard only matters if something
            // ever ends up with two City_HUD instances loaded at once.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BindOnce();
            RefreshFromPlayerData();
        }

        private void BindOnce()
        {
            if (bound) return;
            bound = true;

            if (aiReportButton != null)
            {
                aiReportButton.onClick.RemoveAllListeners();
                aiReportButton.onClick.AddListener(HandleAiReportButtonClicked);
            }
            if (aiReportCloseButton != null)
            {
                aiReportCloseButton.onClick.RemoveAllListeners();
                aiReportCloseButton.onClick.AddListener(CloseAiReportPanel);
            }
            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(ExitGame);
            }
            // settingsButton is intentionally left unwired - no settings panel exists yet in
            // this project; the reference is exposed above for whoever builds that panel next.

            if (aiReportPanel != null) aiReportPanel.SetActive(false);
        }

        // ------------------------------------------------------------------------ public data API

        public void SetPlayerName(string playerName)
        {
            if (playerNameText == null) return;
            ArabicTextUtility.Apply(playerNameText, playerName);
        }

        public void SetRemainingMoney(float amount)
        {
            if (remainingMoneyText == null) return;
            remainingMoneyText.text = amount.ToString("#,0", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");
        }

        // progress01 is clamped to [0, 1] and rendered as a whole-number percentage.
        public void SetGoalProgress(float progress01)
        {
            if (goalProgressText == null) return;
            int percent = Mathf.RoundToInt(Mathf.Clamp01(progress01) * 100f);
            goalProgressText.text = percent.ToString(CultureInfo.InvariantCulture) + "%";
        }

        // Convenience default binding to the existing player-data singleton. Safe to call
        // whenever the underlying data changes (e.g. after a purchase) - every other script can
        // instead call the Set* methods above directly if it already has the fresh values.
        public void RefreshFromPlayerData()
        {
            PlayerDataManager manager = PlayerDataManager.Instance;
            PlayerSetupData data = manager != null ? manager.Data : null;
            if (data == null) return;

            SetPlayerName(data.playerName);
            SetRemainingMoney(data.currentAvailableMoney);
            float progress = data.goalTargetAmount > 0f ? data.savedAmount / data.goalTargetAmount : 0f;
            SetGoalProgress(progress);
        }

        // ------------------------------------------------------------------------ buttons

        public void OpenAiReportPanel()
        {
            if (aiReportPanel != null) aiReportPanel.SetActive(true);
        }

        private void HandleAiReportButtonClicked()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("CityHudController: AI Report button clicked.");
#endif
            OpenAiReportPanel();
            AiReportRequested?.Invoke();
        }

        public void CloseAiReportPanel()
        {
            if (aiReportPanel != null) aiReportPanel.SetActive(false);
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
