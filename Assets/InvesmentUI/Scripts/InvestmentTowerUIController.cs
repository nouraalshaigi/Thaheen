using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using InvestmentTowerUI.MarketData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI
{
    public enum PortfolioTab { Holdings, History, Summary }

    [Serializable]
    public struct QuickQuantityButtonRef
    {
        public Button button;
        public int amount;
        public QuickQuantityMode mode;
    }

    public enum QuickQuantityMode { Fixed, All, Half }

    // Opens/closes the Investment Tower UI and drives navigation between its panels. Built
    // entirely from code-constructed, serialized references (see
    // InvestmentTowerUI.Editor.InvestmentTowerUIBuilder) - never builds any UI itself. All
    // market data comes from InvestmentSampleData (temporary, isolated - see that file).
    public class InvestmentTowerUIController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button overlayCloseButton;
        [Tooltip("Every panel header's close (X) button that should close the whole UI - i.e. every header except the drill-down panels (CompanyDetails/SellShares), which use back-navigation instead.")]
        [SerializeField] private Button[] closeButtons;

        [Header("Wallet (Temporary Testing)")]
        [Tooltip("Used ONLY as a fallback Available Balance while StartFlow.PlayerDataManager has not been created yet (e.g. opening this scene directly, without going through StartFlowScene first). Once onboarding runs, PlayerDataManager.Data.currentAvailableMoney (the monthly amount the player entered) takes over automatically and this value is ignored - see InvestmentWallet.")]
        [SerializeField] private float startingBalance = 0f;

        [Header("Tutorial")]
        [SerializeField] private GameObject tutorialStep1Panel;
        [SerializeField] private Button tutorialStep1NextButton;
        [SerializeField] private GameObject tutorialStep2Panel;
        [SerializeField] private Button tutorialStep2StartButton;
        [SerializeField] private InvestmentPopupHeaderView tutorialStep1Header;
        [SerializeField] private InvestmentPopupHeaderView tutorialStep2Header;

        [Header("Company List")]
        [SerializeField] private GameObject companyListPanel;
        [SerializeField] private InvestmentPopupHeaderView companyListHeader;
        [SerializeField] private TMP_Text availableBalanceText;
        [SerializeField] private Transform companyListContent;
        [Tooltip("Exactly 3 static, individually-named cards under CompanyListContent (AlinmaCompanyButton/AramcoCompanyButton/STCCompanyButton) - order must match InvestmentSampleData.Companies. Never runtime-instantiated.")]
        [SerializeField] private CompanyCardView[] companyListCards;
        [SerializeField] private Button openPortfolioButton;
        [SerializeField] private GameObject openPortfolioButtonRoot;
        [Tooltip("Doubles as the live market-refresh status line (loading/updated/failed) - reverts to its reference disclaimer text a few seconds after each status message.")]
        [SerializeField] private TMP_Text footerNoteText;

        [Header("Company Details")]
        [SerializeField] private GameObject companyDetailsPanel;
        [SerializeField] private InvestmentPopupHeaderView companyDetailsHeader;
        [SerializeField] private TMP_Text detailsNameText;
        [SerializeField] private TMP_Text detailsSectorText;
        [SerializeField] private TMP_Text detailsLogoText;
        [SerializeField] private Image detailsLogoBackground;
        [SerializeField] private TMP_Text detailsRiskText;
        [SerializeField] private TMP_Text detailsChangeText;
        [SerializeField] private TMP_Text detailsPriceText;
        [SerializeField] private TMP_Text detailsDescriptionText;
        [SerializeField] private Button predictUpButton;
        [SerializeField] private Image predictUpBackground;
        [SerializeField] private Button predictDownButton;
        [SerializeField] private Image predictDownBackground;
        [SerializeField] private QuantitySelectorView detailsQuantitySelector;
        [SerializeField] private QuickQuantityButtonRef[] detailsQuickButtons;
        [Tooltip("QuickAmountRow stays always-active with a fixed reserved LayoutElement height - shown/hidden purely via this CanvasGroup so toggling it never changes the panel's layout.")]
        [SerializeField] private CanvasGroup detailsQuickAmountGroup;
        [SerializeField] private TMP_Text detailsPricePerShareText;
        [SerializeField] private TMP_Text detailsShareCountText;
        [SerializeField] private GameObject detailsSummaryRow;
        [Tooltip("SummaryRow stays always-active with a fixed reserved LayoutElement height - shown/hidden purely via this CanvasGroup so toggling it never changes the panel's layout.")]
        [SerializeField] private CanvasGroup detailsSummaryGroup;
        [SerializeField] private Button investButton;

        [Header("Purchase Confirmation")]
        [SerializeField] private GameObject purchaseConfirmationPanel;
        [SerializeField] private TMP_Text confirmNameText;
        [SerializeField] private TMP_Text confirmSectorText;
        [SerializeField] private TMP_Text confirmLogoText;
        [SerializeField] private Image confirmLogoBackground;
        [SerializeField] private TMP_Text confirmPricePerShareText;
        [SerializeField] private TMP_Text confirmShareCountText;
        [SerializeField] private TMP_Text confirmTotalCostText;
        [SerializeField] private TMP_Text confirmRemainingBalanceText;
        [SerializeField] private Button confirmPurchaseButton;
        [SerializeField] private Button confirmBackButton;

        [Header("Portfolio")]
        [SerializeField] private GameObject portfolioPanel;
        [SerializeField] private Button portfolioTabButton;
        [SerializeField] private Button historyTabButton;
        [SerializeField] private Button summaryTabButton;
        [SerializeField] private GameObject portfolioTabContent;
        [SerializeField] private GameObject historyTabContent;
        [SerializeField] private GameObject summaryTabContent;
        [SerializeField] private GameObject portfolioEmptyState;
        [SerializeField] private Button portfolioEmptyStateButton;
        [Tooltip("HoldingsState - PortfolioSummaryRow + SectionTitle + HoldingsList, shown only while shares are owned.")]
        [SerializeField] private GameObject portfolioHoldingsState;
        [SerializeField] private TMP_Text portfolioValueText;
        [SerializeField] private TMP_Text portfolioRemainingBalanceText;
        [SerializeField] private TMP_Text portfolioProfitLossText;
        [SerializeField] private Transform portfolioHoldingsContent;
        [SerializeField] private PortfolioHoldingCardView portfolioHoldingCardPrefab;
        [SerializeField] private GameObject historyScrollView;
        [SerializeField] private Transform historyContent;
        [SerializeField] private TransactionRowView transactionRowPrefab;
        [SerializeField] private GameObject historyEmptyState;
        [SerializeField] private TMP_Text historyEmptyTitleText;
        [SerializeField] private TMP_Text historyEmptySubtitleText;
        [SerializeField] private GameObject summaryScrollView;
        [Tooltip("MainStatsGrid's 4 stat cards.")]
        [SerializeField] private TMP_Text summaryTotalInvestedText;
        [SerializeField] private TMP_Text summaryCurrentValueText;
        [SerializeField] private TMP_Text summaryRemainingBalanceText;
        [SerializeField] private TMP_Text summaryTotalProfitLossText;
        [Tooltip("PerformanceBox's 3 static rows.")]
        [SerializeField] private SummaryRowView summaryBestInvestmentRow;
        [SerializeField] private SummaryRowView summaryWorstInvestmentRow;
        [SerializeField] private SummaryRowView summaryTransactionsCountRow;
        [Tooltip("Shown instead of SummaryScrollView when no transaction has ever been made.")]
        [SerializeField] private GameObject summaryEmptyState;
        [SerializeField] private TMP_Text summaryEmptyText;

        [Header("Sell Shares")]
        [SerializeField] private GameObject sellSharesPanel;
        [SerializeField] private TMP_Text sellNameText;
        [SerializeField] private TMP_Text sellOwnedText;
        [SerializeField] private TMP_Text sellLogoText;
        [SerializeField] private Image sellLogoBackground;
        [SerializeField] private TMP_Text sellCurrentPriceText;
        [SerializeField] private QuantitySelectorView sellQuantitySelector;
        [SerializeField] private QuickQuantityButtonRef[] sellQuickButtons;
        [SerializeField] private TMP_Text sellExpectedValueText;
        [SerializeField] private TMP_Text sellRemainingSharesText;
        [SerializeField] private Button confirmSellButton;
        [SerializeField] private Button sellBackButton;

        private readonly List<GameObject> allPanels = new List<GameObject>();
        private InvestmentCompany selectedCompany;
        private InvestmentHolding selectedHolding;
        private bool predictedUp;
        private bool bound;
        private Action backAction;

        // Isolated market-data layer (Assets/InvesmentUI/Scripts/MarketData) - looked up on this
        // same GameObject since it can't be wired as a scene-only serialized reference from a
        // prefab either. Optional: the UI still works on InvestmentSampleData's built-in sample
        // prices if no SaudiMarketService component is present.
        private SaudiMarketService marketService;
        private const string FooterDisclaimerArabic = "الأسعار تتغير حسب حركة السوق • الاستثمار فيه ربح وخسارة";
        private Coroutine footerRevertRoutine;

        // Looked up by BuildingPopupController (a prefab instance, which can't hold a
        // serialized reference to this scene-only object) to redirect InvestmentTower clicks
        // here instead of the generic popup. Only one Investment Tower UI ever exists in the
        // scene, so a plain singleton is sufficient - no separate manager needed.
        public static InvestmentTowerUIController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            InvestmentWallet.SeedFallback(startingBalance);

            allPanels.Add(tutorialStep1Panel);
            allPanels.Add(tutorialStep2Panel);
            allPanels.Add(companyListPanel);
            allPanels.Add(companyDetailsPanel);
            allPanels.Add(purchaseConfirmationPanel);
            allPanels.Add(portfolioPanel);
            allPanels.Add(sellSharesPanel);

            marketService = GetComponent<SaudiMarketService>();
            if (marketService != null) marketService.OnQuotesUpdated += HandleQuotesUpdated;

            // portfolioHoldingsContent only ever holds dynamically-instantiated
            // PortfolioHoldingCard instances (created on demand by RefreshHoldingsList) - never
            // static content. Clearing it here guarantees a clean slate at the start of every
            // Play session, regardless of any leftover child - most notably an Editor-only Edit
            // Preview sample card that wasn't cleaned up in time - so the real card
            // RefreshHoldingsList creates from InvestmentSampleData.Holdings is never joined by
            // a stray duplicate. portfolioCards (used by RefreshHoldingsList to track/reuse real
            // cards across refreshes) starts empty regardless, so this never discards anything
            // that would otherwise have been reused.
            if (portfolioHoldingsContent != null)
                for (int i = portfolioHoldingsContent.childCount - 1; i >= 0; i--)
                    Destroy(portfolioHoldingsContent.GetChild(i).gameObject);

            BindOnce();

            // Resets which single panel is active to match a fresh, never-opened session,
            // regardless of whatever an Edit Mode preview command left active in the Editor -
            // this only ever toggles GameObject.SetActive on the existing 7 panels (see
            // ShowPanel), never touches a RectTransform and never rebuilds anything.
            ShowPanel(tutorialStep1Panel);

            // 'root' itself must stay active - Awake() (and the Instance assignment above) can
            // only ever run once, when the GameObject first becomes active in the scene. If root
            // were deactivated here (as it used to be), this component would never wake up again
            // on the NEXT scene load, Instance would stay null forever, and
            // BuildingPopupController.Show() would silently fall back to the generic popup for
            // BuildingId.InvestmentTower - which was the actual cause of the old popup opening
            // instead of this UI. The overlay is the real visible/interactive "is it open" layer
            // and is what actually gets shown/hidden - see Open()/Close().
            SetOverlayActive(false);
        }

        // DimOverlay - looked up via overlayCloseButton (a Button that already lives directly on
        // that GameObject, per InvestmentTowerUIBuilder.Build) rather than a new serialized field,
        // so no Inspector rewiring is needed for this fix.
        private GameObject OverlayGameObject => overlayCloseButton != null ? overlayCloseButton.gameObject : null;

        private void SetOverlayActive(bool active)
        {
            GameObject overlay = OverlayGameObject;
            if (overlay != null) overlay.SetActive(active);
            else Debug.LogWarning("InvestmentTowerUIController: 'overlayCloseButton' is not assigned - cannot show/hide the Investment Tower overlay.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (marketService != null) marketService.OnQuotesUpdated -= HandleQuotesUpdated;
        }

        // ------------------------------------------------------------------------------ open/close

        public void Open()
        {
            if (OverlayGameObject == null)
            {
                Debug.LogWarning("InvestmentTowerUIController: 'overlayCloseButton' is not assigned - cannot open.");
                return;
            }

            SetOverlayActive(true);
            backAction = null;
            ShowPanel(tutorialStep1Panel);

            // Fetches the 3 tracked symbols (subject to SaudiMarketService's own cooldown) -
            // never per-frame, only on open. No-op if no SaudiMarketService is present.
            if (marketService != null) marketService.RequestOpenRefresh();
        }

        // ------------------------------------------------------------------------------ market data

        // Fired by SaudiMarketService.OnQuotesUpdated - refreshes only whichever panel is
        // currently visible (each panel already re-reads InvestmentCompany.price/
        // dailyChangePercent, updated in place by SaudiMarketService, so no panel needs to know
        // about market data itself). Never issues or touches web requests directly.
        private void HandleQuotesUpdated()
        {
            if (marketService != null) ShowFooterStatus(marketService.StatusText);

            if (companyListPanel != null && companyListPanel.activeSelf)
                RefreshCompanyList();

            if (companyDetailsPanel != null && companyDetailsPanel.activeSelf)
                RefreshSelectedCompanyPrices();

            if (portfolioPanel != null && portfolioPanel.activeSelf)
                RefreshPortfolio();
        }

        private void ShowFooterStatus(string message)
        {
            if (footerNoteText == null || string.IsNullOrEmpty(message)) return;

            footerNoteText.text = message;
            if (footerRevertRoutine != null) StopCoroutine(footerRevertRoutine);
            footerRevertRoutine = StartCoroutine(RevertFooterAfter(5f));
        }

        private IEnumerator RevertFooterAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (footerNoteText != null) footerNoteText.text = ArabicTextUtility.Format(FooterDisclaimerArabic);
            footerRevertRoutine = null;
        }

        public void Close()
        {
            SetOverlayActive(false);
        }

        private void ShowPanel(GameObject panel)
        {
            foreach (GameObject p in allPanels)
                if (p != null) p.SetActive(p == panel);
        }

        // ------------------------------------------------------------------------------ binding

        private void BindOnce()
        {
            if (bound) return;
            bound = true;

            SafeListen(overlayCloseButton, Close, nameof(overlayCloseButton));
            if (closeButtons != null)
                foreach (Button b in closeButtons)
                    SafeListen(b, Close, nameof(closeButtons));

            SafeListen(tutorialStep1NextButton, () => ShowPanel(tutorialStep2Panel), nameof(tutorialStep1NextButton));
            SafeListen(tutorialStep2StartButton, OpenCompanyList, nameof(tutorialStep2StartButton));
            if (tutorialStep1Header != null) { tutorialStep1Header.SetMode(false); SafeListen(tutorialStep1Header.LeftButton, Close, "tutorialStep1Header.LeftButton"); }
            if (tutorialStep2Header != null) { tutorialStep2Header.SetMode(false); SafeListen(tutorialStep2Header.LeftButton, Close, "tutorialStep2Header.LeftButton"); }

            if (companyListHeader != null) { companyListHeader.SetMode(false); SafeListen(companyListHeader.LeftButton, Close, "companyListHeader.LeftButton"); }
            SafeListen(openPortfolioButton, OpenPortfolio, nameof(openPortfolioButton));
            if (companyListCards != null)
                foreach (CompanyCardView card in companyListCards)
                    if (card != null) card.Bind(SelectCompanyFromList);

            if (companyDetailsHeader != null) SafeListen(companyDetailsHeader.LeftButton, GoBack, "companyDetailsHeader.LeftButton");
            SafeListen(predictUpButton, () => SetPrediction(true), nameof(predictUpButton));
            SafeListen(predictDownButton, () => SetPrediction(false), nameof(predictDownButton));
            if (detailsQuantitySelector != null) detailsQuantitySelector.Bind(RefreshQuantitySummary);
            BindQuickButtons(detailsQuickButtons, detailsQuantitySelector, null);
            SafeListen(investButton, OpenPurchaseConfirmationFromDetails, nameof(investButton));

            SafeListen(confirmPurchaseButton, ConfirmPurchase, nameof(confirmPurchaseButton));
            SafeListen(confirmBackButton, GoBack, nameof(confirmBackButton));

            SafeListen(portfolioTabButton, () => SwitchPortfolioTab(PortfolioTab.Holdings), nameof(portfolioTabButton));
            SafeListen(historyTabButton, () => SwitchPortfolioTab(PortfolioTab.History), nameof(historyTabButton));
            SafeListen(summaryTabButton, () => SwitchPortfolioTab(PortfolioTab.Summary), nameof(summaryTabButton));
            SafeListen(portfolioEmptyStateButton, OpenCompanyList, nameof(portfolioEmptyStateButton));

            if (sellQuantitySelector != null) sellQuantitySelector.Bind(OnSellQuantityChanged);
            BindQuickButtons(sellQuickButtons, sellQuantitySelector, () => selectedHolding?.shares ?? 0);
            SafeListen(confirmSellButton, ConfirmSale, nameof(confirmSellButton));
            SafeListen(sellBackButton, GoBack, nameof(sellBackButton));
        }

        private void BindQuickButtons(QuickQuantityButtonRef[] refs, QuantitySelectorView selector, Func<int> ownedProvider)
        {
            if (refs == null || selector == null) return;
            foreach (QuickQuantityButtonRef q in refs)
            {
                if (q.button == null) continue;
                QuickQuantityButtonRef captured = q;
                captured.button.onClick.AddListener(() =>
                {
                    int target = captured.mode switch
                    {
                        QuickQuantityMode.All => ownedProvider != null ? ownedProvider() : captured.amount,
                        QuickQuantityMode.Half => ownedProvider != null ? Mathf.Max(1, ownedProvider() / 2) : captured.amount,
                        _ => captured.amount,
                    };
                    selector.SetValue(target);
                });
            }
        }

        private static void SafeListen(Button button, Action action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogWarning($"InvestmentTowerUIController: '{fieldName}' is not assigned in the Inspector.");
                return;
            }
            button.onClick.AddListener(() => action());
        }

        // ------------------------------------------------------------------------------ company list

        private void OpenCompanyList()
        {
            backAction = null;
            RefreshCompanyList();
            ShowPanel(companyListPanel);
        }

        private void RefreshCompanyList()
        {
            if (availableBalanceText != null)
                availableBalanceText.text = InvestmentSampleData.AvailableBalance.ToString("0", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");

            if (openPortfolioButtonRoot != null)
                openPortfolioButtonRoot.SetActive(InvestmentSampleData.HasAnyHoldings);

            if (companyListCards == null) return;

            for (int i = 0; i < companyListCards.Length && i < InvestmentSampleData.Companies.Count; i++)
            {
                CompanyCardView card = companyListCards[i];
                if (card == null) continue;
                InvestmentCompany company = InvestmentSampleData.Companies[i];
                card.SetCompany(company);
                card.SetSelected(company == selectedCompany);
            }
        }

        private void SelectCompanyFromList(InvestmentCompany company)
        {
            if (company == null) return;
            OpenCompanyDetails(company.id);
        }

        // ------------------------------------------------------------------------------ company details

        // Public, ID-based entry point - each of the 3 company buttons routes here (via
        // SelectCompanyFromList) so there is exactly one place that opens CompanyDetailsPanel
        // for a given company. Never closes or deactivates root/DimOverlay - only swaps which
        // child panel is shown (see ShowPanel).
        public void OpenCompanyDetails(string companyId)
        {
            InvestmentCompany company = InvestmentSampleData.Companies.Find(c => c.id == companyId);
            if (company == null)
            {
                Debug.LogWarning($"InvestmentTowerUIController: no company with id '{companyId}'.");
                return;
            }

            if (companyListCards != null)
                foreach (CompanyCardView card in companyListCards)
                    if (card != null) card.SetSelected(false);

            OpenCompanyDetails(company, OpenCompanyList);
        }

        private void OpenCompanyDetails(InvestmentCompany company, Action returnTo)
        {
            if (company == null) return;

            // Explicitly clear every field to a neutral state before binding the newly selected
            // company - see ResetCompanyDetailsFields. This is the SAME existing
            // CompanyDetailsPanel and its SAME existing children every time (no
            // instantiate/rebuild, no RectTransform touched) - only their .text/.color/active
            // values change.
            ResetCompanyDetailsFields();

            selectedCompany = company;
            backAction = returnTo;

            ArabicTextUtility.Apply(detailsNameText, company.nameArabic);
            ArabicTextUtility.Apply(detailsSectorText, company.sectorArabic);
            // company.logoLetters is sometimes Arabic ("ال"/"أر") and sometimes Latin ("STC") -
            // Apply() shapes and sets the correct direction per-value instead of assuming one.
            ArabicTextUtility.Apply(detailsLogoText, company.logoLetters);
            if (detailsLogoBackground != null) detailsLogoBackground.color = company.logoColor;
            ArabicTextUtility.Apply(detailsRiskText, company.riskLevelArabic);
            FormatChangePercent(detailsChangeText, company.dailyChangePercent);
            if (detailsPriceText != null)
                ArabicTextUtility.Apply(detailsPriceText, company.price.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ر"));
            ArabicTextUtility.Apply(detailsDescriptionText, company.descriptionArabic);

            predictedUp = true;
            SetPrediction(true);

            if (detailsQuantitySelector != null)
            {
                detailsQuantitySelector.SetBounds(0, 100000);
                detailsQuantitySelector.SetValue(0);
            }

            ShowPanel(companyDetailsPanel);
        }

        // Clears only the previous company's data/visual state - text to empty, the logo badge
        // tint to a neutral color, change-percent color to neutral, prediction backgrounds to
        // unselected, purchase summary hidden/blanked, invest button disabled - before the next
        // company is bound above. Every field here is unconditionally reassigned again
        // immediately afterward regardless, so this changes nothing about the final rendered
        // result; it exists so no previously-selected company's value can ever remain visible,
        // even transiently, and so "reset" and "populate" stay two clearly separate steps.
        // Never touches a RectTransform, anchor, position, size, scale, spacing, padding, font,
        // or button listener - only the dynamic values already listed above.
        private void ResetCompanyDetailsFields()
        {
            if (detailsNameText != null) detailsNameText.text = string.Empty;
            if (detailsSectorText != null) detailsSectorText.text = string.Empty;
            if (detailsLogoText != null) detailsLogoText.text = string.Empty;
            if (detailsLogoBackground != null) detailsLogoBackground.color = Color.white;
            if (detailsRiskText != null) detailsRiskText.text = string.Empty;
            if (detailsChangeText != null)
            {
                detailsChangeText.text = string.Empty;
                detailsChangeText.color = InvestmentPalette.TextMuted;
            }
            if (detailsPriceText != null) detailsPriceText.text = string.Empty;
            if (detailsDescriptionText != null) detailsDescriptionText.text = string.Empty;

            if (predictUpBackground != null) predictUpBackground.color = InvestmentPalette.CardNormal;
            if (predictDownBackground != null) predictDownBackground.color = InvestmentPalette.CardNormal;

            if (detailsPricePerShareText != null) detailsPricePerShareText.text = string.Empty;
            if (detailsShareCountText != null) detailsShareCountText.text = string.Empty;
            // QuickAmountRow/SummaryRow stay active (fixed reserved layout space) - hidden via
            // CanvasGroup only, never SetActive, so this never changes the panel's layout.
            SetHiddenGroup(detailsQuickAmountGroup);
            SetHiddenGroup(detailsSummaryGroup);
            if (investButton != null) investButton.interactable = false;
        }

        // Runtime counterparts of InvestmentTowerUIBuilder.SetHiddenGroup/SetVisibleGroup (Editor
        // code can't be referenced from player/runtime code, so these can't be literally shared).
        private static void SetHiddenGroup(CanvasGroup group)
        {
            if (group == null) return;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private static void SetVisibleGroup(CanvasGroup group)
        {
            if (group == null) return;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        // "+1.20%" / "-0.40%" / "0.00%" with neutral-muted color at exactly 0 - shared by
        // OpenCompanyDetails and RefreshSelectedCompanyPrices so a live market-data update uses
        // the exact same formatting as the initial open.
        private static void FormatChangePercent(TMP_Text target, float percent)
        {
            if (target == null) return;
            bool positive = percent > 0f;
            bool negative = percent < 0f;
            target.text = (positive ? "+" : negative ? "-" : "") + Mathf.Abs(percent).ToString("0.00", CultureInfo.InvariantCulture) + "%";
            target.color = positive ? InvestmentPalette.Positive : negative ? InvestmentPalette.Negative : InvestmentPalette.TextMuted;
        }

        // Refreshes only price-derived text on the already-open CompanyDetailsPanel when a live
        // quote arrives - never resets the quantity selector or navigation state (unlike
        // OpenCompanyDetails, which is only for a fresh open). No visual/hierarchy change.
        private void RefreshSelectedCompanyPrices()
        {
            if (selectedCompany == null) return;

            FormatChangePercent(detailsChangeText, selectedCompany.dailyChangePercent);
            if (detailsPriceText != null)
                detailsPriceText.text = selectedCompany.price.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ر");
            if (detailsQuantitySelector != null && detailsQuantitySelector.Value > 0)
                RefreshQuantitySummary(detailsQuantitySelector.Value);
        }

        private void SetPrediction(bool up)
        {
            predictedUp = up;
            if (predictUpBackground != null) predictUpBackground.color = up ? InvestmentPalette.CardSelected : InvestmentPalette.CardNormal;
            if (predictDownBackground != null) predictDownBackground.color = !up ? new Color(InvestmentPalette.Negative.r, InvestmentPalette.Negative.g, InvestmentPalette.Negative.b, 0.2f) : InvestmentPalette.CardNormal;
        }

        // Narrow refresh for CompanyDetailsPanel's quantity stepper + purchase summary only -
        // bound once as QuantitySelectorView's onChanged callback (see BindOnce) and fired on
        // every Plus/Minus click. Never calls OpenCompanyDetails/ShowPanel/any builder method,
        // never touches a RectTransform/LayoutElement/LayoutGroup/ContentSizeFitter, never
        // instantiates or destroys a UI object - only reassigns the existing
        // detailsPricePerShareText/detailsShareCountText/investButton values below, always via
        // direct "=" assignment, always formatted fresh from the raw quantity/price/balance
        // values (never by re-shaping the text already on screen).
        //
        // CompanyDetailsPanel's current layout has no separate "total cost" or "remaining
        // balance" text field (those exist only on PurchaseConfirmationPanel, populated later by
        // OpenPurchaseConfirmation) - totalCost below is used only for the invest button's
        // affordability check, matching the panel's existing real fields; adding a new display
        // field would require a new child object, which is out of scope for this fix.
        private void RefreshQuantitySummary(int quantity)
        {
            bool hasQuantity = quantity > 0;

            // QuickAmountRow/SummaryRow stay active at all times (fixed reserved LayoutElement
            // space, set at build time) - only their CanvasGroup toggles, so content's
            // VerticalLayoutGroup never recalculates and nothing above/below them can shift or
            // overlap.
            if (hasQuantity)
            {
                SetVisibleGroup(detailsQuickAmountGroup);
                SetVisibleGroup(detailsSummaryGroup);
            }
            else
            {
                SetHiddenGroup(detailsQuickAmountGroup);
                SetHiddenGroup(detailsSummaryGroup);
            }

            if (detailsPricePerShareText != null && selectedCompany != null)
                detailsPricePerShareText.text = selectedCompany.price.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");
            if (detailsShareCountText != null)
                detailsShareCountText.text = quantity.ToString(CultureInfo.InvariantCulture);

            float totalCost = selectedCompany != null ? selectedCompany.price * quantity : 0f;
            if (investButton != null)
                investButton.interactable = hasQuantity && totalCost <= InvestmentSampleData.AvailableBalance;
        }

        // ------------------------------------------------------------------------------ purchase confirmation

        private void OpenPurchaseConfirmationFromDetails()
        {
            if (selectedCompany == null || detailsQuantitySelector == null) return;
            int quantity = detailsQuantitySelector.Value;
            if (quantity <= 0) return;

            Action detailsBack = backAction;
            OpenPurchaseConfirmation(selectedCompany, quantity, () => OpenCompanyDetails(selectedCompany, detailsBack));
        }

        private int pendingQuantity;
        private float purchaseConfirmationShownAt;

        // Fired after a purchase is actually committed (i.e. after RecordPurchase succeeds in
        // ConfirmPurchase - never on a blocked/insufficient-funds attempt). Lets external systems
        // (see Dhaheen.DhaheenGameTracker) observe completed investment decisions without this
        // controller needing to know anything about them.
        public event Action<InvestmentCompany, int, float> PurchaseConfirmed;

        private void OpenPurchaseConfirmation(InvestmentCompany company, int quantity, Action returnTo)
        {
            selectedCompany = company;
            pendingQuantity = quantity;
            backAction = returnTo;
            purchaseConfirmationShownAt = Time.unscaledTime;

            float totalCost = company.price * quantity;
            float remaining = InvestmentSampleData.AvailableBalance - totalCost;

            ArabicTextUtility.Apply(confirmNameText, company.nameArabic);
            ArabicTextUtility.Apply(confirmSectorText, company.sectorArabic);
            // company.logoLetters is sometimes Arabic ("ال"/"أر") and sometimes Latin ("STC").
            ArabicTextUtility.Apply(confirmLogoText, company.logoLetters);
            if (confirmLogoBackground != null) confirmLogoBackground.color = company.logoColor;
            if (confirmPricePerShareText != null) confirmPricePerShareText.text = company.price.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");
            if (confirmShareCountText != null) confirmShareCountText.text = quantity + " " + ArabicTextUtility.Format("سهم");
            if (confirmTotalCostText != null) confirmTotalCostText.text = totalCost.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");
            if (confirmRemainingBalanceText != null) confirmRemainingBalanceText.text = remaining.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");
            if (confirmPurchaseButton != null) confirmPurchaseButton.interactable = remaining >= 0f;

            ShowPanel(purchaseConfirmationPanel);
        }

        private void ConfirmPurchase()
        {
            if (selectedCompany == null || pendingQuantity <= 0) return;

            InvestmentCompany purchasedCompany = selectedCompany;
            int purchasedQuantity = pendingQuantity;
            InvestmentHolding holding = InvestmentSampleData.RecordPurchase(purchasedCompany, purchasedQuantity);
            if (holding != null)
            {
                float responseTimeSeconds = Mathf.Max(0f, Time.unscaledTime - purchaseConfirmationShownAt);
                PurchaseConfirmed?.Invoke(purchasedCompany, purchasedQuantity, responseTimeSeconds);
            }
            OpenCompanyList();
        }

        // ------------------------------------------------------------------------------ portfolio

        private void OpenPortfolio()
        {
            backAction = OpenCompanyList;
            RefreshPortfolio();
            ShowPanel(portfolioPanel);
            SwitchPortfolioTab(PortfolioTab.Holdings);
        }

        private void RefreshPortfolio()
        {
            bool hasHoldings = InvestmentSampleData.HasAnyHoldings;

            if (portfolioEmptyState != null) portfolioEmptyState.SetActive(!hasHoldings);
            if (portfolioHoldingsState != null) portfolioHoldingsState.SetActive(hasHoldings);

            float totalValue = 0f, totalInvested = 0f, totalProfit = 0f;
            foreach (InvestmentHolding h in InvestmentSampleData.Holdings)
            {
                totalValue += h.CurrentValue;
                totalInvested += h.TotalInvested;
                totalProfit += h.ProfitLoss;
            }

            if (portfolioValueText != null)
                portfolioValueText.text = totalValue.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ر");
            if (portfolioRemainingBalanceText != null)
                portfolioRemainingBalanceText.text = InvestmentSampleData.AvailableBalance.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ر");
            if (portfolioProfitLossText != null)
            {
                bool positive = totalProfit > 0f;
                bool negative = totalProfit < 0f;
                string sign = positive ? "+" : negative ? "-" : "";
                portfolioProfitLossText.text = $"{sign}{Mathf.Abs(totalProfit):0.00} " + ArabicTextUtility.Format("ر");
                portfolioProfitLossText.color = positive ? InvestmentPalette.Positive : negative ? InvestmentPalette.Negative : InvestmentPalette.TextMuted;
            }

            if (hasHoldings) RefreshHoldingsList();
            RefreshHistory();
            RefreshSummary(totalValue, totalInvested, totalProfit);
        }

        private readonly List<PortfolioHoldingCardView> portfolioCards = new List<PortfolioHoldingCardView>();

        private void RefreshHoldingsList()
        {
            if (portfolioHoldingsContent == null || portfolioHoldingCardPrefab == null) return;

            while (portfolioCards.Count < InvestmentSampleData.Holdings.Count)
            {
                PortfolioHoldingCardView card = Instantiate(portfolioHoldingCardPrefab, portfolioHoldingsContent);
                // No separate "quick buy more" flow exists yet - both Details and Buy More open
                // CompanyDetailsPanel (where استثمر الآن lets them add to the position), matching
                // the only purchase flow this UI actually has.
                card.Bind(
                    h => OpenCompanyDetails(h.company, OpenPortfolio),
                    h => OpenCompanyDetails(h.company, OpenPortfolio),
                    h => OpenSellShares(h));
                portfolioCards.Add(card);
            }

            for (int i = 0; i < portfolioCards.Count; i++)
            {
                bool active = i < InvestmentSampleData.Holdings.Count;
                portfolioCards[i].gameObject.SetActive(active);
                if (active) portfolioCards[i].SetHolding(InvestmentSampleData.Holdings[i]);
            }
        }

        private readonly List<TransactionRowView> historyRows = new List<TransactionRowView>();

        private void RefreshHistory()
        {
            bool hasTransactions = InvestmentSampleData.Transactions.Count > 0;
            if (historyEmptyState != null) historyEmptyState.SetActive(!hasTransactions);
            if (historyScrollView != null) historyScrollView.SetActive(hasTransactions);
            if (historyContent == null || transactionRowPrefab == null) return;

            while (historyRows.Count < InvestmentSampleData.Transactions.Count)
                historyRows.Add(Instantiate(transactionRowPrefab, historyContent));

            for (int i = 0; i < historyRows.Count; i++)
            {
                bool active = i < InvestmentSampleData.Transactions.Count;
                historyRows[i].gameObject.SetActive(active);
                if (active) historyRows[i].SetTransaction(InvestmentSampleData.Transactions[i]);
            }
        }

        private void RefreshSummary(float totalValue, float totalInvested, float totalProfit)
        {
            // "If no shares have ever been purchased" - Transactions only ever grows, so this
            // stays correct even if the player later sells every share back down to zero.
            bool hasEverTraded = InvestmentSampleData.Transactions.Count > 0;
            if (summaryEmptyState != null) summaryEmptyState.SetActive(!hasEverTraded);
            if (summaryScrollView != null) summaryScrollView.SetActive(hasEverTraded);
            if (!hasEverTraded) return;

            string currency = ArabicTextUtility.Format("ر");

            if (summaryTotalInvestedText != null)
                summaryTotalInvestedText.text = totalInvested.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency;
            if (summaryCurrentValueText != null)
                summaryCurrentValueText.text = totalValue.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency;
            if (summaryRemainingBalanceText != null)
                summaryRemainingBalanceText.text = InvestmentSampleData.AvailableBalance.ToString("0.00", CultureInfo.InvariantCulture) + " " + currency;
            if (summaryTotalProfitLossText != null)
            {
                bool positive = totalProfit > 0f;
                bool negative = totalProfit < 0f;
                string sign = positive ? "+" : negative ? "-" : "";
                summaryTotalProfitLossText.text = $"{sign}{Mathf.Abs(totalProfit):0.00} " + currency;
                summaryTotalProfitLossText.color = positive ? InvestmentPalette.Positive : negative ? InvestmentPalette.Negative : InvestmentPalette.TextMuted;
            }

            InvestmentHolding best = null, worst = null;
            foreach (InvestmentHolding h in InvestmentSampleData.Holdings)
            {
                if (best == null || h.ProfitLossPercent > best.ProfitLossPercent) best = h;
                if (worst == null || h.ProfitLossPercent < worst.ProfitLossPercent) worst = h;
            }

            if (summaryBestInvestmentRow != null)
                summaryBestInvestmentRow.Set("أفضل استثمار", best != null ? best.company.nameArabic : "—");
            if (summaryWorstInvestmentRow != null)
                summaryWorstInvestmentRow.Set("أضعف استثمار", worst != null ? worst.company.nameArabic : "—");
            if (summaryTransactionsCountRow != null)
                summaryTransactionsCountRow.Set("عدد العمليات", InvestmentSampleData.Transactions.Count.ToString(CultureInfo.InvariantCulture));
        }

        private void SwitchPortfolioTab(PortfolioTab tab)
        {
            if (portfolioTabContent != null) portfolioTabContent.SetActive(tab == PortfolioTab.Holdings);
            if (historyTabContent != null) historyTabContent.SetActive(tab == PortfolioTab.History);
            if (summaryTabContent != null) summaryTabContent.SetActive(tab == PortfolioTab.Summary);

            SetTabButtonActive(portfolioTabButton, tab == PortfolioTab.Holdings);
            SetTabButtonActive(historyTabButton, tab == PortfolioTab.History);
            SetTabButtonActive(summaryTabButton, tab == PortfolioTab.Summary);
        }

        private static void SetTabButtonActive(Button button, bool active)
        {
            if (button == null) return;
            Image img = button.targetGraphic as Image;
            if (img != null) img.color = active ? InvestmentPalette.CardSelected : InvestmentPalette.CardNormal;

            Outline border = button.GetComponent<Outline>();
            if (border != null) border.enabled = active;
        }

        // ------------------------------------------------------------------------------ sell shares

        private void OpenSellShares(InvestmentHolding holding)
        {
            if (holding == null) return;

            selectedHolding = holding;
            backAction = OpenPortfolio;

            ArabicTextUtility.Apply(sellNameText, holding.company.nameArabic);
            // sellOwnedText is now the dynamic VALUE only ("5 أسهم") - the Arabic label
            // ("الأسهم المملوكة") is a separate, static TMP field baked at build time (see
            // InvestmentTowerUIBuilder.Portfolio.cs/BuildSellShares), never combined into one
            // runtime string.
            if (sellOwnedText != null)
                sellOwnedText.text = $"{holding.shares} " + ArabicTextUtility.Format("أسهم");
            // holding.company.logoLetters is sometimes Arabic ("ال"/"أر") and sometimes Latin ("STC").
            ArabicTextUtility.Apply(sellLogoText, holding.company.logoLetters);
            if (sellLogoBackground != null) sellLogoBackground.color = holding.company.logoColor;
            if (sellCurrentPriceText != null) sellCurrentPriceText.text = holding.company.price.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");

            if (sellQuantitySelector != null)
            {
                sellQuantitySelector.SetBounds(0, holding.shares);
                sellQuantitySelector.SetValue(0);
            }

            ShowPanel(sellSharesPanel);
        }

        private void OnSellQuantityChanged(int quantity)
        {
            if (selectedHolding == null) return;

            float expectedValue = selectedHolding.company.price * quantity;
            int remainingShares = selectedHolding.shares - quantity;

            if (sellExpectedValueText != null) sellExpectedValueText.text = expectedValue.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");
            if (sellRemainingSharesText != null) sellRemainingSharesText.text = remainingShares.ToString(CultureInfo.InvariantCulture);
            if (confirmSellButton != null) confirmSellButton.interactable = quantity > 0 && quantity <= selectedHolding.shares;
        }

        private void ConfirmSale()
        {
            if (selectedHolding == null || sellQuantitySelector == null) return;
            int quantity = sellQuantitySelector.Value;
            if (quantity <= 0) return;

            InvestmentSampleData.RecordSale(selectedHolding, quantity);
            OpenPortfolio();
        }

        // ------------------------------------------------------------------------------ navigation

        private void GoBack()
        {
            Action action = backAction;
            if (action != null) action();
            else OpenCompanyList();
        }
    }
}
