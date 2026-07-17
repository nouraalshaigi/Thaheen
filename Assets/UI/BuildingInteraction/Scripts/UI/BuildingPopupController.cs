using System;
using StartFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BuildingInteractionSystem
{
    // Reusable popup: one instance is shown/hidden and re-populated for whichever building was
    // clicked/tapped (BuildingInteractionManager owns the single instance, so only one popup can
    // ever be open at a time).
    //
    // The whole visual hierarchy (Overlay / GenericPanel / CharityPopup / MallPopup /
    // SavingsPopup and all their children) is a real, serialized child hierarchy inside
    // BuildingPopup.prefab - hand-editable in the Inspector (Rect Transform, sprites, font
    // sizes, everything). This component only binds runtime behavior (which data populates
    // which text, which panel is active) to those already-existing references; it no longer
    // builds any UI from code. See BuildingInteractionSystem.Editor.BuildingPopupPrefabBuilder
    // for the one-shot editor step that creates/repairs that hierarchy.
    public class BuildingPopupController : MonoBehaviour
    {
        [Header("Overlay")]
        [SerializeField] private Button overlayButton;

        [Header("Generic Panel (buildings outside Charity/Mall/Savings, e.g. Investment Tower)")]
        [SerializeField] private GameObject genericPanel;
        [SerializeField] private Button genericCloseButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text iconFallbackText;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryButtonLabel;

        [Header("Generic Panel - AI Sub-Panel")]
        [Tooltip("Master switch - if false, the AI panel is never shown regardless of per-building data.")]
        [SerializeField] private bool allowAIPanel = true;
        [SerializeField] private AIServiceConfig aiServiceConfig;
        [SerializeField] private GameObject aiPanelGO;
        [SerializeField] private TMP_InputField aiInputField;
        [SerializeField] private TMP_Text aiResponseText;
        [SerializeField] private TMP_Text aiLoadingText;
        [SerializeField] private TMP_Text aiErrorText;
        [SerializeField] private Button aiSendButton;

        [Header("Transaction Popups")]
        [SerializeField] private TransactionPopupView charityPopup;
        [SerializeField] private TransactionPopupView mallPopup;
        [SerializeField] private TransactionPopupView savingsPopup;

        private BuildingInteractionManager manager;
        private BuildingData currentData;
        private IBuildingAIService aiService;
        private int requestToken;

        private void Awake()
        {
            if (overlayButton != null) overlayButton.onClick.AddListener(ClosePopup);
            if (genericCloseButton != null) genericCloseButton.onClick.AddListener(ClosePopup);
            if (primaryButton != null) primaryButton.onClick.AddListener(OnPrimaryClicked);
            if (aiSendButton != null) aiSendButton.onClick.AddListener(OnSendClicked);

            if (charityPopup != null) charityPopup.Bind(TransactionPopupView.Kind.Charity, (data, amount) => data.donatedAmount += amount, ClosePopup);
            if (mallPopup != null) mallPopup.Bind(TransactionPopupView.Kind.Mall, (data, amount) => data.spentAmount += amount, ClosePopup);
            if (savingsPopup != null) savingsPopup.Bind(TransactionPopupView.Kind.Savings, (data, amount) => data.savedAmount += amount, ClosePopup);

            gameObject.SetActive(false);
        }

        public void Initialize(BuildingInteractionManager owningManager)
        {
            manager = owningManager;
        }

        public void Show(BuildingData data)
        {
            if (data == null) return;

            // Investment Tower has its own, much larger UI system (tutorial/company list/
            // details/portfolio/etc. - see Assets/InvesmentUI) that lives as a static, initially
            // -disabled child of the same BuildingInteraction_Canvas this popup is instantiated
            // under, rather than inside this reusable popup instance. It's looked up via its own
            // singleton (not a serialized reference) because a prefab asset can't hold a direct
            // reference to a scene-only object. This popup is left closed/inactive for
            // InvestmentTower - it never shows the generic panel underneath the Investment UI.
            if (data.id == BuildingId.InvestmentTower)
            {
                if (InvestmentTowerUI.InvestmentTowerUIController.Instance != null)
                {
                    gameObject.SetActive(false);
                    InvestmentTowerUI.InvestmentTowerUIController.Instance.Open();
                }
                else
                {
                    Debug.LogWarning("BuildingPopupController: InvestmentTowerUIController.Instance is null - " +
                        "falling back to the generic popup for InvestmentTower.");
                }
                if (InvestmentTowerUI.InvestmentTowerUIController.Instance != null) return;
            }

            currentData = data;
            requestToken++;
            gameObject.SetActive(true);

            TransactionPopupView transactionView = data.id switch
            {
                BuildingId.CharityHouse => charityPopup,
                BuildingId.ShoppingMall => mallPopup,
                BuildingId.NajdiHousePiggyBank => savingsPopup,
                _ => null
            };

            // Any building besides Charity/Mall/Savings/InvestmentTower (or InvestmentTower
            // itself, if its UI singleton wasn't found - see above) keeps the generic popup.
            // Falls back to the generic popup if a transaction view reference wasn't wired up,
            // so the popup never silently does nothing.
            bool showTransaction = transactionView != null;

            if (genericPanel != null) genericPanel.SetActive(!showTransaction);
            if (charityPopup != null) charityPopup.gameObject.SetActive(transactionView == charityPopup);
            if (mallPopup != null) mallPopup.gameObject.SetActive(transactionView == mallPopup);
            if (savingsPopup != null) savingsPopup.gameObject.SetActive(transactionView == savingsPopup);

            if (showTransaction)
            {
                transactionView.RefreshOnShow();
                return;
            }

            // Guarded rather than assumed non-null: if the prefab's hierarchy hasn't been built
            // yet (see BuildingPopupPrefabBuilder), these references are null until it runs -
            // better to show an empty-but-visible popup than throw and abort Show() entirely.
            if (titleText != null) titleText.text = data.titleArabic;
            if (descriptionText != null) descriptionText.text = data.descriptionArabic;
            if (primaryButtonLabel != null)
                primaryButtonLabel.text = string.IsNullOrEmpty(data.primaryButtonTextArabic) ? "دخول" : data.primaryButtonTextArabic;

            if (iconImage != null && iconFallbackText != null)
            {
                if (data.icon != null)
                {
                    iconImage.sprite = data.icon;
                    iconImage.gameObject.SetActive(true);
                    iconFallbackText.gameObject.SetActive(false);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                    iconFallbackText.gameObject.SetActive(true);
                    iconFallbackText.text = string.IsNullOrEmpty(data.iconFallbackLetter) ? "؟" : data.iconFallbackLetter;
                }
            }

            if (aiPanelGO != null)
            {
                bool showAI = allowAIPanel && data.aiPanelEnabledByDefault;
                aiPanelGO.SetActive(showAI);
            }
            ResetAIPanel();
        }

        public void Hide()
        {
            requestToken++;
            gameObject.SetActive(false);
            currentData = null;
        }

        private void ResetAIPanel()
        {
            if (aiInputField != null) aiInputField.text = string.Empty;
            if (aiResponseText != null) aiResponseText.text = string.Empty;
            SetAILoading(false);
            SetAIError(string.Empty);
        }

        private void OnPrimaryClicked()
        {
            // Building-specific gameplay hook for the future. Today, if this building's AI
            // panel is showing, "دخول" simply focuses the question field.
            if (aiPanelGO != null && aiPanelGO.activeSelf && aiInputField != null)
            {
                aiInputField.Select();
                aiInputField.ActivateInputField();
            }
        }

        private async void OnSendClicked()
        {
            if (currentData == null || aiInputField == null) return;

            int myToken = ++requestToken;
            string question = aiInputField.text;

            SetAIError(string.Empty);
            SetAILoading(true);

            if (aiService == null) aiService = AIServiceLocator.Resolve(aiServiceConfig);

            BuildingAIRequest request = new BuildingAIRequest
            {
                buildingId = currentData.id,
                buildingName = currentData.titleArabic,
                playerQuestion = question,
                financialContext = PlayerFinancialContext.Empty
            };

            BuildingAIResponse response;
            try
            {
                response = await aiService.AskAsync(request);
            }
            catch (Exception ex)
            {
                response = BuildingAIResponse.Failure("حدث خطأ غير متوقع. حاول مرة أخرى.");
                Debug.LogException(ex);
            }

            // The popup may have been closed or re-shown for a different building while this
            // request was in flight - only apply the result if it's still the same request.
            if (myToken != requestToken) return;

            SetAILoading(false);

            if (response.success)
            {
                aiResponseText.text = string.IsNullOrEmpty(response.suggestedAction)
                    ? response.aiResponse
                    : response.aiResponse + "\n\n" + response.suggestedAction;
            }
            else
            {
                SetAIError(string.IsNullOrEmpty(response.errorMessage) ? "تعذر الحصول على رد. حاول مرة أخرى." : response.errorMessage);
            }
        }

        private void SetAILoading(bool loading)
        {
            if (aiLoadingText != null) aiLoadingText.gameObject.SetActive(loading);
            if (aiSendButton != null) aiSendButton.interactable = !loading;
        }

        private void SetAIError(string message)
        {
            if (aiErrorText == null) return;
            aiErrorText.text = message;
            aiErrorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void ClosePopup()
        {
            if (manager != null) manager.ClosePopup();
            else Hide();
        }
    }
}
