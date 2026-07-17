using System.Collections.Generic;
using StartFlow.Arabic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI.Editor
{
    internal static partial class InvestmentTowerUIBuilder
    {
        // ------------------------------------------------------------------------ CompanyListPanel

        // Builds CompanyListPanel's contents directly under a pre-created "Popup" RectTransform
        // (see BuildPanelWithPopup) so both the full build and the CompanyList-only targeted
        // rebuild share this exact logic - never duplicated.
        internal static void BuildCompanyListContents(RectTransform popup, Art art, Prefabs prefabs, ControllerRefs r)
        {
            r.companyListHeader = InstantiateHeader(popup, prefabs, art, "برج الاستثمار", showBack: false);

            RectTransform topRow = CreateUIObject("TopInformationRow", popup);
            AnchorFraction(topRow, 0.05f, 0.80f, 0.95f, 0.90f);
            AddHorizontalLayout(topRow.gameObject, 10f);

            // Balance column (left side of the row)
            RectTransform balanceCol = CreateUIObject("BalanceColumn", topRow);
            AddLayoutElement(balanceCol.gameObject, preferredWidth: 150).flexibleWidth = 0;
            AddVerticalLayout(balanceCol.gameObject, 3f, alignment: TextAnchor.MiddleRight);
            CreateText(balanceCol, "AvailableBalanceLabel", "المبلغ المتاح", 12f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);

            RectTransform balanceRow = CreateUIObject("BalanceValueRow", balanceCol);
            AddHorizontalLayout(balanceRow.gameObject, 4f, alignment: TextAnchor.MiddleRight);
            AddLayoutElement(balanceRow.gameObject, preferredHeight: 22);
            RectTransform riyalIconRT = CreateUIObject("CurrencyIcon", balanceRow);
            AddLayoutElement(riyalIconRT.gameObject, preferredWidth: 20, preferredHeight: 12).flexibleWidth = 0;
            Image riyalIcon = riyalIconRT.gameObject.AddComponent<Image>();
            riyalIcon.sprite = art.Riyal;
            riyalIcon.preserveAspect = true;
            riyalIcon.raycastTarget = false;
            TMP_Text balanceValue = CreateText(balanceRow, "AvailableBalanceValueText", "0", 19f, InvestmentPalette.Positive, TextAlignmentOptions.MidlineRight, false, art.Bold, FontStyles.Bold);
            r.availableBalanceText = balanceValue;

            // Question column (right side of the row)
            RectTransform questionCol = CreateUIObject("QuestionColumn", topRow);
            AddLayoutElement(questionCol.gameObject, flexibleWidth: 1);
            AddVerticalLayout(questionCol.gameObject, 3f, alignment: TextAnchor.MiddleRight);
            CreateText(questionCol, "QuestionText", "وين تبي تستثمر؟", 18f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, true, art.Bold, FontStyles.Bold);
            CreateText(questionCol, "SubtitleText", "اختر شركة لترى تفاصيلها", 12f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);

            // Available height here is ~227px (0.45 of the 505px popup). 3 cards at
            // CompanyCardHeight (64) + 8px spacing + 6px top/bottom padding = 220px, so all 3
            // fit with room to spare and never encroach on OpenPortfolioButton/FooterNote below.
            RectTransform listContent = CreateUIObject("CompanyListContent", popup);
            AnchorFraction(listContent, 0.05f, 0.34f, 0.95f, 0.79f);
            AddVerticalLayout(listContent.gameObject, 8f, new RectOffset(0, 0, 6, 6));
            r.companyListContent = listContent;

            // Three static, individually-named company buttons (never runtime-instantiated
            // clones) so each one can be found and hand-edited directly in the Inspector/
            // Hierarchy. Order matches InvestmentSampleData.Companies (alinma/aramco/stc) so
            // InvestmentTowerUIController.RefreshCompanyList can bind companyListCards[i] to
            // Companies[i] by index. Pre-populated with the current sample data immediately so
            // the panel already looks correct in the Editor, before Play mode.
            r.companyListCards = new List<CompanyCardView>();
            (string childName, int companyIndex)[] companySlots =
            {
                ("AlinmaCompanyButton", 0),
                ("AramcoCompanyButton", 1),
                ("STCCompanyButton", 2),
            };
            foreach ((string childName, int companyIndex) in companySlots)
            {
                GameObject cardInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.CompanyCard, listContent);
                cardInstance.name = childName;
                CompanyCardView cardView = cardInstance.GetComponent<CompanyCardView>();
                if (companyIndex < InvestmentSampleData.Companies.Count)
                    cardView.SetCompany(InvestmentSampleData.Companies[companyIndex]);
                r.companyListCards.Add(cardView);
            }

            RectTransform portfolioButtonRoot = CreateUIObject("OpenPortfolioButton", popup);
            AnchorFraction(portfolioButtonRoot, 0.05f, 0.24f, 0.95f, 0.32f);
            r.openPortfolioButtonRoot = portfolioButtonRoot.gameObject;
            Button portfolioButton = CreateSpriteButton(portfolioButtonRoot, "ClickableButton", RoundedDark());
            StretchFull((RectTransform)portfolioButton.transform);
            ((Image)portfolioButton.targetGraphic).color = new Color(1f, 1f, 1f, 0.08f);
            TMP_Text portfolioLabel = CreateText(portfolioButton.transform, "ButtonLabelText", "عرض محفظتي", 16f, InvestmentPalette.Positive, TextAlignmentOptions.Center, true, art.Bold, FontStyles.Bold);
            StretchFull(portfolioLabel.GetComponent<RectTransform>());
            r.openPortfolioButton = portfolioButton;

            RectTransform footer = CreateUIObject("FooterNote", popup);
            AnchorFraction(footer, 0.05f, 0.16f, 0.95f, 0.23f);
            TMP_Text footerText = CreateText(footer, "FooterNoteText", "الأسعار تتغير حسب حركة السوق • الاستثمار فيه ربح وخسارة", 11f,
                InvestmentPalette.TextMuted, TextAlignmentOptions.Center, true, art.Regular);
            footerText.enableWordWrapping = true;
            StretchFull(footerText.GetComponent<RectTransform>());
            // Doubles as the live market-status line (loading/updated/failed - see
            // SaudiMarketService) - InvestmentTowerUIController swaps its text temporarily then
            // reverts to this disclaimer, so no extra UI element or layout change is needed.
            r.footerNoteText = footerText;
        }

        private static void BuildCompanyList(Transform parent, Art art, Prefabs prefabs, ControllerRefs r)
        {
            RectTransform popup = BuildPanelWithPopup(parent, "CompanyListPanel", art.ThirdPage, out GameObject panelRoot);
            r.companyListPanel = panelRoot;
            BuildCompanyListContents(popup, art, prefabs, r);
        }

        // ------------------------------------------------------------------------ CompanyDetailsPanel

        private static void BuildCompanyDetails(Transform parent, Art art, Prefabs prefabs, ControllerRefs r)
        {
            CreatePanelBackground(parent, art.ForthPage, out RectTransform panel);
            panel.gameObject.name = "CompanyDetailsPanel";
            r.companyDetailsPanel = panel.gameObject;

            r.companyDetailsHeader = InstantiateHeader(panel, prefabs, art, string.Empty, showBack: true);

            RectTransform content = CreateUIObject("Content", panel);
            AnchorFraction(content, 0.05f, 0.03f, 0.95f, 0.86f);
            AddVerticalLayout(content.gameObject, 12f);

            GameObject headerRow = Object.Instantiate(prefabs.DetailsHeader, content);
            AddLayoutElement(headerRow, preferredHeight: 48);
            r.detailsNameText = headerRow.transform.Find("TextColumn/Name").GetComponent<TMP_Text>();
            r.detailsSectorText = headerRow.transform.Find("TextColumn/Sector").GetComponent<TMP_Text>();
            r.detailsLogoText = headerRow.transform.Find("Logo/LogoText").GetComponent<TMP_Text>();
            r.detailsLogoBackground = headerRow.transform.Find("Logo").GetComponent<Image>();

            RectTransform statRow = CreateUIObject("StatRow", content);
            AddLayoutElement(statRow.gameObject, preferredHeight: 56);
            AddHorizontalLayout(statRow.gameObject, 8f);
            r.detailsRiskText = BuildStatPill(statRow, art, "المخاطرة", valueIsArabic: true);
            r.detailsChangeText = BuildStatPill(statRow, art, "التغير اليومي");
            r.detailsPriceText = BuildStatPill(statRow, art, "السعر الحالي");

            RectTransform descBox = CreateUIObject("DescriptionBox", content);
            AddLayoutElement(descBox.gameObject, preferredHeight: 64);
            Image descBg = descBox.gameObject.AddComponent<Image>();
            descBg.sprite = RoundedDark();
            descBg.type = Image.Type.Sliced;
            descBg.color = new Color(1f, 1f, 1f, 0.05f);
            TMP_Text desc = CreateText(descBox, "Text", string.Empty, 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.Center, true, art.Regular);
            desc.enableWordWrapping = true;
            RectTransform descRT = desc.GetComponent<RectTransform>();
            StretchFull(descRT);
            descRT.offsetMin = new Vector2(12f, 6f);
            descRT.offsetMax = new Vector2(-12f, -6f);
            r.detailsDescriptionText = desc;

            CreateText(content, "PredictionLabel", "السهم مرتفع اليوم، وش تتوقع؟", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);

            RectTransform predictRow = CreateUIObject("PredictionRow", content);
            AddLayoutElement(predictRow.gameObject, preferredHeight: 44);
            AddHorizontalLayout(predictRow.gameObject, 8f);
            (r.predictUpButton, r.predictUpBackground) = BuildPredictionButton(predictRow, art, "يكمل صعود");
            (r.predictDownButton, r.predictDownBackground) = BuildPredictionButton(predictRow, art, "ممكن ينزل");

            CreateText(content, "QuantityLabel", "كم سهم تبي تشتري؟", 15f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, true, art.Bold, FontStyles.Bold);

            GameObject quantitySelector = Object.Instantiate(prefabs.QuantitySelector, content);
            AddLayoutElement(quantitySelector, preferredHeight: 48);
            r.detailsQuantitySelector = quantitySelector.GetComponent<QuantitySelectorView>();

            // QuickAmountRow and SummaryRow stay ALWAYS ACTIVE (never SetActive(false)) with a
            // fixed, reserved LayoutElement height (flexibleHeight: 0, so content's
            // VerticalLayoutGroup never recalculates its total required height when quantity
            // changes) and are shown/hidden purely via CanvasGroup alpha/interactable/
            // blocksRaycasts - this is what actually prevents the layout-shift/overlap bug that
            // SetActive(false)/(true) caused before, since the reserved space never changes.
            RectTransform quickRow = CreateUIObject("QuickAmountRow", content);
            AddLayoutElement(quickRow.gameObject, preferredHeight: 40, flexibleHeight: 0);
            AddHorizontalLayout(quickRow.gameObject, 8f);
            r.detailsQuickButtons.Add(BuildQuickButton(quickRow, art, "1 سهم", 1));
            r.detailsQuickButtons.Add(BuildQuickButton(quickRow, art, "5 أسهم", 5));
            r.detailsQuickButtons.Add(BuildQuickButton(quickRow, art, "10 أسهم", 10));
            CanvasGroup quickGroup = quickRow.gameObject.AddComponent<CanvasGroup>();
            SetHiddenGroup(quickGroup); // quantity starts at 0 on every fresh open
            r.detailsQuickAmountGroup = quickGroup;

            RectTransform summaryRow = CreateUIObject("SummaryRow", content);
            AddLayoutElement(summaryRow.gameObject, preferredHeight: 36, flexibleHeight: 0);
            AddHorizontalLayout(summaryRow.gameObject, 8f);
            r.detailsSummaryRow = summaryRow.gameObject;
            (r.detailsPricePerShareText, _) = BuildSummaryStat(summaryRow, art, "سعر السهم");
            (r.detailsShareCountText, _) = BuildSummaryStat(summaryRow, art, "عدد الأسهم");
            CanvasGroup summaryGroup = summaryRow.gameObject.AddComponent<CanvasGroup>();
            SetHiddenGroup(summaryGroup);
            r.detailsSummaryGroup = summaryGroup;

            GameObject investGO = Object.Instantiate(prefabs.PrimaryButton, panel);
            RectTransform investRT = (RectTransform)investGO.transform;
            investRT.anchorMin = new Vector2(0.5f, 0f);
            investRT.anchorMax = new Vector2(0.5f, 0f);
            investRT.pivot = new Vector2(0.5f, 0f);
            investRT.anchoredPosition = new Vector2(0f, 20f);
            SetButtonLabel(investGO, "استثمر الآن", art);
            r.investButton = investGO.GetComponent<Button>();
        }

        // valueIsArabic must be true for pills whose Value ever holds a full Arabic word (e.g.
        // the risk-level pill's "منخفضة"/"متوسطة"/"مرتفعة") so isRightToLeftText is set
        // correctly at build time - false (the default) for numeric/price/percentage pills.
        private static TMP_Text BuildStatPill(Transform parent, Art art, string label, bool valueIsArabic = false)
        {
            RectTransform pill = CreateUIObject("StatPill", parent);
            AddLayoutElement(pill.gameObject, flexibleWidth: 1);
            Image bg = pill.gameObject.AddComponent<Image>();
            bg.sprite = RoundedDark();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.05f);
            AddVerticalLayout(pill.gameObject, 2f, new RectOffset(4, 4, 8, 8));

            TMP_Text value = CreateText(pill, "Value", "-", 15f, InvestmentPalette.TextPrimary, TextAlignmentOptions.Center, valueIsArabic, art.Bold, FontStyles.Bold);
            CreateText(pill, "Label", label, 11f, InvestmentPalette.TextMuted, TextAlignmentOptions.Center, true, art.Regular);
            return value;
        }

        private static (Button, Image) BuildPredictionButton(Transform parent, Art art, string label)
        {
            Button btn = CreateSpriteButton(parent, "PredictButton_" + label, RoundedDark());
            AddLayoutElement(btn.gameObject, flexibleWidth: 1, preferredHeight: 44);
            Image bg = (Image)btn.targetGraphic;
            bg.type = Image.Type.Sliced;
            bg.color = InvestmentPalette.CardNormal;
            TMP_Text text = CreateText(btn.transform, "Label", label, 14f, InvestmentPalette.TextPrimary, TextAlignmentOptions.Center, true, art.Bold, FontStyles.Bold);
            StretchFull(text.GetComponent<RectTransform>());
            return (btn, bg);
        }

        private static QuickQuantityButtonRef BuildQuickButton(Transform parent, Art art, string label, int amount)
        {
            Button btn = CreateSpriteButton(parent, "Quick_" + amount, RoundedDark());
            AddLayoutElement(btn.gameObject, flexibleWidth: 1);
            ((Image)btn.targetGraphic).color = new Color(1f, 1f, 1f, 0.06f);
            TMP_Text text = CreateText(btn.transform, "Label", label, 13f, InvestmentPalette.TextPrimary, TextAlignmentOptions.Center, true, art.Regular);
            StretchFull(text.GetComponent<RectTransform>());
            return new QuickQuantityButtonRef { button = btn, amount = amount, mode = QuickQuantityMode.Fixed };
        }

        private static (TMP_Text, TMP_Text) BuildSummaryStat(Transform parent, Art art, string label)
        {
            RectTransform col = CreateUIObject("Stat_" + label, parent);
            AddLayoutElement(col.gameObject, flexibleWidth: 1);
            AddVerticalLayout(col.gameObject, 2f, alignment: TextAnchor.MiddleRight);
            TMP_Text value = CreateText(col, "Value", "-", 15f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, false, art.Bold, FontStyles.Bold);
            TMP_Text lbl = CreateText(col, "Label", label, 11f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);
            return (value, lbl);
        }

        // ------------------------------------------------------------------------ PurchaseConfirmationPanel

        private static void BuildPurchaseConfirmation(Transform parent, Art art, Prefabs prefabs, ControllerRefs r)
        {
            CreatePanelBackground(parent, art.FifthPage, out RectTransform panel);
            panel.gameObject.name = "PurchaseConfirmationPanel";
            r.purchaseConfirmationPanel = panel.gameObject;

            InvestmentPopupHeaderView confirmHeader = InstantiateHeader(panel, prefabs, art, string.Empty, showBack: false);
            r.closeButtons.Add(confirmHeader.LeftButton);

            RectTransform content = CreateUIObject("Content", panel);
            AnchorFraction(content, 0.05f, 0.08f, 0.95f, 0.85f);
            AddVerticalLayout(content.gameObject, 14f);

            GameObject headerRow = Object.Instantiate(prefabs.DetailsHeader, content);
            AddLayoutElement(headerRow, preferredHeight: 48);
            r.confirmNameText = headerRow.transform.Find("TextColumn/Name").GetComponent<TMP_Text>();
            r.confirmSectorText = headerRow.transform.Find("TextColumn/Sector").GetComponent<TMP_Text>();
            r.confirmLogoText = headerRow.transform.Find("Logo/LogoText").GetComponent<TMP_Text>();
            r.confirmLogoBackground = headerRow.transform.Find("Logo").GetComponent<Image>();

            RectTransform box = CreateUIObject("PurchaseBox", content);
            AddLayoutElement(box.gameObject, preferredHeight: art.PurchaseBox.rect.height);
            Image boxImg = box.gameObject.AddComponent<Image>();
            boxImg.sprite = art.PurchaseBox;
            boxImg.type = Image.Type.Sliced;
            RectTransform boxContent = CreateUIObject("Rows", box);
            StretchFull(boxContent);
            boxContent.offsetMin = new Vector2(16f, 10f);
            boxContent.offsetMax = new Vector2(-16f, -10f);
            AddVerticalLayout(boxContent.gameObject, 6f);

            r.confirmPricePerShareText = BuildConfirmRow(boxContent, art, "سعر السهم");
            r.confirmShareCountText = BuildConfirmRow(boxContent, art, "عدد الأسهم");
            r.confirmTotalCostText = BuildConfirmRow(boxContent, art, "إجمالي الاستثمار", InvestmentPalette.Positive);
            r.confirmRemainingBalanceText = BuildConfirmRow(boxContent, art, "الرصيد بعد الشراء");

            GameObject confirmGO = Object.Instantiate(prefabs.PrimaryButton, content);
            AddLayoutElement(confirmGO, preferredHeight: 56);
            SetButtonLabel(confirmGO, "تأكيد الشراء", art);
            r.confirmPurchaseButton = confirmGO.GetComponent<Button>();

            GameObject backGO = Object.Instantiate(prefabs.SecondaryButton, content);
            AddLayoutElement(backGO, preferredHeight: 48);
            SetButtonLabel(backGO, "رجوع", art);
            r.confirmBackButton = backGO.GetComponent<Button>();
        }

        private static TMP_Text BuildConfirmRow(Transform parent, Art art, string label, Color? valueColor = null)
        {
            RectTransform row = CreateUIObject("Row_" + label, parent);
            AddLayoutElement(row.gameObject, preferredHeight: 26);
            AddHorizontalLayout(row.gameObject, 8f);

            TMP_Text value = CreateText(row, "Value", "-", 16f, valueColor ?? InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, false, art.Bold, FontStyles.Bold);
            AddLayoutElement(value.gameObject, flexibleWidth: 1);
            TMP_Text lbl = CreateText(row, "Label", label, 14f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);
            AddLayoutElement(lbl.gameObject, flexibleWidth: 1);
            return value;
        }
    }
}
