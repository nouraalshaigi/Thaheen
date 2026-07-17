using System.Collections.Generic;
using InvestmentTowerUI.MarketData;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InvestmentTowerUI.Editor
{
    // Builds the entire Investment Tower UI (9 panels + 9 reusable prefabs) from code, following
    // the exact same safe patterns already established in this project for BuildingPopup.prefab:
    // - never hand-authors YAML, always real Unity API calls;
    // - the reusable prefabs are real .prefab assets under Assets/InvesmentUI/Prefabs;
    // - the panel hierarchy is built as a real GameObject tree, added as a child of the existing
    //   BuildingInteraction_Canvas inside the ALREADY-LOADED OGscene (never a second Scene
    //   object - see the scene-save note below);
    // - manual-only: there is no [InitializeOnLoad]/delayCall auto-build. Building or rebuilding
    //   only ever happens when RebuildManually() (or one of the narrower, panel-scoped
    //   maintenance entries in InvestmentTowerUIBuilder.Maintenance.cs) is explicitly invoked
    //   from the Tools menu - never automatically after a compile, scene load, save, or Play
    //   Mode, so manual Inspector/Scene edits are never at risk of being silently overwritten.
    //
    // Split into partial-class files by area (Tutorial / Trading / Portfolio) purely to keep each
    // file a manageable size - all share the helpers and Refs classes defined here.
    internal static partial class InvestmentTowerUIBuilder
    {
        private const string ScenePath = "Assets/OGscene.unity";
        private const string CanvasObjectName = "BuildingInteraction_Canvas";
        private const string RootObjectName = "AI_Alinma_Investment_Tower_UI";
        private const string OverlayObjectName = "DimOverlay";
        private const string PrefabFolder = "Assets/InvesmentUI/Prefabs";
        private const string ArtRoot = "Assets/InvesmentUI";
        private const string RegularFontPath = "Assets/InvesmentUI/Fonts/TMP/Cairo-Regular SDF.asset";
        private const string BoldFontPath = "Assets/InvesmentUI/Fonts/TMP/Cairo-Bold SDF.asset";

        [MenuItem("Tools/Investment Tower/Rebuild UI (manual only)")]
        public static void RebuildManually() => Build(forceRebuildPrefabs: true);

        private static void Build(bool forceRebuildPrefabs)
        {
            // Called directly (not just relied upon via its own [InitializeOnLoad]) so font
            // generation is guaranteed to have already run before this checks for the result -
            // two independent delayCall registrations firing on the same domain reload are not
            // guaranteed to run in a particular order relative to each other.
            InvestmentCairoFontSetup.EnsureFontsGenerated();

            TMP_FontAsset regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (regularFont == null || boldFont == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: Cairo TMP font assets not found under Assets/InvesmentUI/Fonts/TMP - " +
                    "run Tools/Investment Tower/Regenerate Cairo Font Assets first.");
                return;
            }
            var art = new Art(regularFont, boldFont);
            if (!art.LoadAll()) return;

            Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"InvestmentTowerUIBuilder: '{ScenePath}' is not open - open it in the Editor first.");
                return;
            }

            GameObject canvasGO = FindInScene(scene, CanvasObjectName);
            if (canvasGO == null)
            {
                Debug.LogError($"InvestmentTowerUIBuilder: '{CanvasObjectName}' not found in '{ScenePath}' - not creating a new canvas (see task constraints).");
                return;
            }

            Transform existingRoot = canvasGO.transform.Find(RootObjectName);
            if (existingRoot != null)
                Object.DestroyImmediate(existingRoot.gameObject);

            if (!EnsurePrefabs(art, forceRebuildPrefabs, out Prefabs prefabs))
                return;

            RectTransform root = CreateUIObject(RootObjectName, canvasGO.transform);
            StretchFull(root);
            CanvasGroup rootGroup = root.gameObject.AddComponent<CanvasGroup>();
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;

            InvestmentTowerUIController controller = root.gameObject.AddComponent<InvestmentTowerUIController>();
            root.gameObject.AddComponent<SaudiMarketService>();

            RectTransform overlay = CreateUIObject(OverlayObjectName, root);
            StretchFull(overlay);
            Image overlayImg = overlay.gameObject.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.55f);
            Button overlayBtn = overlay.gameObject.AddComponent<Button>();
            overlayBtn.transition = Selectable.Transition.None;

            var refs = new ControllerRefs { root = root.gameObject, overlayButton = overlayBtn };

            BuildTutorialStep1(overlay, art, prefabs, refs);
            BuildTutorialStep2(overlay, art, prefabs, refs);
            BuildCompanyList(overlay, art, prefabs, refs);
            BuildCompanyDetails(overlay, art, prefabs, refs);
            BuildPurchaseConfirmation(overlay, art, prefabs, refs);
            BuildPortfolio(overlay, art, prefabs, refs);
            BuildSellShares(overlay, art, prefabs, refs);

            refs.tutorialStep1Panel.SetActive(true);
            refs.tutorialStep2Panel.SetActive(false);
            refs.companyListPanel.SetActive(false);
            refs.companyDetailsPanel.SetActive(false);
            refs.purchaseConfirmationPanel.SetActive(false);
            refs.portfolioPanel.SetActive(false);
            refs.sellSharesPanel.SetActive(false);

            WireController(controller, refs);

            // root itself must stay active - InvestmentTowerUIController.Awake() (and the
            // Instance singleton assignment it makes, which BuildingPopupController depends on to
            // route Investment Tower clicks here instead of the generic popup) only ever runs
            // once, the first time this GameObject becomes active. Saving it as inactive would
            // silently and permanently break that on every future scene/Play load. DimOverlay is
            // the real "is it open" layer, closed by default - see
            // InvestmentTowerUIController.Awake/Open/Close.
            overlay.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("InvestmentTowerUIBuilder: AI_Alinma_Investment_Tower_UI built and wired under BuildingInteraction_Canvas.");
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject rootGO in scene.GetRootGameObjects())
            {
                GameObject found = FindInHierarchy(rootGO.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindInHierarchy(Transform t, string name)
        {
            if (t.name == name) return t.gameObject;
            for (int i = 0; i < t.childCount; i++)
            {
                GameObject found = FindInHierarchy(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------------ shared art/handles

        internal class Art
        {
            public readonly TMP_FontAsset Regular;
            public readonly TMP_FontAsset Bold;

            public Sprite FirstPage, SeconedPage, ThirdPage, ForthPage, FifthPage, Sixthpage, SevenPage;
            public Sprite CloseButton, BackButton, GBtn1, PlusButton, MinuesButton, PlusRedButton;
            public Sprite ButtonForCompanys, ButtonForCompanys2, ForAllInvesmentPages, InvestNowButton, PurchaseBox, SellContainer, ExtraPlainForText;
            public Sprite BuyBadge, SellBadge, TrendUp, TrendDown, Riyal, GreenContainer, RedContainer;
            public Sprite IconGeneric, InvestIcon, HistoryIcon, ArrowIcon;
            // Exact provided per-company CompanyListPanel button assets - see Assets/InvesmentUI/CompanyListPanel.
            public Sprite AlinmaAccent, AramcoAccent, StcAccent;

            public Art(TMP_FontAsset regular, TMP_FontAsset bold) { Regular = regular; Bold = bold; }

            public bool LoadAll()
            {
                FirstPage = Load($"{ArtRoot}/Containers_use/FirstPage.png");
                SeconedPage = Load($"{ArtRoot}/Containers_use/SeconedPage.png");
                ThirdPage = Load($"{ArtRoot}/Containers_use/ThirdPage.png");
                ForthPage = Load($"{ArtRoot}/Containers_use/ForthPage.png");
                FifthPage = Load($"{ArtRoot}/Containers_use/FifthPage.png");
                Sixthpage = Load($"{ArtRoot}/Containers_use/Sixthpage.png");
                SevenPage = Load($"{ArtRoot}/Containers_use/SevenPage.png");

                CloseButton = Load($"{ArtRoot}/Button/Close_Button.png");
                BackButton = Load($"{ArtRoot}/Button/Back_Button.png");
                GBtn1 = Load($"{ArtRoot}/Button/GBtn1.png");
                PlusButton = Load($"{ArtRoot}/Button/Plus_Button.png");
                MinuesButton = Load($"{ArtRoot}/Button/Minues_Button.png");
                PlusRedButton = Load($"{ArtRoot}/Button/Plus_Red_Button.png");
                ButtonForCompanys = Load($"{ArtRoot}/Button/Button_for_companys.png");
                ButtonForCompanys2 = Load($"{ArtRoot}/Button/Button_for_companys2.png");
                ForAllInvesmentPages = Load($"{ArtRoot}/Button/ForAllInvesmentPages.png");
                InvestNowButton = Load($"{ArtRoot}/Button/InvestNowButton.png");
                PurchaseBox = Load($"{ArtRoot}/Containers_use/Purchase_Box.png");
                SellContainer = Load($"{ArtRoot}/Containers_use/Sell_Container.png");
                ExtraPlainForText = Load($"{ArtRoot}/Containers_use/ExtraPlainForText.png");

                BuyBadge = Load($"{ArtRoot}/Icons/Buy.png");
                SellBadge = Load($"{ArtRoot}/Icons/Sell.png");
                TrendUp = Load($"{ArtRoot}/Icons/Up.png");
                TrendDown = Load($"{ArtRoot}/Icons/Down.png");
                Riyal = Load($"{ArtRoot}/Icons/ريال (2).png");
                GreenContainer = Load($"{ArtRoot}/Button/GreenContainer.png");
                RedContainer = Load($"{ArtRoot}/Button/RedContainer.png");
                IconGeneric = Load($"{ArtRoot}/Icons/Icon.png");
                InvestIcon = Load($"{ArtRoot}/Icons/InvestIcon.png");
                HistoryIcon = Load($"{ArtRoot}/Icons/History_Icon.png");
                ArrowIcon = Load($"{ArtRoot}/Icons/Arrow_Icon.png");

                AlinmaAccent = Load($"{ArtRoot}/CompanyListPanel/Inma_Button.png");
                AramcoAccent = Load($"{ArtRoot}/CompanyListPanel/Aramco_Button.png");
                StcAccent = Load($"{ArtRoot}/CompanyListPanel/STC_Button.png");

                var required = new (string, Sprite)[]
                {
                    ("FirstPage", FirstPage), ("SeconedPage", SeconedPage), ("ThirdPage", ThirdPage),
                    ("ForthPage", ForthPage), ("FifthPage", FifthPage), ("Sixthpage", Sixthpage), ("SevenPage", SevenPage),
                    ("CloseButton", CloseButton), ("BackButton", BackButton), ("GBtn1", GBtn1),
                    ("PlusButton", PlusButton), ("MinuesButton", MinuesButton),
                    ("ButtonForCompanys", ButtonForCompanys), ("InvestNowButton", InvestNowButton),
                    ("PurchaseBox", PurchaseBox), ("Riyal", Riyal),
                    ("AlinmaAccent", AlinmaAccent), ("AramcoAccent", AramcoAccent), ("StcAccent", StcAccent),
                };
                foreach ((string name, Sprite sprite) in required)
                {
                    if (sprite == null)
                    {
                        Debug.LogError($"InvestmentTowerUIBuilder: required sprite '{name}' failed to load - check Assets/InvesmentUI import settings.");
                        return false;
                    }
                }
                return true;
            }

            private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        internal class Prefabs
        {
            public GameObject PrimaryButton, SecondaryButton, CompanyCard, DetailsHeader, QuantitySelector,
                PortfolioHoldingCard, TransactionRow, SummaryRow, PopupHeader;
        }

        // All serialized-field targets InvestmentTowerUIController expects, collected while
        // building so WireController can assign every one of them in a single pass at the end.
        internal class ControllerRefs
        {
            public GameObject root;
            public Button overlayButton;
            public List<Button> closeButtons = new List<Button>();

            public GameObject tutorialStep1Panel;
            public Button tutorialStep1NextButton;
            public GameObject tutorialStep2Panel;
            public Button tutorialStep2StartButton;
            public InvestmentPopupHeaderView tutorialStep1Header;
            public InvestmentPopupHeaderView tutorialStep2Header;

            public GameObject companyListPanel;
            public InvestmentPopupHeaderView companyListHeader;
            public TMP_Text availableBalanceText;
            public Transform companyListContent;
            public List<CompanyCardView> companyListCards = new List<CompanyCardView>();
            public Button openPortfolioButton;
            public GameObject openPortfolioButtonRoot;
            public TMP_Text footerNoteText;

            public GameObject companyDetailsPanel;
            public InvestmentPopupHeaderView companyDetailsHeader;
            public TMP_Text detailsNameText, detailsSectorText, detailsLogoText, detailsRiskText, detailsChangeText, detailsPriceText, detailsDescriptionText;
            public Image detailsLogoBackground;
            public Button predictUpButton, predictDownButton;
            public Image predictUpBackground, predictDownBackground;
            public QuantitySelectorView detailsQuantitySelector;
            public List<QuickQuantityButtonRef> detailsQuickButtons = new List<QuickQuantityButtonRef>();
            public CanvasGroup detailsQuickAmountGroup;
            public TMP_Text detailsPricePerShareText, detailsShareCountText;
            public GameObject detailsSummaryRow;
            public CanvasGroup detailsSummaryGroup;
            public Button investButton;

            public GameObject purchaseConfirmationPanel;
            public TMP_Text confirmNameText, confirmSectorText, confirmLogoText, confirmPricePerShareText, confirmShareCountText, confirmTotalCostText, confirmRemainingBalanceText;
            public Image confirmLogoBackground;
            public Button confirmPurchaseButton, confirmBackButton;

            public GameObject portfolioPanel;
            public Button portfolioTabButton, historyTabButton, summaryTabButton;
            public GameObject portfolioTabContent, historyTabContent, summaryTabContent;
            public GameObject portfolioEmptyState;
            public Button portfolioEmptyStateButton;
            public GameObject portfolioHoldingsState;
            public TMP_Text portfolioValueText, portfolioRemainingBalanceText, portfolioProfitLossText;
            public Transform portfolioHoldingsContent;
            public PortfolioHoldingCardView portfolioHoldingCardPrefab;
            public GameObject historyScrollView;
            public Transform historyContent;
            public TransactionRowView transactionRowPrefab;
            public GameObject historyEmptyState;
            public TMP_Text historyEmptyTitleText, historyEmptySubtitleText;
            public GameObject summaryScrollView;
            public TMP_Text summaryTotalInvestedText, summaryCurrentValueText, summaryRemainingBalanceText, summaryTotalProfitLossText;
            public SummaryRowView summaryBestInvestmentRow, summaryWorstInvestmentRow, summaryTransactionsCountRow;
            public GameObject summaryEmptyState;
            public TMP_Text summaryEmptyText;

            public GameObject sellSharesPanel;
            public TMP_Text sellNameText, sellOwnedText, sellLogoText, sellCurrentPriceText;
            public Image sellLogoBackground;
            public QuantitySelectorView sellQuantitySelector;
            public List<QuickQuantityButtonRef> sellQuickButtons = new List<QuickQuantityButtonRef>();
            public TMP_Text sellExpectedValueText, sellRemainingSharesText;
            public Button confirmSellButton, sellBackButton;
        }

        private static void WireController(InvestmentTowerUIController controller, ControllerRefs r)
        {
            SerializedObject so = new SerializedObject(controller);
            Set(so, "root", r.root);
            Set(so, "overlayCloseButton", r.overlayButton);
            SetButtonArray(so, "closeButtons", r.closeButtons);

            Set(so, "tutorialStep1Panel", r.tutorialStep1Panel);
            Set(so, "tutorialStep1NextButton", r.tutorialStep1NextButton);
            Set(so, "tutorialStep2Panel", r.tutorialStep2Panel);
            Set(so, "tutorialStep2StartButton", r.tutorialStep2StartButton);
            Set(so, "tutorialStep1Header", r.tutorialStep1Header);
            Set(so, "tutorialStep2Header", r.tutorialStep2Header);

            Set(so, "companyListPanel", r.companyListPanel);
            Set(so, "companyListHeader", r.companyListHeader);
            Set(so, "availableBalanceText", r.availableBalanceText);
            Set(so, "companyListContent", r.companyListContent);
            SetCardArray(so, "companyListCards", r.companyListCards);
            Set(so, "openPortfolioButton", r.openPortfolioButton);
            Set(so, "openPortfolioButtonRoot", r.openPortfolioButtonRoot);
            Set(so, "footerNoteText", r.footerNoteText);

            Set(so, "companyDetailsPanel", r.companyDetailsPanel);
            Set(so, "companyDetailsHeader", r.companyDetailsHeader);
            Set(so, "detailsNameText", r.detailsNameText);
            Set(so, "detailsSectorText", r.detailsSectorText);
            Set(so, "detailsLogoText", r.detailsLogoText);
            Set(so, "detailsLogoBackground", r.detailsLogoBackground);
            Set(so, "detailsRiskText", r.detailsRiskText);
            Set(so, "detailsChangeText", r.detailsChangeText);
            Set(so, "detailsPriceText", r.detailsPriceText);
            Set(so, "detailsDescriptionText", r.detailsDescriptionText);
            Set(so, "predictUpButton", r.predictUpButton);
            Set(so, "predictUpBackground", r.predictUpBackground);
            Set(so, "predictDownButton", r.predictDownButton);
            Set(so, "predictDownBackground", r.predictDownBackground);
            Set(so, "detailsQuantitySelector", r.detailsQuantitySelector);
            SetQuickArray(so, "detailsQuickButtons", r.detailsQuickButtons);
            Set(so, "detailsQuickAmountGroup", r.detailsQuickAmountGroup);
            Set(so, "detailsPricePerShareText", r.detailsPricePerShareText);
            Set(so, "detailsShareCountText", r.detailsShareCountText);
            Set(so, "detailsSummaryRow", r.detailsSummaryRow);
            Set(so, "detailsSummaryGroup", r.detailsSummaryGroup);
            Set(so, "investButton", r.investButton);

            Set(so, "purchaseConfirmationPanel", r.purchaseConfirmationPanel);
            Set(so, "confirmNameText", r.confirmNameText);
            Set(so, "confirmSectorText", r.confirmSectorText);
            Set(so, "confirmLogoText", r.confirmLogoText);
            Set(so, "confirmLogoBackground", r.confirmLogoBackground);
            Set(so, "confirmPricePerShareText", r.confirmPricePerShareText);
            Set(so, "confirmShareCountText", r.confirmShareCountText);
            Set(so, "confirmTotalCostText", r.confirmTotalCostText);
            Set(so, "confirmRemainingBalanceText", r.confirmRemainingBalanceText);
            Set(so, "confirmPurchaseButton", r.confirmPurchaseButton);
            Set(so, "confirmBackButton", r.confirmBackButton);

            Set(so, "portfolioPanel", r.portfolioPanel);
            Set(so, "portfolioTabButton", r.portfolioTabButton);
            Set(so, "historyTabButton", r.historyTabButton);
            Set(so, "summaryTabButton", r.summaryTabButton);
            Set(so, "portfolioTabContent", r.portfolioTabContent);
            Set(so, "historyTabContent", r.historyTabContent);
            Set(so, "summaryTabContent", r.summaryTabContent);
            Set(so, "portfolioEmptyState", r.portfolioEmptyState);
            Set(so, "portfolioEmptyStateButton", r.portfolioEmptyStateButton);
            Set(so, "portfolioHoldingsState", r.portfolioHoldingsState);
            Set(so, "portfolioValueText", r.portfolioValueText);
            Set(so, "portfolioRemainingBalanceText", r.portfolioRemainingBalanceText);
            Set(so, "portfolioProfitLossText", r.portfolioProfitLossText);
            Set(so, "portfolioHoldingsContent", r.portfolioHoldingsContent);
            Set(so, "portfolioHoldingCardPrefab", r.portfolioHoldingCardPrefab);
            Set(so, "historyScrollView", r.historyScrollView);
            Set(so, "historyContent", r.historyContent);
            Set(so, "transactionRowPrefab", r.transactionRowPrefab);
            Set(so, "historyEmptyState", r.historyEmptyState);
            Set(so, "historyEmptyTitleText", r.historyEmptyTitleText);
            Set(so, "historyEmptySubtitleText", r.historyEmptySubtitleText);
            Set(so, "summaryScrollView", r.summaryScrollView);
            Set(so, "summaryTotalInvestedText", r.summaryTotalInvestedText);
            Set(so, "summaryCurrentValueText", r.summaryCurrentValueText);
            Set(so, "summaryRemainingBalanceText", r.summaryRemainingBalanceText);
            Set(so, "summaryTotalProfitLossText", r.summaryTotalProfitLossText);
            Set(so, "summaryBestInvestmentRow", r.summaryBestInvestmentRow);
            Set(so, "summaryWorstInvestmentRow", r.summaryWorstInvestmentRow);
            Set(so, "summaryTransactionsCountRow", r.summaryTransactionsCountRow);
            Set(so, "summaryEmptyState", r.summaryEmptyState);
            Set(so, "summaryEmptyText", r.summaryEmptyText);

            Set(so, "sellSharesPanel", r.sellSharesPanel);
            Set(so, "sellNameText", r.sellNameText);
            Set(so, "sellOwnedText", r.sellOwnedText);
            Set(so, "sellLogoText", r.sellLogoText);
            Set(so, "sellLogoBackground", r.sellLogoBackground);
            Set(so, "sellCurrentPriceText", r.sellCurrentPriceText);
            Set(so, "sellQuantitySelector", r.sellQuantitySelector);
            SetQuickArray(so, "sellQuickButtons", r.sellQuickButtons);
            Set(so, "sellExpectedValueText", r.sellExpectedValueText);
            Set(so, "sellRemainingSharesText", r.sellRemainingSharesText);
            Set(so, "confirmSellButton", r.confirmSellButton);
            Set(so, "sellBackButton", r.sellBackButton);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"InvestmentTowerUIBuilder: InvestmentTowerUIController has no serialized field '{propertyName}'.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static void SetButtonArray(SerializedObject so, string propertyName, List<Button> list)
        {
            SerializedProperty arr = so.FindProperty(propertyName);
            if (arr == null)
            {
                Debug.LogError($"InvestmentTowerUIBuilder: InvestmentTowerUIController has no serialized field '{propertyName}'.");
                return;
            }
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        }

        private static void SetCardArray(SerializedObject so, string propertyName, List<CompanyCardView> list)
        {
            SerializedProperty arr = so.FindProperty(propertyName);
            if (arr == null)
            {
                Debug.LogError($"InvestmentTowerUIBuilder: InvestmentTowerUIController has no serialized field '{propertyName}'.");
                return;
            }
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        }

        private static void SetQuickArray(SerializedObject so, string propertyName, List<QuickQuantityButtonRef> list)
        {
            SerializedProperty arr = so.FindProperty(propertyName);
            if (arr == null)
            {
                Debug.LogError($"InvestmentTowerUIBuilder: InvestmentTowerUIController has no serialized field '{propertyName}'.");
                return;
            }
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                SerializedProperty el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("button").objectReferenceValue = list[i].button;
                el.FindPropertyRelative("amount").intValue = list[i].amount;
                el.FindPropertyRelative("mode").enumValueIndex = (int)list[i].mode;
            }
        }

        // ------------------------------------------------------------------------ shared UI helpers

        internal static RectTransform CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        internal static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        internal static void AnchorFraction(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        internal static Image CreatePanelBackground(Transform parent, Sprite sprite, out RectTransform rt)
        {
            rt = CreateUIObject("PanelRoot", parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = true;
            return img;
        }

        // Every top-level panel is an empty, unnamed-visual anchor (e.g. "CompanyListPanel")
        // containing exactly one visual child named "Popup" (the actual rounded card - Image +
        // all interactive content). This split exists purely so the Inspector/Hierarchy reads
        // cleanly (panelName > Popup > named sections), matching the requested hierarchy - it
        // has no behavioral effect versus the old single-object layout.
        internal static RectTransform BuildPanelWithPopup(Transform parent, string panelName, Sprite sprite, out GameObject panelRoot)
        {
            RectTransform panel = CreateUIObject(panelName, parent);
            StretchFull(panel);
            panelRoot = panel.gameObject;

            Image popupImg = CreatePanelBackground(panel, sprite, out RectTransform popup);
            popup.gameObject.name = "Popup";
            return popup;
        }

        internal static TMP_Text CreateText(Transform parent, string name, string content, float fontSize,
            Color color, TextAlignmentOptions alignment, bool rtl, TMP_FontAsset font, FontStyles style = FontStyles.Normal)
        {
            RectTransform rt = CreateUIObject(name, parent);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = rtl ? StartFlow.Arabic.ArabicTextShaper.Shape(content) : content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.isRightToLeftText = rtl;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.fontStyle = style;
            if (font != null) text.font = font;
            return text;
        }

        internal static Image CreateImage(Transform parent, string name, Sprite sprite)
        {
            RectTransform rt = CreateUIObject(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        internal static Button CreateSpriteButton(Transform parent, string name, Sprite sprite)
        {
            RectTransform rt = CreateUIObject(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = true;
            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            return btn;
        }

        internal static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing, RectOffset padding = null,
            TextAnchor alignment = TextAnchor.UpperCenter)
        {
            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.childAlignment = alignment;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            if (padding != null) vlg.padding = padding;
            return vlg;
        }

        internal static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, float spacing, RectOffset padding = null,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = alignment;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            if (padding != null) hlg.padding = padding;
            return hlg;
        }

        internal static LayoutElement AddLayoutElement(GameObject go, float preferredHeight = -1, float preferredWidth = -1,
            float flexibleWidth = -1, float flexibleHeight = -1)
        {
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.preferredWidth = preferredWidth;
            le.flexibleWidth = flexibleWidth;
            le.flexibleHeight = flexibleHeight;
            return le;
        }

        // Shared hidden/visible CanvasGroup states for rows that must stay always-active (fixed
        // reserved LayoutElement space) and toggle purely via alpha/interactable/blocksRaycasts -
        // see BuildCompanyDetails's QuickAmountRow/SummaryRow. Editor-side counterpart of the
        // runtime helpers of the same name on InvestmentTowerUIController (Editor code can't be
        // referenced from player/runtime code, so these can't be literally shared).
        internal static void SetHiddenGroup(CanvasGroup group)
        {
            if (group == null) return;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        internal static void SetVisibleGroup(CanvasGroup group)
        {
            if (group == null) return;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }
}
