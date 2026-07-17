using System.Globalization;
using StartFlow.Arabic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StartFlow
{
    public class MonthlyMoneyScreenController : MonoBehaviour
    {
        private const int MinAmount = 50;
        private const int MaxAmount = 1000;
        private static readonly string ExceedsMaxMessage = ArabicTextShaper.Shape("لحظة! الحد الأعلى للميزانية الشهرية هو ٠٠٠١ ريال");

        [SerializeField] private StartFlowController flowController;
        [SerializeField] private TMP_InputField amountField;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text validationMessage;

        private bool advancing;
        private bool touched;

        private void Awake()
        {
            if (amountField != null) amountField.onValueChanged.AddListener(_ => { touched = true; Validate(); });
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);

            Validate();
        }

        private bool TryGetAmount(out int amount)
        {
            amount = 0;
            if (amountField == null) return false;

            string raw = amountField.text.Trim();
            return !string.IsNullOrEmpty(raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        }

        private void Validate()
        {
            bool hasValue = TryGetAmount(out int amount);
            bool isValid = hasValue && amount >= MinAmount && amount <= MaxAmount;

            if (continueButton != null) continueButton.interactable = isValid;

            if (validationMessage != null)
                validationMessage.text = (touched && hasValue && amount > MaxAmount) ? ExceedsMaxMessage : string.Empty;
        }

        private void OnContinueClicked()
        {
            if (advancing) return;
            touched = true;
            if (!TryGetAmount(out int amount) || amount < MinAmount || amount > MaxAmount) { Validate(); return; }

            advancing = true;

            PlayerSetupData data = PlayerDataManager.GetOrCreate().Data;
            data.monthlyMoney = amount;
            data.currentAvailableMoney = amount;

            // Last screen in the flow - GoToNextScreen() falls through to LoadGameScene()
            // (OGscene) since there is no screen after this one.
            flowController.GoToNextScreen();
        }
    }
}
