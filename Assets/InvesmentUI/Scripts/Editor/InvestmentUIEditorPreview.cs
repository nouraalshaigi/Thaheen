using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InvestmentTowerUI.Editor
{
    // Real-scene-object Edit Mode preview for AI_Alinma_Investment_Tower_UI. Every command below
    // only ever activates/deactivates the SAME real, already-saved GameObjects that exist in
    // OGscene.unity - it never instantiates clones of static UI, never uses
    // HideFlags.DontSave, never rebuilds a panel, and never touches any RectTransform, position,
    // size, spacing, font, or icon assignment. Manual layout edits made after switching to any of
    // these previews save normally with Ctrl+S and persist through Play Mode exactly like any
    // other scene edit - this tool has no save-time or load-time hook that could revert them.
    //
    // The one narrow exception is "Show Holdings Portfolio": since the holdings list is
    // genuinely empty until a real purchase happens, it optionally instantiates ONE real
    // (not DontSave) sample card - using the real saved PortfolioHoldingCard prefab, in the real
    // saved HoldingsList - so that layout can be seen/arranged with representative content. That
    // sample card is clearly named, is never written into InvestmentSampleData (no wallet/
    // transaction/portfolio save data is touched), and is removed automatically the moment Play
    // Mode starts or a different preview command runs.
    [InitializeOnLoad]
    internal static class InvestmentUIEditorPreview
    {
        private const string MenuRoot = "Tools/Investment Tower/Edit Preview/";
        private const string SamplePreviewMarker = " (Sample Preview - Do Not Save)";

        private static readonly string[] TopLevelPanelFields =
        {
            "tutorialStep1Panel", "tutorialStep2Panel", "companyListPanel", "companyDetailsPanel",
            "purchaseConfirmationPanel", "portfolioPanel", "sellSharesPanel",
        };

        private static readonly string[] PortfolioTabFields =
        {
            "portfolioTabContent", "historyTabContent", "summaryTabContent",
        };

        static InvestmentUIEditorPreview()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // The only automatic behavior this tool performs: the temporary sample holding card (see
        // ShowHoldingsPortfolio) must never exist once the game actually runs. Nothing else about
        // panel/overlay active-state is touched or reverted here - whatever the Investment Tower
        // looks like in the Editor is left exactly as-is; InvestmentTowerUIController.Awake()
        // already resets which panel is shown to match real gameplay flow.
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                RemoveSampleCards(FindController());
        }

        // Shared validate check for every "Show X" command below - grays them all out while
        // Play Mode is running so a sample preview card can never be created during actual
        // gameplay (see EnsureSampleCard), closing the only other way "both cards visible at
        // once" could happen besides the automatic Play Mode cleanup.
        [MenuItem(MenuRoot + "Show Tutorial 1", true)]
        [MenuItem(MenuRoot + "Show Tutorial 2", true)]
        [MenuItem(MenuRoot + "Show Company List", true)]
        [MenuItem(MenuRoot + "Show Company Details", true)]
        [MenuItem(MenuRoot + "Show Company Details With Quantity", true)]
        [MenuItem(MenuRoot + "Show Purchase Confirmation", true)]
        [MenuItem(MenuRoot + "Show Sell Shares", true)]
        [MenuItem(MenuRoot + "Show Empty Portfolio", true)]
        [MenuItem(MenuRoot + "Show Holdings Portfolio", true)]
        [MenuItem(MenuRoot + "Show History", true)]
        [MenuItem(MenuRoot + "Show Summary", true)]
        private static bool ValidateNotPlaying() => !EditorApplication.isPlaying;

        [MenuItem(MenuRoot + "Show Tutorial 1")]
        private static void ShowTutorial1() => ShowPanel("tutorialStep1Panel");

        [MenuItem(MenuRoot + "Show Tutorial 2")]
        private static void ShowTutorial2() => ShowPanel("tutorialStep2Panel");

        [MenuItem(MenuRoot + "Show Company List")]
        private static void ShowCompanyList() => ShowPanel("companyListPanel");

        [MenuItem(MenuRoot + "Show Company Details")]
        private static void ShowCompanyDetails() => ShowPanel("companyDetailsPanel");

        [MenuItem(MenuRoot + "Show Purchase Confirmation")]
        private static void ShowPurchaseConfirmation() => ShowPanel("purchaseConfirmationPanel");

        // CompanyDetailsPanel with QuantitySelector/QuickAmountRow/SummaryRow revealed at a
        // representative quantity, using the REAL saved fields/rows (no clones, no
        // HideFlags.DontSave) - QuickAmountRow/SummaryRow are always-active with reserved
        // layout space (see BuildCompanyDetails), so this only sets safe local sample text and
        // flips their CanvasGroup to visible, exactly matching what RefreshQuantitySummary does
        // at runtime for a non-zero quantity. Never writes to InvestmentSampleData.
        [MenuItem(MenuRoot + "Show Company Details With Quantity")]
        private static void ShowCompanyDetailsWithQuantity()
        {
            InvestmentTowerUIController controller = ShowPanel("companyDetailsPanel");
            if (controller == null) return;

            var so = new SerializedObject(controller);

            ApplyArabicText(so, "detailsNameText", "الإنماء");
            ApplyArabicText(so, "detailsSectorText", "الخدمات المالية");
            ApplyArabicText(so, "detailsLogoText", "ال");
            ApplyArabicText(so, "detailsRiskText", "متوسطة");
            SetPlainText(so, "detailsChangeText", "+1.20%");
            ApplyArabicText(so, "detailsPriceText", "28.40 ر");
            ApplyArabicText(so, "detailsDescriptionText",
                "بنك الإنماء يقدم خدمات مصرفية إسلامية متكاملة للأفراد والشركات في المملكة العربية السعودية.");

            // The QuantitySelector prefab instance has its own private valueText field - resolve
            // it through its own SerializedObject rather than the controller's.
            QuantitySelectorView selector = so.FindProperty("detailsQuantitySelector")?.objectReferenceValue as QuantitySelectorView;
            if (selector != null)
            {
                var selectorSo = new SerializedObject(selector);
                TMP_Text selectorValueText = selectorSo.FindProperty("valueText")?.objectReferenceValue as TMP_Text;
                if (selectorValueText != null) selectorValueText.text = "5 " + ArabicTextUtility.Format("سهم");
            }

            SetPlainText(so, "detailsPricePerShareText", "28.40 " + ArabicTextUtility.Format("ريال"));
            SetPlainText(so, "detailsShareCountText", "5");

            CanvasGroup quickGroup = so.FindProperty("detailsQuickAmountGroup")?.objectReferenceValue as CanvasGroup;
            CanvasGroup summaryGroup = so.FindProperty("detailsSummaryGroup")?.objectReferenceValue as CanvasGroup;
            InvestmentTowerUIBuilder.SetVisibleGroup(quickGroup);
            InvestmentTowerUIBuilder.SetVisibleGroup(summaryGroup);
        }

        private static void ApplyArabicText(SerializedObject so, string field, string value)
        {
            TMP_Text target = so.FindProperty(field)?.objectReferenceValue as TMP_Text;
            ArabicTextUtility.Apply(target, value);
        }

        private static void SetPlainText(SerializedObject so, string field, string value)
        {
            TMP_Text target = so.FindProperty(field)?.objectReferenceValue as TMP_Text;
            if (target != null) target.text = value;
        }

        [MenuItem(MenuRoot + "Show Sell Shares")]
        private static void ShowSellShares() => ShowPanel("sellSharesPanel");

        [MenuItem(MenuRoot + "Show Empty Portfolio")]
        private static void ShowEmptyPortfolio()
        {
            InvestmentTowerUIController controller = ShowPanel("portfolioPanel");
            if (controller == null) return;
            SetPortfolioTab(controller, "portfolioTabContent");
            SetPortfolioHoldingsSubState(controller, showEmpty: true);
        }

        [MenuItem(MenuRoot + "Show Holdings Portfolio")]
        private static void ShowHoldingsPortfolio()
        {
            InvestmentTowerUIController controller = ShowPanel("portfolioPanel");
            if (controller == null) return;
            SetPortfolioTab(controller, "portfolioTabContent");
            SetPortfolioHoldingsSubState(controller, showEmpty: false);
            EnsureSampleCard(controller);
        }

        [MenuItem(MenuRoot + "Show History")]
        private static void ShowHistory()
        {
            InvestmentTowerUIController controller = ShowPanel("portfolioPanel");
            if (controller == null) return;
            SetPortfolioTab(controller, "historyTabContent");
        }

        [MenuItem(MenuRoot + "Show Summary")]
        private static void ShowSummary()
        {
            InvestmentTowerUIController controller = ShowPanel("portfolioPanel");
            if (controller == null) return;
            SetPortfolioTab(controller, "summaryTabContent");
        }

        // ------------------------------------------------------------------------ shared

        // Activates root + DimOverlay, activates exactly one of the 7 real top-level panels (by
        // its real controller field name) and deactivates the rest. Never touches a
        // RectTransform, never instantiates or destroys anything except a leftover sample card
        // from a previous "Show Holdings Portfolio" call, never rebuilds. Returns the controller
        // so the Portfolio-specific commands above can do their extra (also real-object-only)
        // step.
        private static InvestmentTowerUIController ShowPanel(string activePanelField)
        {
            InvestmentTowerUIController controller = FindController();
            if (controller == null)
            {
                Debug.LogWarning("InvestmentUIEditorPreview: AI_Alinma_Investment_Tower_UI/InvestmentTowerUIController not found in the open scene.");
                return null;
            }

            var so = new SerializedObject(controller);

            GameObject root = GetGameObject(so, "root");
            if (root != null) root.SetActive(true);

            GameObject overlay = GetGameObject(so, "overlayCloseButton");
            if (overlay != null) overlay.SetActive(true);

            foreach (string field in TopLevelPanelFields)
                SetActiveIfPresent(so, field, field == activePanelField);

            RemoveSampleCards(controller);
            return controller;
        }

        private static void SetPortfolioTab(InvestmentTowerUIController controller, string activeTabField)
        {
            var so = new SerializedObject(controller);
            foreach (string field in PortfolioTabFields)
                SetActiveIfPresent(so, field, field == activeTabField);
        }

        private static void SetPortfolioHoldingsSubState(InvestmentTowerUIController controller, bool showEmpty)
        {
            var so = new SerializedObject(controller);
            SetActiveIfPresent(so, "portfolioEmptyState", showEmpty);
            SetActiveIfPresent(so, "portfolioHoldingsState", !showEmpty);
        }

        // Instantiates exactly one real (non-DontSave) instance of the real saved
        // PortfolioHoldingCard prefab into the real saved HoldingsList, populated with plain
        // local sample data that is never written to InvestmentSampleData.Holdings/Companies -
        // purely so the real layout can be seen/arranged with representative content. A no-op if
        // a sample card is already present.
        private static void EnsureSampleCard(InvestmentTowerUIController controller)
        {
            var so = new SerializedObject(controller);
            Transform content = so.FindProperty("portfolioHoldingsContent")?.objectReferenceValue as Transform;
            GameObject prefab = ToGameObject(so.FindProperty("portfolioHoldingCardPrefab")?.objectReferenceValue);
            if (content == null || prefab == null) return;

            for (int i = 0; i < content.childCount; i++)
                if (content.GetChild(i).name.EndsWith(SamplePreviewMarker)) return; // already there

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, content);
            instance.name = prefab.name + SamplePreviewMarker;

            PortfolioHoldingCardView view = instance.GetComponent<PortfolioHoldingCardView>();
            if (view == null) return;

            var sampleCompany = new InvestmentCompany
            {
                id = "__edit_preview_sample",
                nameArabic = "الإنماء",
                sectorArabic = "الخدمات المالية",
                logoLetters = "ال",
                logoColor = new Color32(0x2E, 0x7D, 0x5B, 0xFF),
                price = 28.40f,
            };
            view.SetHolding(new InvestmentHolding { company = sampleCompany, shares = 5, averagePrice = 26.00f });
        }

        private static void RemoveSampleCards(InvestmentTowerUIController controller)
        {
            if (controller == null) return;
            foreach (Transform t in controller.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.EndsWith(SamplePreviewMarker))
                    Object.DestroyImmediate(t.gameObject);
        }

        private static InvestmentTowerUIController FindController()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return null;

            foreach (GameObject rootGO in scene.GetRootGameObjects())
            {
                InvestmentTowerUIController found = rootGO.GetComponentInChildren<InvestmentTowerUIController>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetActiveIfPresent(SerializedObject so, string field, bool active)
        {
            GameObject go = GetGameObject(so, field);
            if (go != null) go.SetActive(active);
        }

        private static GameObject GetGameObject(SerializedObject so, string field)
        {
            SerializedProperty prop = so.FindProperty(field);
            return ToGameObject(prop?.objectReferenceValue);
        }

        // Resolves a serialized reference to its owning GameObject whether the field is typed as
        // GameObject directly or as one of its components (e.g. overlayCloseButton is a Button
        // that lives directly on DimOverlay; portfolioHoldingCardPrefab is typed as
        // PortfolioHoldingCardView).
        private static GameObject ToGameObject(Object obj)
        {
            if (obj is GameObject go) return go;
            if (obj is Component component) return component.gameObject;
            return null;
        }
    }
}
