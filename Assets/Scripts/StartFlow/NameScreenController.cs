using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StartFlow
{
    public class NameScreenController : MonoBehaviour
    {
        [SerializeField] private StartFlowController flowController;
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button continueButton;

        private bool advancing;

        private void Awake()
        {
            if (nameInputField != null)
                nameInputField.onValueChanged.AddListener(_ => Validate());

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);

            Validate();
        }

        private bool TryGetValidName(out string trimmedName)
        {
            trimmedName = nameInputField != null ? nameInputField.text.Trim() : string.Empty;
            return trimmedName.Length > 0;
        }

        private void Validate()
        {
            bool isValid = TryGetValidName(out _);
            if (continueButton != null) continueButton.interactable = isValid;
        }

        private void OnContinueClicked()
        {
            if (advancing) return;
            if (!TryGetValidName(out string trimmedName)) return;

            advancing = true;

            // Save the raw logical name exactly as entered (trimmed only).
            PlayerDataManager.GetOrCreate().Data.playerName = trimmedName;

            flowController.GoToNextScreen();
        }
    }
}
