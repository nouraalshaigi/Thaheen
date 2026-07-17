using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StartFlow
{
    // Orchestrates the linear Welcome -> Name -> Goal -> MonthlyMoney -> OGscene flow. Each
    // screen owns its own validation/logic; this only handles which screen is visible and the
    // fade transition between them, plus the final scene load.
    public class StartFlowController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup[] screensInOrder;
        [SerializeField] private float fadeDuration = 0.35f;
        [SerializeField] private string gameSceneName = "OGscene";

        private int currentIndex = -1;
        private bool isTransitioning;

        private void Awake()
        {
            PlayerDataManager.GetOrCreate();

            foreach (CanvasGroup group in screensInOrder)
            {
                if (group == null) continue;
                group.gameObject.SetActive(true);
                SetGroupVisible(group, false);
            }
        }

        private void Start()
        {
            ShowScreenImmediate(0);
        }

        public void GoToNextScreen()
        {
            if (isTransitioning) return;

            int next = currentIndex + 1;
            if (next >= screensInOrder.Length)
            {
                LoadGameScene();
                return;
            }

            StartCoroutine(TransitionTo(next));
        }

        public void GoToPreviousScreen()
        {
            if (isTransitioning || currentIndex <= 0) return;
            StartCoroutine(TransitionTo(currentIndex - 1));
        }

        private void ShowScreenImmediate(int index)
        {
            for (int i = 0; i < screensInOrder.Length; i++)
                SetGroupVisible(screensInOrder[i], i == index);

            currentIndex = index;
        }

        private IEnumerator TransitionTo(int nextIndex)
        {
            isTransitioning = true;

            CanvasGroup current = currentIndex >= 0 ? screensInOrder[currentIndex] : null;
            CanvasGroup next = screensInOrder[nextIndex];

            if (current != null)
            {
                yield return Fade(current, 1f, 0f);
                current.interactable = false;
                current.blocksRaycasts = false;
            }

            next.interactable = false;
            next.blocksRaycasts = false;
            yield return Fade(next, 0f, 1f);
            next.interactable = true;
            next.blocksRaycasts = true;

            currentIndex = nextIndex;
            isTransitioning = false;
        }

        private IEnumerator Fade(CanvasGroup group, float from, float to)
        {
            float t = 0f;
            float duration = Mathf.Max(0.01f, fadeDuration);
            group.alpha = from;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }

            group.alpha = to;
        }

        private static void SetGroupVisible(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private void LoadGameScene()
        {
            if (PlayerDataManager.Instance != null)
                PlayerDataManager.Instance.TrySaveFallback();

            // A brand-new game is starting here (not just a mid-game scene change) - clear any
            // decisions the Dhaheen tracker carried over from a previous session. See
            // Dhaheen.DhaheenGameTracker.StartNewSession for why this exact call site was chosen.
            Dhaheen.DhaheenGameTracker.GetOrCreate().StartNewSession();

            SceneManager.LoadScene(gameSceneName);
        }
    }
}
