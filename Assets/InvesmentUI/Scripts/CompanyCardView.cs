using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI
{
    // Reusable row for CompanyListPanel (InvesmentUI/Prefabs/CompanyCard.prefab). Child names in
    // the prefab: Background, CompanyAccentArt, SelectionBorder, CompanyBadge, CompanyNameText,
    // SectorText, CurrentPriceText, CurrencyText, DailyChangeText, TrendIcon. The card root
    // itself is the ClickableButton (see clickableButton field) - the whole card is clickable.
    public class CompanyCardView : MonoBehaviour
    {
        [Header("Background / Selection")]
        [Tooltip("The card's base fill - editable Image, tint changes on selection.")]
        [SerializeField] private Image background;
        [Tooltip("Border/outline shown only while this card is the selected company - hidden otherwise.")]
        [SerializeField] private Image selectionBorder;

        [Header("Per-Company Accent Art (exact provided button assets)")]
        [Tooltip("Sits above Background, below the readable text/icons. Sprite is swapped per company in SetCompany, matched by InvestmentCompany.id - see the 3 sprite fields below. Source: Assets/InvesmentUI/CompanyListPanel.")]
        [SerializeField] private Image companyAccentArt;
        [Tooltip("Assign CompanyListPanel/Inma_Button.png")]
        [SerializeField] private Sprite alinmaAccentSprite;
        [Tooltip("Assign CompanyListPanel/Aramco_Button.png")]
        [SerializeField] private Sprite aramcoAccentSprite;
        [Tooltip("Assign CompanyListPanel/STC_Button.png")]
        [SerializeField] private Sprite stcAccentSprite;

        [Header("Company Identity")]
        [Tooltip("The logo badge circle. No per-company logo artwork was provided, so it's tinted with the company's brand color and shows CompanyLogoText (e.g. 'ال') as a letter mark, matching the references.")]
        [SerializeField] private Image companyLogo;
        [SerializeField] private TMP_Text companyLogoText;
        [SerializeField] private TMP_Text companyNameText;
        [SerializeField] private TMP_Text sectorText;

        [Header("Price")]
        [SerializeField] private TMP_Text currentPriceText;
        [Tooltip("The 'ريال' unit label next to the price - a real TMP text, never baked into an image.")]
        [SerializeField] private TMP_Text currencyText;

        [Header("Daily Change")]
        [SerializeField] private TMP_Text dailyChangeText;
        [Tooltip("Up.png for positive movement, Down.png for negative - assigned from Assets/InvesmentUI/Icons.")]
        [SerializeField] private Image trendIcon;
        [SerializeField] private Sprite trendUpSprite;
        [SerializeField] private Sprite trendDownSprite;

        [Header("Owned Shares (hidden until the player owns this company)")]
        [SerializeField] private TMP_Text ownedSharesText;

        [Header("Interaction")]
        [SerializeField] private Button clickableButton;

        private InvestmentCompany company;
        private Action<InvestmentCompany> onClicked;

        // Guarantees exactly one listener on clickableButton no matter how many times Bind is
        // called - RemoveAllListeners first clears anything else wired to it (including any
        // stray persistent listener from an older/manual edit), so a company click can never
        // also fire an unrelated handler (e.g. the header's close button).
        public void Bind(Action<InvestmentCompany> clickedCallback)
        {
            onClicked = clickedCallback;
            if (clickableButton == null) return;
            clickableButton.onClick.RemoveAllListeners();
            clickableButton.onClick.AddListener(() => onClicked?.Invoke(company));
        }

        public void SetCompany(InvestmentCompany data)
        {
            company = data;
            bool positive = data.dailyChangePercent > 0f;
            bool negative = data.dailyChangePercent < 0f;

            ArabicTextUtility.Apply(companyNameText, data.nameArabic);
            ArabicTextUtility.Apply(sectorText, data.sectorArabic);
            if (currentPriceText != null) currentPriceText.text = data.price.ToString("0.00", CultureInfo.InvariantCulture);
            if (currencyText != null) currencyText.text = ArabicTextUtility.Format("ريال");

            if (dailyChangeText != null)
            {
                dailyChangeText.text = (positive ? "+" : negative ? "-" : "") +
                    Mathf.Abs(data.dailyChangePercent).ToString("0.00", CultureInfo.InvariantCulture) + "%";
                dailyChangeText.color = positive ? InvestmentPalette.Positive : negative ? InvestmentPalette.Negative : InvestmentPalette.TextMuted;
            }
            if (trendIcon != null)
            {
                Sprite s = positive ? trendUpSprite : negative ? trendDownSprite : null;
                if (s != null) { trendIcon.sprite = s; trendIcon.gameObject.SetActive(true); }
                else trendIcon.gameObject.SetActive(false);
            }
            if (companyLogo != null) companyLogo.color = data.logoColor;
            // data.logoLetters is sometimes Arabic ("ال"/"أر") and sometimes Latin ("STC").
            ArabicTextUtility.Apply(companyLogoText, data.logoLetters);

            if (companyAccentArt != null)
            {
                Sprite accent = data.id switch
                {
                    "alinma" => alinmaAccentSprite,
                    "aramco" => aramcoAccentSprite,
                    "stc" => stcAccentSprite,
                    _ => null,
                };
                companyAccentArt.sprite = accent;
                companyAccentArt.gameObject.SetActive(accent != null);
            }

            InvestmentHolding holding = InvestmentSampleData.FindHolding(data);
            if (ownedSharesText != null)
            {
                bool owned = holding != null && holding.shares > 0;
                ownedSharesText.gameObject.SetActive(owned);
                if (owned) ownedSharesText.text = holding.shares + " " + ArabicTextUtility.Format("سهم");
            }
        }

        public void SetSelected(bool selected)
        {
            if (background != null)
                background.color = selected ? InvestmentPalette.CardSelected : InvestmentPalette.CardNormal;
            if (selectionBorder != null)
                selectionBorder.gameObject.SetActive(selected);
        }
    }
}
