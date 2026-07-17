using StartFlow.Arabic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI.Editor
{
    internal static partial class InvestmentTowerUIBuilder
    {
        // ------------------------------------------------------------------------ PortfolioPanel

        // Builds PortfolioPanel's contents directly under a pre-created "Popup" RectTransform
        // (see BuildPanelWithPopup) so both the full build and the PortfolioPanel-only targeted
        // rebuild share this exact logic - never duplicated.
        internal static void BuildPortfolioContents(RectTransform popup, Art art, Prefabs prefabs, ControllerRefs r)
        {
            InvestmentPopupHeaderView portfolioHeader = InstantiateHeader(popup, prefabs, art, "محفظة الاستثمار", showBack: false);
            r.closeButtons.Add(portfolioHeader.LeftButton);

            // TabsRow - 3 equal-width tabs, sits directly below the fixed-height 56px header.
            RectTransform tabsRow = CreateUIObject("TabsRow", popup);
            AnchorFraction(tabsRow, 0.05f, 0.78f, 0.95f, 0.86f);
            AddHorizontalLayout(tabsRow.gameObject, 8f);
            r.portfolioTabButton = BuildTabButton(tabsRow, art, "محفظتي", art.InvestIcon);
            r.historyTabButton = BuildTabButton(tabsRow, art, "التاريخ", art.HistoryIcon);
            r.summaryTabButton = BuildTabButton(tabsRow, art, "الملخص", art.IconGeneric);

            // TabArea - PortfolioContent / HistoryTabContent / SummaryTabContent, only one active
            // at a time (see InvestmentTowerUIController.SwitchPortfolioTab).
            RectTransform tabArea = CreateUIObject("TabArea", popup);
            AnchorFraction(tabArea, 0.05f, 0.14f, 0.95f, 0.76f);

            BuildPortfolioContent(tabArea, art, prefabs, r);
            BuildHistoryTabContent(tabArea, art, prefabs, r);
            BuildSummaryTabContent(tabArea, art, prefabs, r);

            // BottomActions - reserved per the required hierarchy; no always-on button is
            // specified for PortfolioPanel itself (the empty-state's own "استكشف الأسهم" button
            // lives inside EmptyState, and each holding card has its own ActionsRow), so this is
            // deliberately left as a real, non-zero, empty, inspector-editable container ready
            // for future content rather than an invented button.
            RectTransform bottomActions = CreateUIObject("BottomActions", popup);
            AnchorFraction(bottomActions, 0.05f, 0.03f, 0.95f, 0.12f);
        }

        private static void BuildPortfolio(Transform parent, Art art, Prefabs prefabs, ControllerRefs r)
        {
            RectTransform popup = BuildPanelWithPopup(parent, "PortfolioPanel", art.Sixthpage, out GameObject panelRoot);
            r.portfolioPanel = panelRoot;
            BuildPortfolioContents(popup, art, prefabs, r);
        }

        // ------------------------------------------------------------------------ PortfolioContent (محفظتي tab)

        private static void BuildPortfolioContent(Transform tabArea, Art art, Prefabs prefabs, ControllerRefs r)
        {
            RectTransform portfolioContent = CreateUIObject("PortfolioContent", tabArea);
            StretchFull(portfolioContent);
            r.portfolioTabContent = portfolioContent.gameObject;

            // EmptyState - shown ONLY (title/subtitle/button, nothing else) while the player owns
            // zero shares. Centered both axes inside PortfolioContent.
            RectTransform emptyState = CreateUIObject("EmptyState", portfolioContent);
            StretchFull(emptyState);
            AddVerticalLayout(emptyState.gameObject, 10f, new RectOffset(24, 24, 0, 0), TextAnchor.MiddleCenter);
            r.portfolioEmptyState = emptyState.gameObject;
            TMP_Text emptyTitle = CreateText(emptyState, "TitleText", "محفظتك فارغة حالياً", 18f, InvestmentPalette.TextPrimary, TextAlignmentOptions.Center, true, art.Bold, FontStyles.Bold);
            AddLayoutElement(emptyTitle.gameObject, preferredHeight: 26);
            TMP_Text emptySubtitle = CreateText(emptyState, "SubtitleText", "اشترِ أول سهم عشان تبدأ تتابع استثماراتك", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.Center, true, art.Regular);
            emptySubtitle.enableWordWrapping = true;
            AddLayoutElement(emptySubtitle.gameObject, preferredHeight: 40);
            // emptyState's VerticalLayoutGroup force-expands child width (matching every other
            // full-width primary button in this UI, e.g. the tutorial's NextButton), so the
            // button fills the padded row width rather than being pinned to a fixed width.
            GameObject exploreBtn = Object.Instantiate(prefabs.PrimaryButton, emptyState);
            exploreBtn.name = "ExploreStocksButton";
            AddLayoutElement(exploreBtn, preferredHeight: 50);
            SetButtonLabel(exploreBtn, "استكشف الأسهم", art);
            r.portfolioEmptyStateButton = exploreBtn.GetComponent<Button>();

            // HoldingsState - shown ONLY while the player owns 1+ shares: PortfolioSummaryRow,
            // SectionTitle, HoldingsList, in that exact order.
            RectTransform holdingsState = CreateUIObject("HoldingsState", portfolioContent);
            StretchFull(holdingsState);
            AddVerticalLayout(holdingsState.gameObject, 10f);
            r.portfolioHoldingsState = holdingsState.gameObject;

            RectTransform summaryRow = CreateUIObject("PortfolioSummaryRow", holdingsState);
            AddLayoutElement(summaryRow.gameObject, preferredHeight: 56);
            AddHorizontalLayout(summaryRow.gameObject, 8f);
            r.portfolioValueText = BuildPortfolioSummaryCard(summaryRow, art, "PortfolioValueCard", "قيمة المحفظة");
            r.portfolioRemainingBalanceText = BuildPortfolioSummaryCard(summaryRow, art, "RemainingBalanceCard", "الرصيد المتبقي");
            r.portfolioProfitLossText = BuildPortfolioSummaryCard(summaryRow, art, "ProfitLossCard", "الربح / الخسارة");

            TMP_Text sectionTitle = CreateText(holdingsState, "SectionTitle", "الأسهم المملوكة", 15f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, true, art.Bold, FontStyles.Bold);
            AddLayoutElement(sectionTitle.gameObject, preferredHeight: 22);

            RectTransform holdingsList = BuildScrollList(holdingsState, "HoldingsList", out Transform holdingsContent);
            AddLayoutElement(holdingsList.gameObject, flexibleHeight: 1);
            r.portfolioHoldingsContent = holdingsContent;
            r.portfolioHoldingCardPrefab = prefabs.PortfolioHoldingCard.GetComponent<PortfolioHoldingCardView>();
        }

        // Named per-card summary card (not a generic "StatPill") - label smaller/muted above a
        // larger/bold value, matching "labels smaller than values".
        private static TMP_Text BuildPortfolioSummaryCard(Transform parent, Art art, string cardName, string label)
        {
            RectTransform card = CreateUIObject(cardName, parent);
            AddLayoutElement(card.gameObject, flexibleWidth: 1);
            Image bg = card.gameObject.AddComponent<Image>();
            bg.sprite = RoundedDark();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.05f);
            AddVerticalLayout(card.gameObject, 3f, new RectOffset(6, 6, 8, 8));

            TMP_Text value = CreateText(card, "ValueText", "-", 15f, InvestmentPalette.TextPrimary, TextAlignmentOptions.Center, false, art.Bold, FontStyles.Bold);
            CreateText(card, "LabelText", label, 11f, InvestmentPalette.TextMuted, TextAlignmentOptions.Center, true, art.Regular);
            return value;
        }

        // ------------------------------------------------------------------------ History tab

        // HistoryTabContent > HistoryEmptyState (one centered rounded box) | HistoryScrollView
        // (Viewport > Content, only ever holding real, individually rounded TransactionRow
        // instances - see RefreshHistory). Only one of the two is ever active.
        private static void BuildHistoryTabContent(Transform tabArea, Art art, Prefabs prefabs, ControllerRefs r)
        {
            RectTransform historyTab = CreateUIObject("HistoryTabContent", tabArea);
            StretchFull(historyTab);
            historyTab.gameObject.SetActive(false);
            r.historyTabContent = historyTab.gameObject;

            GameObject historyEmpty = BuildCenteredEmptyBox(historyTab, "HistoryEmptyState", art,
                "لا توجد عمليات حتى الآن", "أول عملية شراء أو بيع ستظهر هنا",
                out TMP_Text historyTitle, out TMP_Text historySubtitle);
            r.historyEmptyState = historyEmpty;
            r.historyEmptyTitleText = historyTitle;
            r.historyEmptySubtitleText = historySubtitle;

            RectTransform historyScroll = BuildScrollList(historyTab, "HistoryScrollView", out Transform historyContent,
                new RectOffset(12, 12, 12, 12), 12f);
            r.historyScrollView = historyScroll.gameObject;
            r.historyContent = historyContent;
            r.transactionRowPrefab = prefabs.TransactionRow.GetComponent<TransactionRowView>();
        }

        // Shared "one centered box" empty-state pattern for History/Summary: a StretchFull
        // wrapper (for centering) containing one fixed-size rounded box with a title and an
        // optional subtitle. subtitle/subtitleText are null for a title-only box (Summary).
        private static GameObject BuildCenteredEmptyBox(Transform parent, string wrapperName, Art art,
            string title, string subtitle, out TMP_Text titleText, out TMP_Text subtitleText)
        {
            RectTransform wrapper = CreateUIObject(wrapperName, parent);
            StretchFull(wrapper);

            RectTransform box = CreateUIObject("EmptyBox", wrapper);
            box.anchorMin = new Vector2(0.5f, 0.5f);
            box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(400f, string.IsNullOrEmpty(subtitle) ? 90f : 140f);
            Image bg = box.gameObject.AddComponent<Image>();
            bg.sprite = RoundedDark();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.05f);
            AddVerticalLayout(box.gameObject, 8f, new RectOffset(20, 20, 16, 16), TextAnchor.MiddleCenter);

            titleText = CreateText(box, "TitleText", title, 16f, InvestmentPalette.TextPrimary, TextAlignmentOptions.Center, true, art.Bold, FontStyles.Bold);

            if (!string.IsNullOrEmpty(subtitle))
            {
                subtitleText = CreateText(box, "SubtitleText", subtitle, 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.Center, true, art.Regular);
                subtitleText.enableWordWrapping = true;
            }
            else
            {
                subtitleText = null;
            }

            return wrapper.gameObject;
        }

        // ------------------------------------------------------------------------ Summary tab

        // SummaryTabContent > SummaryEmptyState (one centered rounded box) | SummaryScrollView
        // (Viewport > Content > SummarySection: a static MainStatsGrid of 4 stat cards + a
        // static PerformanceBox of 3 rows). Only one of the two top-level states is ever active.
        private static void BuildSummaryTabContent(Transform tabArea, Art art, Prefabs prefabs, ControllerRefs r)
        {
            RectTransform summaryTab = CreateUIObject("SummaryTabContent", tabArea);
            StretchFull(summaryTab);
            summaryTab.gameObject.SetActive(false);
            r.summaryTabContent = summaryTab.gameObject;

            GameObject summaryEmpty = BuildCenteredEmptyBox(summaryTab, "SummaryEmptyState", art,
                "لا يوجد ملخص استثماري بعد", null, out TMP_Text summaryEmptyTitle, out _);
            r.summaryEmptyState = summaryEmpty;
            r.summaryEmptyText = summaryEmptyTitle;

            RectTransform summaryScroll = BuildScrollList(summaryTab, "SummaryScrollView", out Transform summaryScrollContent,
                new RectOffset(12, 12, 12, 12), 12f);
            r.summaryScrollView = summaryScroll.gameObject;

            RectTransform summarySection = CreateUIObject("SummarySection", summaryScrollContent);
            AddVerticalLayout(summarySection.gameObject, 12f);

            // MainStatsGrid - 4 fixed equal-size stat cards, 2 columns. GridLayoutGroup reports
            // its own preferred height (rows * cellSize + spacing) up through SummarySection's
            // VerticalLayoutGroup automatically, same as every other nested layout group here.
            RectTransform statsGrid = CreateUIObject("MainStatsGrid", summarySection);
            GridLayoutGroup grid = statsGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(244f, 70f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            r.summaryTotalInvestedText = BuildPortfolioSummaryCard(statsGrid, art, "TotalInvestedCard", "إجمالي المبلغ المستثمر");
            r.summaryCurrentValueText = BuildPortfolioSummaryCard(statsGrid, art, "CurrentPortfolioValueCard", "القيمة الحالية للمحفظة");
            r.summaryRemainingBalanceText = BuildPortfolioSummaryCard(statsGrid, art, "RemainingBalanceCard", "الرصيد المتبقي");
            r.summaryTotalProfitLossText = BuildPortfolioSummaryCard(statsGrid, art, "TotalProfitLossCard", "الربح / الخسارة");

            // PerformanceBox - one larger rounded container, 3 static SummaryRow instances (real
            // saved instances, not runtime-pooled - this tab's content is fixed-count/known in
            // advance). Row spacing (10) is the "clear divider or spacing between rows".
            RectTransform perfBox = CreateUIObject("PerformanceBox", summarySection);
            Image perfBg = perfBox.gameObject.AddComponent<Image>();
            perfBg.sprite = RoundedDark();
            perfBg.type = Image.Type.Sliced;
            perfBg.color = new Color(1f, 1f, 1f, 0.05f);
            AddVerticalLayout(perfBox.gameObject, 10f, new RectOffset(14, 14, 12, 12));

            r.summaryBestInvestmentRow = BuildStaticSummaryRow(perfBox, prefabs, "BestInvestmentRow");
            r.summaryWorstInvestmentRow = BuildStaticSummaryRow(perfBox, prefabs, "WorstInvestmentRow");
            r.summaryTransactionsCountRow = BuildStaticSummaryRow(perfBox, prefabs, "TransactionsCountRow");
        }

        // Instantiates one real, saved SummaryRow prefab instance (not a preview/throwaway clone)
        // for PerformanceBox's 3 fixed rows.
        private static SummaryRowView BuildStaticSummaryRow(Transform parent, Prefabs prefabs, string rowName)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.SummaryRow, parent);
            go.name = rowName;
            return go.GetComponent<SummaryRowView>();
        }

        // Equal-width tab: icon + label centered, dark muted fill when inactive, green fill +
        // green Outline border when active (see InvestmentTowerUIController.SetTabButtonActive).
        private static Button BuildTabButton(Transform parent, Art art, string label, Sprite icon)
        {
            Button btn = CreateSpriteButton(parent, "Tab_" + label, RoundedDark());
            AddLayoutElement(btn.gameObject, flexibleWidth: 1, preferredHeight: 40);
            Image bg = (Image)btn.targetGraphic;
            bg.type = Image.Type.Sliced;
            bg.color = InvestmentPalette.CardNormal;

            Outline activeBorder = btn.gameObject.AddComponent<Outline>();
            activeBorder.effectColor = InvestmentPalette.Positive;
            activeBorder.effectDistance = new Vector2(1.5f, -1.5f);
            activeBorder.enabled = false;

            RectTransform row = CreateUIObject("TabContentRow", btn.transform);
            StretchFull(row);
            AddHorizontalLayout(row.gameObject, 4f);

            if (icon != null)
            {
                RectTransform iconRT = CreateUIObject("TabIcon", row);
                AddLayoutElement(iconRT.gameObject, preferredWidth: 16, preferredHeight: 16).flexibleWidth = 0;
                Image iconImg = iconRT.gameObject.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            TMP_Text text = CreateText(row, "TabLabel", label, 14f, InvestmentPalette.TextPrimary, TextAlignmentOptions.Center, true, art.Bold, FontStyles.Bold);
            AddLayoutElement(text.gameObject, flexibleWidth: 1);
            return btn;
        }

        // Real ScrollRect + Viewport (RectMask2D) + Content (VerticalLayoutGroup +
        // ContentSizeFitter) - used for every scrolling list in the Investment Tower UI.
        // padding/spacing default to the original values so the existing Portfolio HoldingsList
        // call site is completely unaffected - only History/Summary's new call sites pass
        // explicit values.
        private static RectTransform BuildScrollList(Transform parent, string name, out Transform content,
            RectOffset padding = null, float spacing = 8f)
        {
            RectTransform root = CreateUIObject(name, parent);
            StretchFull(root);
            ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = CreateUIObject("Viewport", root);
            StretchFull(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

            RectTransform contentRT = CreateUIObject("Content", viewport);
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0f, 0f);
            AddVerticalLayout(contentRT.gameObject, spacing, padding);
            ContentSizeFitter fitter = contentRT.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = contentRT;
            content = contentRT;
            return root;
        }

        // ------------------------------------------------------------------------ SellSharesPanel

        private static void BuildSellShares(Transform parent, Art art, Prefabs prefabs, ControllerRefs r)
        {
            CreatePanelBackground(parent, art.SevenPage, out RectTransform panel);
            panel.gameObject.name = "SellSharesPanel";
            r.sellSharesPanel = panel.gameObject;

            InvestmentPopupHeaderView sellHeader = InstantiateHeader(panel, prefabs, art, string.Empty, showBack: true);
            r.sellBackButton = sellHeader.LeftButton; // this panel has no separate bottom "رجوع" button - the header chevron is the only way back

            RectTransform content = CreateUIObject("Content", panel);
            AnchorFraction(content, 0.05f, 0.05f, 0.95f, 0.85f);
            AddVerticalLayout(content.gameObject, 12f);

            GameObject headerRow = Object.Instantiate(prefabs.DetailsHeader, content);
            AddLayoutElement(headerRow, preferredHeight: 48);
            r.sellNameText = headerRow.transform.Find("TextColumn/Name").GetComponent<TMP_Text>();
            r.sellLogoText = headerRow.transform.Find("Logo/LogoText").GetComponent<TMP_Text>();
            r.sellLogoBackground = headerRow.transform.Find("Logo").GetComponent<Image>();

            // The shared DetailsHeader's single "Sector" TMP field is repurposed here (SellSharesPanel
            // only - CompanyDetails/PurchaseConfirmation still show a real sector line) as two
            // separate fields in the exact same row slot, matching the already-safe
            // "SecondCurrentPriceRow" Label+Value pattern below: a static Arabic label (shaped,
            // right-to-left) and a dynamic numeric value ("5 أسهم", left-to-right, number-led) -
            // never a single composed sentence mixing multiple Arabic phrases with embedded
            // numbers, which TMP's isRightToLeftText can't reliably mirror.
            RectTransform ownedRow = (RectTransform)headerRow.transform.Find("TextColumn/Sector");
            ownedRow.gameObject.name = "OwnedSharesRow";
            Object.DestroyImmediate(ownedRow.GetComponent<TMP_Text>());
            AddHorizontalLayout(ownedRow.gameObject, 4f, alignment: TextAnchor.MiddleRight);
            TMP_Text ownedValue = CreateText(ownedRow, "OwnedSharesValueText", "0 أسهم", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineLeft, false, art.Regular);
            AddLayoutElement(ownedValue.gameObject, flexibleWidth: 1);
            CreateText(ownedRow, "OwnedSharesLabelText", "الأسهم المملوكة", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);
            r.sellOwnedText = ownedValue;

            RectTransform priceRow = CreateUIObject("CurrentPriceRow", content);
            AddLayoutElement(priceRow.gameObject, preferredHeight: art.SellContainer != null ? art.SellContainer.rect.height : 40);
            Image priceBg = priceRow.gameObject.AddComponent<Image>();
            if (art.SellContainer != null) { priceBg.sprite = art.SellContainer; priceBg.type = Image.Type.Sliced; }
            else priceBg.color = new Color(1f, 1f, 1f, 0.05f);
            AddHorizontalLayout(priceRow.gameObject, 8f, new RectOffset(14, 14, 4, 4));
            TMP_Text priceValue = CreateText(priceRow, "Value", "-", 16f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, false, art.Bold, FontStyles.Bold);
            AddLayoutElement(priceValue.gameObject, flexibleWidth: 1);
            CreateText(priceRow, "Label", "السعر الحالي للسهم", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);
            r.sellCurrentPriceText = priceValue;

            CreateText(content, "QuantityLabel", "كم سهم تبي تبيع؟", 15f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, true, art.Bold, FontStyles.Bold);

            // Reuses the one QuantitySelector prefab (task specifies a single reusable prefab
            // for this component) rather than a separate red-tinted variant.
            GameObject qsInstance = Object.Instantiate(prefabs.QuantitySelector, content);
            AddLayoutElement(qsInstance, preferredHeight: 48);
            r.sellQuantitySelector = qsInstance.GetComponent<QuantitySelectorView>();

            RectTransform quickRow = CreateUIObject("QuickOptionsRow", content);
            AddLayoutElement(quickRow.gameObject, preferredHeight: 40);
            AddHorizontalLayout(quickRow.gameObject, 8f);
            r.sellQuickButtons.Add(BuildQuickButtonMode(quickRow, art, "الكل", QuickQuantityMode.All));
            r.sellQuickButtons.Add(BuildQuickButtonMode(quickRow, art, "النصف", QuickQuantityMode.Half));
            r.sellQuickButtons.Add(BuildQuickButtonMode(quickRow, art, "1", QuickQuantityMode.Fixed, 1));

            RectTransform summaryRow = CreateUIObject("SummaryRow", content);
            AddLayoutElement(summaryRow.gameObject, preferredHeight: 36);
            AddHorizontalLayout(summaryRow.gameObject, 8f);
            (r.sellExpectedValueText, _) = BuildSummaryStat(summaryRow, art, "القيمة المتوقعة");
            (r.sellRemainingSharesText, _) = BuildSummaryStat(summaryRow, art, "الأسهم المتبقية");

            GameObject confirmGO = Object.Instantiate(prefabs.PrimaryButton, panel);
            RectTransform confirmRT = (RectTransform)confirmGO.transform;
            confirmRT.anchorMin = new Vector2(0.5f, 0f);
            confirmRT.anchorMax = new Vector2(0.5f, 0f);
            confirmRT.pivot = new Vector2(0.5f, 0f);
            confirmRT.anchoredPosition = new Vector2(0f, 20f);
            SetButtonLabel(confirmGO, "تأكيد البيع", art);
            r.confirmSellButton = confirmGO.GetComponent<Button>();
        }

        private static QuickQuantityButtonRef BuildQuickButtonMode(Transform parent, Art art, string label, QuickQuantityMode mode, int amount = 0)
        {
            Button btn = CreateSpriteButton(parent, "Quick_" + label, RoundedDark());
            AddLayoutElement(btn.gameObject, flexibleWidth: 1);
            ((Image)btn.targetGraphic).color = new Color(1f, 1f, 1f, 0.06f);
            TMP_Text text = CreateText(btn.transform, "Label", label, 13f, InvestmentPalette.TextPrimary, TextAlignmentOptions.Center, true, art.Regular);
            StretchFull(text.GetComponent<RectTransform>());
            return new QuickQuantityButtonRef { button = btn, amount = amount, mode = mode };
        }
    }
}
