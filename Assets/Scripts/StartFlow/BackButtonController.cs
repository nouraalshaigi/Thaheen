using UnityEngine;
using UnityEngine.UI;

namespace StartFlow
{
    // Wires a screen's back button to StartFlowController.GoToPreviousScreen() at runtime.
    // Deliberately not wired via Button.onClick.AddListener() inside the Editor build script
    // that constructs the scene - a listener added there is a non-persistent UnityEvent call
    // and is silently dropped when the scene is saved/reloaded, so the button would work only
    // within that one Editor session and do nothing afterwards. Wiring it here in Awake(),
    // using a BindPrivate'd (persistent, serialized) flowController reference, matches every
    // other screen controller's pattern and survives scene save/reload normally.
    public class BackButtonController : MonoBehaviour
    {
        [SerializeField] private StartFlowController flowController;
        [SerializeField] private Button backButton;

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        private void OnBackClicked()
        {
            if (flowController != null)
                flowController.GoToPreviousScreen();
        }
    }
}
