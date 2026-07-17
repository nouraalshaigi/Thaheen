using TMPro;
using UnityEngine;

namespace InvestmentTowerUI
{
    // Reusable label/value row for the Summary tab (InvesmentUI/Prefabs/SummaryRow.prefab).
    public class SummaryRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;

        public void Set(string labelArabic, string value, Color? valueColor = null)
        {
            if (labelText != null) labelText.text = ArabicTextUtility.Format(labelArabic);
            if (valueText != null)
            {
                // value is sometimes a money amount ("142.00 ر", left-to-right) and sometimes a
                // company name ("الإنماء", right-to-left, e.g. for "أفضل استثمار"/"أضعف
                // استثمار") - Apply() shapes and sets the correct direction for whichever it is,
                // instead of assuming a single fixed direction for this shared field.
                ArabicTextUtility.Apply(valueText, value);
                if (valueColor.HasValue) valueText.color = valueColor.Value;
            }
        }
    }
}
