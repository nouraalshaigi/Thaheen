using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StartFlow
{
    public class GoalScreenController : MonoBehaviour
    {
        [SerializeField] private StartFlowController flowController;
        [SerializeField] private TMP_InputField goalNameField;
        [SerializeField] private TMP_InputField goalAmountField;
        [SerializeField] private Button continueButton;

        private bool advancing;

        private void Awake()
        {
            if (goalNameField != null) goalNameField.onValueChanged.AddListener(_ => Validate());
            if (goalAmountField != null) goalAmountField.onValueChanged.AddListener(_ => Validate());
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);

            Validate();
        }

        private bool HasName => goalNameField != null && !string.IsNullOrWhiteSpace(goalNameField.text);

        private bool TryGetAmount(out float amount)
        {
            amount = 0f;
            if (goalAmountField == null) return false;

            string raw = goalAmountField.text.Trim();
            return !string.IsNullOrEmpty(raw)
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out amount)
                && amount > 0f;
        }

        private void Validate()
        {
            bool hasName = HasName;
            bool hasAmount = TryGetAmount(out _);
            if (continueButton != null) continueButton.interactable = hasName && hasAmount;
        }

        private void OnContinueClicked()
        {
            if (advancing) return;
            if (!TryGetAmount(out float amount)) return;
            if (!HasName) return;

            advancing = true;

            PlayerSetupData data = PlayerDataManager.GetOrCreate().Data;
            data.financialGoal = goalNameField.text.Trim();
            data.goalTargetAmount = amount;

            flowController.GoToNextScreen();
        }
    }
}
