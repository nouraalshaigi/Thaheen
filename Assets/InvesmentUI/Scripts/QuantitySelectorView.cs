using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI
{
    // Reusable +/- share-quantity stepper (InvestmentTowerUI/Prefabs/QuantitySelector.prefab).
    // Used both for buying (CompanyDetailsPanel) and selling (SellSharesPanel) - the owning
    // panel supplies bounds and quick-amount meaning (e.g. "الكل"/"النصف" only make sense in the
    // sell context, so this component only knows how to set/clamp/report a value, never what a
    // quick button represents).
    public class QuantitySelectorView : MonoBehaviour
    {
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private TMP_Text valueText;

        private int value;
        private int minValue;
        private int maxValue = int.MaxValue;
        private Action<int> onChanged;
        private bool bound;

        public int Value => value;

        // Guarantees exactly one listener per button - see CompanyCardView.Bind for the same
        // reasoning. bound also guards the whole body so this never re-runs per company switch
        // (this one QuantitySelectorView instance is shared across every company shown in
        // CompanyDetailsPanel/SellSharesPanel - only onChanged's target company-specific state
        // changes, never the listener itself).
        public void Bind(Action<int> changedCallback)
        {
            onChanged = changedCallback;
            if (bound) return;
            bound = true;

            if (minusButton != null)
            {
                minusButton.onClick.RemoveAllListeners();
                minusButton.onClick.AddListener(() => SetValue(value - 1));
            }
            if (plusButton != null)
            {
                plusButton.onClick.RemoveAllListeners();
                plusButton.onClick.AddListener(() => SetValue(value + 1));
            }
        }

        public void SetBounds(int min, int max)
        {
            minValue = min;
            maxValue = Mathf.Max(min, max);
            SetValue(value);
        }

        public void SetValue(int newValue)
        {
            value = Mathf.Clamp(newValue, minValue, maxValue);
            Refresh();
            onChanged?.Invoke(value);
        }

        private void Refresh()
        {
            if (valueText != null)
                valueText.text = $"{value} " + ArabicTextUtility.Format("سهم");

            if (minusButton != null) minusButton.interactable = value > minValue;
            if (plusButton != null) plusButton.interactable = value < maxValue;
        }
    }
}
