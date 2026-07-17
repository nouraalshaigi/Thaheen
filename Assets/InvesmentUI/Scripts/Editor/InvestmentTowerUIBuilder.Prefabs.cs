using BuildingInteractionSystem;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI.Editor
{
    internal static partial class InvestmentTowerUIBuilder
    {
        private static bool EnsurePrefabs(Art art, bool force, out Prefabs prefabs)
        {
            prefabs = new Prefabs();
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/InvesmentUI", "Prefabs");

            prefabs.PrimaryButton = BuildOrLoad("InvestmentPrimaryButton", force, () => BuildPrimaryButtonPrefab(art));
            prefabs.SecondaryButton = BuildOrLoad("InvestmentSecondaryButton", force, () => BuildSecondaryButtonPrefab(art));
            prefabs.CompanyCard = BuildOrLoad("CompanyCard", force, () => BuildCompanyCardPrefab(art));
            prefabs.DetailsHeader = BuildOrLoad("CompanyDetailsHeader", force, () => BuildCompanyDetailsHeaderPrefab(art));
            prefabs.QuantitySelector = BuildOrLoad("QuantitySelector", force, () => BuildQuantitySelectorPrefab(art, forSell: false));
            prefabs.PortfolioHoldingCard = BuildOrLoad("PortfolioHoldingCard", force, () => BuildPortfolioHoldingCardPrefab(art));
            prefabs.TransactionRow = BuildOrLoad("TransactionRow", force, () => BuildTransactionRowPrefab(art));
            prefabs.SummaryRow = BuildOrLoad("SummaryRow", force, () => BuildSummaryRowPrefab(art));
            prefabs.PopupHeader = BuildOrLoad("InvestmentPopupHeader", force, () => BuildPopupHeaderPrefab(art));

            return prefabs.PrimaryButton != null && prefabs.SecondaryButton != null && prefabs.CompanyCard != null &&
                prefabs.DetailsHeader != null && prefabs.QuantitySelector != null && prefabs.PortfolioHoldingCard != null &&
                prefabs.TransactionRow != null && prefabs.SummaryRow != null && prefabs.PopupHeader != null;
        }

        private static GameObject BuildOrLoad(string prefabName, bool force, System.Func<GameObject> builder)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            if (!force)
            {
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null) return existing;
            }

            GameObject scratch = builder();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(scratch, path);
            Object.DestroyImmediate(scratch);
            return saved;
        }

        private static Sprite RoundedDark() => RoundedRectSpriteFactory.GetRoundedSprite(16, 96);

        // ------------------------------------------------------------------------ InvestmentPrimaryButton

        private static GameObject BuildPrimaryButtonPrefab(Art art)
        {
            GameObject go = new GameObject("InvestmentPrimaryButton", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(347f, 56f);
            Image img = go.AddComponent<Image>();
            img.sprite = art.GBtn1;
            img.type = Image.Type.Sliced;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            TMP_Text label = CreateText(rt, "Label", "زر", 22f, Color.white, TextAlignmentOptions.Center, true, art.Bold, FontStyles.Bold);
            StretchFull(label.GetComponent<RectTransform>());
            return go;
        }

        // ------------------------------------------------------------------------ InvestmentSecondaryButton

        private static GameObject BuildSecondaryButtonPrefab(Art art)
        {
            GameObject go = new GameObject("InvestmentSecondaryButton", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(347f, 50f);
            Image img = go.AddComponent<Image>();
            img.sprite = RoundedDark();
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, 0.08f);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            TMP_Text label = CreateText(rt, "Label", "رجوع", 20f, InvestmentPalette.TextMuted, TextAlignmentOptions.Center, true, art.Regular);
            StretchFull(label.GetComponent<RectTransform>());
            return go;
        }

        // ------------------------------------------------------------------------ CompanyCard

        // Child names match the required list exactly: Background, CompanyAccentArt,
        // SelectionBorder, CompanyBadge (+ CompanyLogoText, since no per-company logo art exists -
        // see CompanyCardView), CompanyNameText, SectorText, CurrentPriceText, CurrencyText,
        // DailyChangeText, TrendIcon, ClickableButton (the card root itself - see CompanyCardView's
        // clickableButton field; the whole card is clickable, not just a small sub-region).
        // Visual left-to-right order in Row is ChangeColumn / TextColumn / CompanyBadge, matching
        // the reference (trend+% on the left edge, name+sector+price in the middle, brand badge
        // on the right edge).
        private const float CompanyCardHeight = 64f;

        private static GameObject BuildCompanyCardPrefab(Art art)
        {
            GameObject go = new GameObject("CompanyCard", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(500f, CompanyCardHeight);

            // CompanyListContent's VerticalLayoutGroup has childControlHeight=true but
            // childForceExpandHeight=false, so each card's height comes from its own preferred
            // size. The card root has no other ILayoutElement (Button/CompanyCardView don't
            // count), so WITHOUT this it resolves to 0 - collapsing all 3 cards on top of each
            // other and shrinking their real clickable area to nothing (clicks then miss the
            // card and fall through to the dim overlay's close button). This LayoutElement is
            // what actually reserves the card's visible height.
            LayoutElement cardLayoutElement = go.AddComponent<LayoutElement>();
            cardLayoutElement.preferredHeight = CompanyCardHeight;
            cardLayoutElement.minHeight = CompanyCardHeight;

            RectTransform backgroundRT = CreateUIObject("Background", rt);
            StretchFull(backgroundRT);
            Image bg = backgroundRT.gameObject.AddComponent<Image>();
            bg.sprite = art.ButtonForCompanys2 != null ? art.ButtonForCompanys2 : RoundedDark();
            bg.type = Image.Type.Sliced;
            bg.color = InvestmentPalette.CardNormal;

            // Exact provided per-company button asset (Assets/InvesmentUI/CompanyListPanel) -
            // swapped by CompanyCardView.SetCompany based on InvestmentCompany.id. Sits above the
            // card fill, below the readable text/icons, at its own native (subtle) opacity - never
            // tinted or stretched out of aspect beyond the row's own shape.
            RectTransform accentRT = CreateUIObject("CompanyAccentArt", rt);
            StretchFull(accentRT);
            Image accentImg = accentRT.gameObject.AddComponent<Image>();
            accentImg.color = Color.white;
            accentImg.raycastTarget = false;
            accentImg.gameObject.SetActive(false);

            RectTransform selectionBorderRT = CreateUIObject("SelectionBorder", rt);
            StretchFull(selectionBorderRT);
            Image selectionBorderImg = selectionBorderRT.gameObject.AddComponent<Image>();
            selectionBorderImg.sprite = RoundedDark();
            selectionBorderImg.type = Image.Type.Sliced;
            selectionBorderImg.color = InvestmentPalette.Positive;
            selectionBorderImg.raycastTarget = false;
            Outline selectionOutline = selectionBorderRT.gameObject.AddComponent<Outline>();
            selectionOutline.effectColor = InvestmentPalette.Positive;
            selectionOutline.effectDistance = new Vector2(2f, -2f);
            selectionBorderRT.gameObject.SetActive(false);

            Button clickableButton = go.AddComponent<Button>();
            clickableButton.targetGraphic = bg;
            clickableButton.transition = Selectable.Transition.None;

            RectTransform row = CreateUIObject("Row", rt);
            StretchFull(row);
            row.offsetMin = new Vector2(14f, 8f);
            row.offsetMax = new Vector2(-14f, -8f);
            AddHorizontalLayout(row.gameObject, 12f);

            // Change % + trend icon column (leftmost)
            RectTransform changeCol = CreateUIObject("ChangeColumn", row);
            AddLayoutElement(changeCol.gameObject, preferredWidth: 84).flexibleWidth = 0;
            AddVerticalLayout(changeCol.gameObject, 3f, alignment: TextAnchor.MiddleLeft);

            RectTransform changeRow = CreateUIObject("ChangeRow", changeCol);
            AddHorizontalLayout(changeRow.gameObject, 4f, alignment: TextAnchor.MiddleLeft);
            AddLayoutElement(changeRow.gameObject, preferredHeight: 18);
            RectTransform trendRT = CreateUIObject("TrendIcon", changeRow);
            AddLayoutElement(trendRT.gameObject, preferredWidth: 24, preferredHeight: 16).flexibleWidth = 0;
            Image trendImg = trendRT.gameObject.AddComponent<Image>();
            trendImg.preserveAspect = true;
            trendImg.raycastTarget = false;
            TMP_Text dailyChangeText = CreateText(changeRow, "DailyChangeText", "+0.00%", 15f, InvestmentPalette.Positive, TextAlignmentOptions.MidlineLeft, false, art.Bold, FontStyles.Bold);

            TMP_Text ownedSharesText = CreateText(changeCol, "OwnedSharesText", string.Empty, 12f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineLeft, false, art.Regular);
            ownedSharesText.gameObject.SetActive(false);

            // Name + sector/price column (middle, flexible)
            RectTransform textCol = CreateUIObject("TextColumn", row);
            AddLayoutElement(textCol.gameObject, flexibleWidth: 1);
            AddVerticalLayout(textCol.gameObject, 3f, alignment: TextAnchor.MiddleRight);
            TMP_Text nameText = CreateText(textCol, "CompanyNameText", "اسم الشركة", 19f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, true, art.Bold, FontStyles.Bold);

            // "28.40 ريال  •  القطاع" on one line, matching the reference - CurrentPriceText and
            // CurrencyText stay separate editable TMP fields (as required) laid out left of a
            // static "•" separator, with SectorText on the right.
            RectTransform sectorPriceRow = CreateUIObject("SectorPriceRow", textCol);
            AddHorizontalLayout(sectorPriceRow.gameObject, 6f, alignment: TextAnchor.MiddleRight);
            AddLayoutElement(sectorPriceRow.gameObject, preferredHeight: 16);

            RectTransform priceRow = CreateUIObject("PriceRow", sectorPriceRow);
            AddHorizontalLayout(priceRow.gameObject, 4f, alignment: TextAnchor.MiddleRight);
            AddLayoutElement(priceRow.gameObject, preferredWidth: 76).flexibleWidth = 0;
            TMP_Text currentPriceText = CreateText(priceRow, "CurrentPriceText", "0.00", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, false, art.Regular);
            AddLayoutElement(currentPriceText.gameObject, flexibleWidth: 1);
            TMP_Text currencyText = CreateText(priceRow, "CurrencyText", "ريال", 12f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);
            AddLayoutElement(currencyText.gameObject, preferredWidth: 30).flexibleWidth = 0;

            TMP_Text separatorText = CreateText(sectorPriceRow, "SectorSeparatorText", "•", 12f, InvestmentPalette.TextMuted, TextAlignmentOptions.Center, false, art.Regular);
            AddLayoutElement(separatorText.gameObject, preferredWidth: 12).flexibleWidth = 0;

            TMP_Text sectorText = CreateText(sectorPriceRow, "SectorText", "القطاع", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);
            AddLayoutElement(sectorText.gameObject, flexibleWidth: 1);

            // CompanyBadge (rightmost)
            RectTransform logoRT = CreateUIObject("CompanyBadge", row);
            AddLayoutElement(logoRT.gameObject, preferredWidth: 44, preferredHeight: 44).flexibleWidth = 0;
            Image companyLogo = logoRT.gameObject.AddComponent<Image>();
            companyLogo.sprite = RoundedRectSpriteFactory.GetRoundedSprite(22, 48);
            companyLogo.type = Image.Type.Sliced;
            companyLogo.color = Color.gray;
            TMP_Text logoText = CreateText(logoRT, "CompanyLogoText", "ال", 15f, Color.white, TextAlignmentOptions.Center, false, art.Bold, FontStyles.Bold);
            StretchFull(logoText.GetComponent<RectTransform>());

            CompanyCardView view = go.AddComponent<CompanyCardView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("companyAccentArt").objectReferenceValue = accentImg;
            so.FindProperty("alinmaAccentSprite").objectReferenceValue = art.AlinmaAccent;
            so.FindProperty("aramcoAccentSprite").objectReferenceValue = art.AramcoAccent;
            so.FindProperty("stcAccentSprite").objectReferenceValue = art.StcAccent;
            so.FindProperty("selectionBorder").objectReferenceValue = selectionBorderImg;
            so.FindProperty("companyLogo").objectReferenceValue = companyLogo;
            so.FindProperty("companyLogoText").objectReferenceValue = logoText;
            so.FindProperty("companyNameText").objectReferenceValue = nameText;
            so.FindProperty("sectorText").objectReferenceValue = sectorText;
            so.FindProperty("currentPriceText").objectReferenceValue = currentPriceText;
            so.FindProperty("currencyText").objectReferenceValue = currencyText;
            so.FindProperty("dailyChangeText").objectReferenceValue = dailyChangeText;
            so.FindProperty("trendIcon").objectReferenceValue = trendImg;
            so.FindProperty("trendUpSprite").objectReferenceValue = art.TrendUp;
            so.FindProperty("trendDownSprite").objectReferenceValue = art.TrendDown;
            so.FindProperty("ownedSharesText").objectReferenceValue = ownedSharesText;
            so.FindProperty("clickableButton").objectReferenceValue = clickableButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ------------------------------------------------------------------------ CompanyDetailsHeader
        // (name + sector + logo row, reused at the top of CompanyDetails/PurchaseConfirmation/SellShares)

        private static GameObject BuildCompanyDetailsHeaderPrefab(Art art)
        {
            GameObject go = new GameObject("CompanyDetailsHeader", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(460f, 48f);
            AddHorizontalLayout(go, 10f);

            RectTransform logo = CreateUIObject("Logo", rt);
            AddLayoutElement(logo.gameObject, preferredWidth: 44, preferredHeight: 44).flexibleWidth = 0;
            Image logoBg = logo.gameObject.AddComponent<Image>();
            logoBg.sprite = RoundedRectSpriteFactory.GetRoundedSprite(22, 48);
            logoBg.type = Image.Type.Sliced;
            logoBg.color = Color.gray;
            TMP_Text logoText = CreateText(logo, "LogoText", "ال", 15f, Color.white, TextAlignmentOptions.Center, false, art.Bold, FontStyles.Bold);
            StretchFull(logoText.GetComponent<RectTransform>());

            RectTransform textCol = CreateUIObject("TextColumn", rt);
            AddLayoutElement(textCol.gameObject, flexibleWidth: 1);
            AddVerticalLayout(textCol.gameObject, 2f, alignment: TextAnchor.MiddleRight);
            TMP_Text name = CreateText(textCol, "Name", "اسم الشركة", 20f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, true, art.Bold, FontStyles.Bold);
            TMP_Text sector = CreateText(textCol, "Sector", "القطاع", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);

            // No dedicated script - InvestmentTowerUIController grabs these child references
            // directly by name when this prefab is instantiated inline (see Trading.cs).
            return go;
        }

        // ------------------------------------------------------------------------ QuantitySelector

        private static GameObject BuildQuantitySelectorPrefab(Art art, bool forSell)
        {
            GameObject go = new GameObject("QuantitySelector", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(347f, 48f);
            AddHorizontalLayout(go, 10f);

            Sprite minusSprite = art.MinuesButton;
            Sprite plusSprite = forSell && art.PlusRedButton != null ? art.PlusRedButton : art.PlusButton;

            Button minus = CreateSpriteButton(rt, "MinusButton", minusSprite);
            AddLayoutElement(minus.gameObject, preferredWidth: 32, preferredHeight: 32).flexibleWidth = 0;
            ((Image)minus.targetGraphic).preserveAspect = true;

            RectTransform valueBox = CreateUIObject("ValueBox", rt);
            AddLayoutElement(valueBox.gameObject, flexibleWidth: 1, preferredHeight: 44);
            Image valueBg = valueBox.gameObject.AddComponent<Image>();
            valueBg.sprite = RoundedDark();
            valueBg.type = Image.Type.Sliced;
            valueBg.color = new Color(1f, 1f, 1f, 0.06f);
            TMP_Text valueText = CreateText(valueBox, "ValueText", "0 سهم", 20f, InvestmentPalette.TextPrimary, TextAlignmentOptions.Center, false, art.Bold, FontStyles.Bold);
            StretchFull(valueText.GetComponent<RectTransform>());

            Button plus = CreateSpriteButton(rt, "PlusButton", plusSprite);
            AddLayoutElement(plus.gameObject, preferredWidth: 32, preferredHeight: 32).flexibleWidth = 0;
            ((Image)plus.targetGraphic).preserveAspect = true;

            QuantitySelectorView view = go.AddComponent<QuantitySelectorView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("minusButton").objectReferenceValue = minus;
            so.FindProperty("plusButton").objectReferenceValue = plus;
            so.FindProperty("valueText").objectReferenceValue = valueText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ------------------------------------------------------------------------ PortfolioHoldingCard

        // Child names match PortfolioPanel's required hierarchy exactly: CompanyHeaderRow
        // (CompanyBadge/CompanyBadgeText, CompanyNameText, SectorText), HoldingStatsGrid
        // (OwnedSharesStat, AverageBuyPriceStat, CurrentPriceStat, ProfitLossStat - each a
        // Label+Value pair), ActionsRow (DetailsButton, BuyMoreButton, SellButton - visual
        // left-to-right order matches the portfolio reference: buy/green, sell/red,
        // details/neutral).
        private static GameObject BuildPortfolioHoldingCardPrefab(Art art)
        {
            GameObject go = new GameObject("PortfolioHoldingCard", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(500f, 172f);
            LayoutElement cardLayoutElement = go.AddComponent<LayoutElement>();
            cardLayoutElement.preferredHeight = 172f;
            cardLayoutElement.minHeight = 172f;

            Image bg = go.AddComponent<Image>();
            bg.sprite = RoundedDark();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.06f);

            RectTransform content = CreateUIObject("Content", rt);
            StretchFull(content);
            content.offsetMin = new Vector2(14f, 12f);
            content.offsetMax = new Vector2(-14f, -12f);
            AddVerticalLayout(content.gameObject, 10f);

            // CompanyHeaderRow: NameColumn (flexible, right-aligned text) + CompanyBadge (fixed,
            // rightmost) - matches CompanyCard's proven RTL badge-on-the-right pattern.
            RectTransform headerRow = CreateUIObject("CompanyHeaderRow", content);
            AddLayoutElement(headerRow.gameObject, preferredHeight: 40);
            AddHorizontalLayout(headerRow.gameObject, 8f);

            RectTransform nameCol = CreateUIObject("NameColumn", headerRow);
            AddVerticalLayout(nameCol.gameObject, 2f, alignment: TextAnchor.MiddleRight);
            AddLayoutElement(nameCol.gameObject, flexibleWidth: 1);
            TMP_Text name = CreateText(nameCol, "CompanyNameText", "اسم الشركة", 17f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, true, art.Bold, FontStyles.Bold);
            TMP_Text sector = CreateText(nameCol, "SectorText", "القطاع", 12f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);

            RectTransform badge = CreateUIObject("CompanyBadge", headerRow);
            AddLayoutElement(badge.gameObject, preferredWidth: 36, preferredHeight: 36).flexibleWidth = 0;
            Image badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = RoundedRectSpriteFactory.GetRoundedSprite(18, 40);
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = Color.gray;
            TMP_Text badgeText = CreateText(badge, "CompanyBadgeText", "ال", 13f, Color.white, TextAlignmentOptions.Center, false, art.Bold, FontStyles.Bold);
            StretchFull(badgeText.GetComponent<RectTransform>());

            // HoldingStatsGrid: 2 rows x 2 columns of named Label+Value stat cells.
            RectTransform statsGrid = CreateUIObject("HoldingStatsGrid", content);
            AddLayoutElement(statsGrid.gameObject, preferredHeight: 64);
            AddVerticalLayout(statsGrid.gameObject, 6f);

            RectTransform statsRow1 = CreateUIObject("StatsRow1", statsGrid);
            AddLayoutElement(statsRow1.gameObject, flexibleHeight: 1);
            AddHorizontalLayout(statsRow1.gameObject, 16f);
            TMP_Text sharesVal = BuildStatCell(statsRow1, art, "OwnedSharesStat", "عدد الأسهم");
            TMP_Text avgVal = BuildStatCell(statsRow1, art, "AverageBuyPriceStat", "متوسط الشراء");

            RectTransform statsRow2 = CreateUIObject("StatsRow2", statsGrid);
            AddLayoutElement(statsRow2.gameObject, flexibleHeight: 1);
            AddHorizontalLayout(statsRow2.gameObject, 16f);
            TMP_Text currentVal = BuildStatCell(statsRow2, art, "CurrentPriceStat", "السعر الحالي");
            TMP_Text profitVal = BuildStatCell(statsRow2, art, "ProfitLossStat", "الربح / الخسارة");

            // ActionsRow
            RectTransform actionsRow = CreateUIObject("ActionsRow", content);
            AddLayoutElement(actionsRow.gameObject, preferredHeight: 40);
            AddHorizontalLayout(actionsRow.gameObject, 8f);

            Button buyMore = CreateSpriteButton(actionsRow, "BuyMoreButton", RoundedDark());
            AddLayoutElement(buyMore.gameObject, flexibleWidth: 1);
            ((Image)buyMore.targetGraphic).color = InvestmentPalette.Positive;
            TMP_Text buyMoreLabel = CreateText(buyMore.transform, "Label", "اشتري المزيد", 13f, Color.white, TextAlignmentOptions.Center, true, art.Bold, FontStyles.Bold);
            StretchFull(buyMoreLabel.GetComponent<RectTransform>());

            Button sell = CreateSpriteButton(actionsRow, "SellButton", RoundedDark());
            AddLayoutElement(sell.gameObject, flexibleWidth: 1);
            ((Image)sell.targetGraphic).color = new Color(InvestmentPalette.Negative.r, InvestmentPalette.Negative.g, InvestmentPalette.Negative.b, 0.18f);
            TMP_Text sellLabel = CreateText(sell.transform, "Label", "بيع", 13f, InvestmentPalette.Negative, TextAlignmentOptions.Center, true, art.Bold, FontStyles.Bold);
            StretchFull(sellLabel.GetComponent<RectTransform>());

            Button details = CreateSpriteButton(actionsRow, "DetailsButton", RoundedDark());
            AddLayoutElement(details.gameObject, flexibleWidth: 1);
            ((Image)details.targetGraphic).color = new Color(1f, 1f, 1f, 0.08f);
            TMP_Text detailsLabel = CreateText(details.transform, "Label", "تفاصيل", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.Center, true, art.Regular);
            StretchFull(detailsLabel.GetComponent<RectTransform>());

            PortfolioHoldingCardView view = go.AddComponent<PortfolioHoldingCardView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("companyBadge").objectReferenceValue = badgeImg;
            so.FindProperty("companyBadgeText").objectReferenceValue = badgeText;
            so.FindProperty("companyNameText").objectReferenceValue = name;
            so.FindProperty("sectorText").objectReferenceValue = sector;
            so.FindProperty("ownedSharesValue").objectReferenceValue = sharesVal;
            so.FindProperty("averageBuyPriceValue").objectReferenceValue = avgVal;
            so.FindProperty("currentPriceValue").objectReferenceValue = currentVal;
            so.FindProperty("profitLossValue").objectReferenceValue = profitVal;
            so.FindProperty("detailsButton").objectReferenceValue = details;
            so.FindProperty("buyMoreButton").objectReferenceValue = buyMore;
            so.FindProperty("sellButton").objectReferenceValue = sell;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // One named Label+Value stat cell (e.g. "OwnedSharesStat") for HoldingStatsGrid -
        // labels are always smaller/muted, values larger/bold, per spec.
        private static TMP_Text BuildStatCell(Transform parent, Art art, string cellName, string label)
        {
            RectTransform cell = CreateUIObject(cellName, parent);
            AddLayoutElement(cell.gameObject, flexibleWidth: 1);
            AddVerticalLayout(cell.gameObject, 2f, alignment: TextAnchor.MiddleRight);
            CreateText(cell, "Label", label, 11f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);
            TMP_Text value = CreateText(cell, "Value", "-", 15f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, false, art.Bold, FontStyles.Bold);
            return value;
        }


        // ------------------------------------------------------------------------ TransactionRow

        // Child names match the required hierarchy exactly: TypeBadge, CompanyInfo
        // (CompanyNameText, DateTimeText), TransactionValues (SharesText, PriceText, TotalText).
        // Visual left-to-right order in Row is TransactionValues / CompanyInfo / TypeBadge,
        // matching the same RTL convention already established for CompanyCard/
        // PortfolioHoldingCard (numbers on the left, Arabic text in the middle, badge on the
        // right edge) - the required hierarchy above is a naming/ownership list, not a strict
        // left-to-right mandate (same interpretation used for those two prefabs).
        private static GameObject BuildTransactionRowPrefab(Art art)
        {
            GameObject go = new GameObject("TransactionRow", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(500f, 82f);
            LayoutElement rowLayoutElement = go.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = 82f;
            rowLayoutElement.flexibleHeight = 0f;

            Image bg = go.AddComponent<Image>();
            bg.sprite = RoundedDark();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.06f);

            RectTransform row = CreateUIObject("Row", rt);
            StretchFull(row);
            row.offsetMin = new Vector2(14f, 10f);
            row.offsetMax = new Vector2(-14f, -10f);
            AddHorizontalLayout(row.gameObject, 10f);

            // TransactionValues (leftmost) - 3 stacked, number-led values, never Arabic-shaped as
            // a unit (each is "number + single trailing Arabic word", the already-safe pattern).
            RectTransform valuesCol = CreateUIObject("TransactionValues", row);
            AddLayoutElement(valuesCol.gameObject, preferredWidth: 130).flexibleWidth = 0;
            AddVerticalLayout(valuesCol.gameObject, 2f, alignment: TextAnchor.MiddleLeft);
            TMP_Text shares = CreateText(valuesCol, "SharesText", "0 أسهم", 12f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineLeft, false, art.Regular);
            TMP_Text price = CreateText(valuesCol, "PriceText", "0.00 ريال", 12f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineLeft, false, art.Regular);
            TMP_Text total = CreateText(valuesCol, "TotalText", "0.00 ريال", 14f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, false, art.Bold, FontStyles.Bold);

            // CompanyInfo (middle, flexible)
            RectTransform infoCol = CreateUIObject("CompanyInfo", row);
            AddLayoutElement(infoCol.gameObject, flexibleWidth: 1);
            AddVerticalLayout(infoCol.gameObject, 3f, alignment: TextAnchor.MiddleRight);
            TMP_Text company = CreateText(infoCol, "CompanyNameText", "اسم الشركة", 16f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineRight, true, art.Bold, FontStyles.Bold);
            TMP_Text dateTime = CreateText(infoCol, "DateTimeText", "2026/01/01  00:00", 11f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, false, art.Regular);

            // TypeBadge (rightmost) - Buy.png/Sell.png already bake the شراء/بيع label into the
            // badge art itself, and are already colored green/red respectively - a real Image,
            // never a generated pill + separate text label.
            RectTransform badgeRT = CreateUIObject("TypeBadge", row);
            AddLayoutElement(badgeRT.gameObject, preferredWidth: 50, preferredHeight: 26).flexibleWidth = 0;
            Image typeBadge = badgeRT.gameObject.AddComponent<Image>();
            typeBadge.sprite = art.BuyBadge;
            typeBadge.preserveAspect = true;
            typeBadge.raycastTarget = false;

            TransactionRowView view = go.AddComponent<TransactionRowView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("typeBadge").objectReferenceValue = typeBadge;
            so.FindProperty("buySprite").objectReferenceValue = art.BuyBadge;
            so.FindProperty("sellSprite").objectReferenceValue = art.SellBadge;
            so.FindProperty("companyNameText").objectReferenceValue = company;
            so.FindProperty("dateTimeText").objectReferenceValue = dateTime;
            so.FindProperty("sharesText").objectReferenceValue = shares;
            so.FindProperty("priceText").objectReferenceValue = price;
            so.FindProperty("totalText").objectReferenceValue = total;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ------------------------------------------------------------------------ SummaryRow

        private static GameObject BuildSummaryRowPrefab(Art art)
        {
            GameObject go = new GameObject("SummaryRow", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(500f, 44f);
            // Reserved height inside the auto-layout PerformanceBox - without this the row
            // collapses to zero height (childControlHeight=true) and its label/value overlap
            // every other row, which was the exact History/Summary readability bug being fixed.
            LayoutElement rowLayoutElement = go.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = 44f;
            rowLayoutElement.flexibleHeight = 0f;

            Image bg = go.AddComponent<Image>();
            bg.sprite = RoundedDark();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.05f);

            RectTransform row = CreateUIObject("Row", rt);
            StretchFull(row);
            row.offsetMin = new Vector2(16f, 4f);
            row.offsetMax = new Vector2(-16f, -4f);
            AddHorizontalLayout(row.gameObject, 8f);

            TMP_Text value = CreateText(row, "Value", "0.00", 16f, InvestmentPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, false, art.Bold, FontStyles.Bold);
            AddLayoutElement(value.gameObject, flexibleWidth: 1);
            TMP_Text label = CreateText(row, "Label", "التسمية", 14f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, art.Regular);
            AddLayoutElement(label.gameObject, flexibleWidth: 1);

            SummaryRowView view = go.AddComponent<SummaryRowView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("labelText").objectReferenceValue = label;
            so.FindProperty("valueText").objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ------------------------------------------------------------------------ InvestmentPopupHeader

        private static GameObject BuildPopupHeaderPrefab(Art art)
        {
            GameObject go = new GameObject("InvestmentPopupHeader", typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(540f, 56f);

            Button leftButton = CreateSpriteButton(rt, "LeftButton", art.CloseButton);
            RectTransform leftRT = leftButton.GetComponent<RectTransform>();
            leftRT.anchorMin = new Vector2(0f, 0.5f);
            leftRT.anchorMax = new Vector2(0f, 0.5f);
            leftRT.pivot = new Vector2(0f, 0.5f);
            leftRT.sizeDelta = new Vector2(30f, 30f);
            leftRT.anchoredPosition = new Vector2(20f, 0f);
            ((Image)leftButton.targetGraphic).preserveAspect = true;

            TMP_Text title = CreateText(rt, "Title", "برج الاستثمار", 22f, InvestmentPalette.Positive, TextAlignmentOptions.Center, true, art.Bold, FontStyles.Bold);
            RectTransform titleRT = title.GetComponent<RectTransform>();
            AnchorFraction(titleRT, 0f, 0f, 1f, 1f);

            InvestmentPopupHeaderView view = go.AddComponent<InvestmentPopupHeaderView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("leftButton").objectReferenceValue = leftButton;
            so.FindProperty("leftButtonIcon").objectReferenceValue = leftButton.targetGraphic;
            so.FindProperty("closeIconSprite").objectReferenceValue = art.CloseButton;
            so.FindProperty("backIconSprite").objectReferenceValue = art.BackButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }
    }
}
