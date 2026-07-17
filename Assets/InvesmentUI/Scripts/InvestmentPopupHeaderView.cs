using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI
{
    // Reusable panel header (InvesmentUI/Prefabs/InvestmentPopupHeader.prefab): centered title +
    // a single left-side button that is either a close (X) or a back (‹) depending on the panel,
    // matching the references (top-level panels show X, drill-down panels show ‹).
    public class InvestmentPopupHeaderView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button leftButton;
        [SerializeField] private Image leftButtonIcon;
        [SerializeField] private Sprite closeIconSprite;
        [SerializeField] private Sprite backIconSprite;

        public Button LeftButton => leftButton;

        public void SetTitle(string shapedArabicTitle)
        {
            if (titleText != null) titleText.text = shapedArabicTitle;
        }

        public void SetMode(bool showBack)
        {
            if (leftButtonIcon == null) return;
            Sprite sprite = showBack ? backIconSprite : closeIconSprite;
            if (sprite != null) leftButtonIcon.sprite = sprite;
        }
    }
}
