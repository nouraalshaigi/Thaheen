using UnityEngine;
using UnityEngine.UI;

namespace StartFlow
{
    // First screen of the onboarding flow - purely a "start" gate, no data collection.
    public class WelcomeScreenController : MonoBehaviour
    {
        [SerializeField] private StartFlowController flowController;
        [SerializeField] private Button startButton;

        private bool clicked;

        private void Awake()
        {
            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);
        }

        private void OnStartClicked()
        {
            if (clicked) return;
            clicked = true;

            if (startButton != null) startButton.interactable = false;

            flowController.GoToNextScreen();
        }
    }
}
