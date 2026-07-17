using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI
{
    // Reusable row for the History tab (InvesmentUI/Prefabs/TransactionRow.prefab). Child names
    // in the prefab: TypeBadge, CompanyInfo (CompanyNameText, DateTimeText), TransactionValues
    // (SharesText, PriceText, TotalText).
    public class TransactionRowView : MonoBehaviour
    {
        [Header("Type")]
        [Tooltip("Buy.png for a buy row (green), Sell.png for a sell row (red) - both already bake the شراء/بيع label into the badge art itself.")]
        [SerializeField] private Image typeBadge;
        [SerializeField] private Sprite buySprite;
        [SerializeField] private Sprite sellSprite;

        [Header("Company Info")]
        [SerializeField] private TMP_Text companyNameText;
        [SerializeField] private TMP_Text dateTimeText;

        [Header("Transaction Values")]
        [SerializeField] private TMP_Text sharesText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text totalText;

        public void SetTransaction(InvestmentTransaction transaction)
        {
            bool isBuy = transaction.type == InvestmentTransactionType.Buy;

            if (typeBadge != null)
            {
                Sprite s = isBuy ? buySprite : sellSprite;
                if (s != null) { typeBadge.sprite = s; typeBadge.gameObject.SetActive(true); }
                else typeBadge.gameObject.SetActive(false);
            }

            ArabicTextUtility.Apply(companyNameText, transaction.company.nameArabic);

            // Pure date/time digits - no Arabic content, direct assignment only.
            if (dateTimeText != null)
            {
                dateTimeText.text = transaction.timestamp.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) +
                    "  " + transaction.timestamp.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            }

            if (sharesText != null)
                sharesText.text = transaction.shares.ToString(CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("أسهم");
            if (priceText != null)
                priceText.text = transaction.pricePerShare.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");
            if (totalText != null)
                totalText.text = transaction.TotalValue.ToString("0.00", CultureInfo.InvariantCulture) + " " + ArabicTextUtility.Format("ريال");
        }
    }
}
