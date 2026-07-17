using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI
{
    // Reusable owned-position card for PortfolioPanel's Holdings list
    // (InvesmentUI/Prefabs/PortfolioHoldingCard.prefab). Child names in the prefab:
    // CompanyHeaderRow (CompanyBadge/CompanyBadgeText, CompanyNameText, SectorText),
    // HoldingStatsGrid (OwnedSharesStat, AverageBuyPriceStat, CurrentPriceStat, ProfitLossStat -
    // each Label+Value), ActionsRow (DetailsButton, BuyMoreButton, SellButton).
    public class PortfolioHoldingCardView : MonoBehaviour
    {
        [Header("Company Header")]
        [SerializeField] private Image companyBadge;
        [SerializeField] private TMP_Text companyBadgeText;
        [SerializeField] private TMP_Text companyNameText;
        [SerializeField] private TMP_Text sectorText;

        [Header("Holding Stats")]
        [SerializeField] private TMP_Text ownedSharesValue;
        [SerializeField] private TMP_Text averageBuyPriceValue;
        [SerializeField] private TMP_Text currentPriceValue;
        [SerializeField] private TMP_Text profitLossValue;

        [Header("Actions")]
        [SerializeField] private Button detailsButton;
        [SerializeField] private Button buyMoreButton;
        [SerializeField] private Button sellButton;

        private InvestmentHolding holding;
        private Action<InvestmentHolding> onDetails;
        private Action<InvestmentHolding> onBuyMore;
        private Action<InvestmentHolding> onSell;

        // Guarantees exactly one listener per button no matter how many times Bind runs - see
        // CompanyCardView.Bind for the same reasoning.
        public void Bind(Action<InvestmentHolding> details, Action<InvestmentHolding> buyMore, Action<InvestmentHolding> sell)
        {
            onDetails = details;
            onBuyMore = buyMore;
            onSell = sell;

            if (detailsButton != null)
            {
                detailsButton.onClick.RemoveAllListeners();
                detailsButton.onClick.AddListener(() => onDetails?.Invoke(holding));
            }
            if (buyMoreButton != null)
            {
                buyMoreButton.onClick.RemoveAllListeners();
                buyMoreButton.onClick.AddListener(() => onBuyMore?.Invoke(holding));
            }
            if (sellButton != null)
            {
                sellButton.onClick.RemoveAllListeners();
                sellButton.onClick.AddListener(() => onSell?.Invoke(holding));
            }
        }

        public void SetHolding(InvestmentHolding data)
        {
            holding = data;
            InvestmentCompany c = data.company;

            if (companyBadge != null) companyBadge.color = c.logoColor;
            // c.logoLetters is sometimes Arabic ("ال"/"أر") and sometimes Latin ("STC").
            ArabicTextUtility.Apply(companyBadgeText, c.logoLetters);
            ArabicTextUtility.Apply(companyNameText, c.nameArabic);
            ArabicTextUtility.Apply(sectorText, c.sectorArabic);

            if (ownedSharesValue != null) ownedSharesValue.text = data.shares.ToString(CultureInfo.InvariantCulture);
            if (averageBuyPriceValue != null) averageBuyPriceValue.text = FormatMoney(data.averagePrice);
            if (currentPriceValue != null) currentPriceValue.text = FormatMoney(c.price);

            if (profitLossValue != null)
            {
                float profit = data.ProfitLoss;
                bool positive = profit > 0f;
                bool negative = profit < 0f;
                string sign = positive ? "+" : negative ? "-" : "";
                profitLossValue.text = $"{sign}{Mathf.Abs(profit):0.00} ({sign}{Mathf.Abs(data.ProfitLossPercent):0.0}%)";
                profitLossValue.color = positive ? InvestmentPalette.Positive : negative ? InvestmentPalette.Negative : InvestmentPalette.TextMuted;
            }
        }

        private static string FormatMoney(float value) =>
            value.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ر");
    }
}
