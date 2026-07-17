using System;
using System.Globalization;
using StartFlow;
using StartFlow.Arabic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BuildingInteractionSystem
{
    // One compact, centered transactional modal (Charity / Mall / Savings). The UI itself is a
    // real, serialized child hierarchy inside BuildingPopup.prefab (background/close button/
    // input/quick-amount buttons/confirm button/validation message - all hand-editable in the
    // Inspector); this component only binds runtime behavior to those already-existing
    // references. See BuildingInteractionSystem.Editor.BuildingPopupPrefabBuilder for the
    // editor step that creates/repairs that hierarchy (manual-only - see that file's header).
    //
    // Reads and writes StartFlow.PlayerDataManager.Data directly; there is no separate wallet/
    // data system here by design (see BuildingPopupController for why).
    public class TransactionPopupView : MonoBehaviour
    {
        public enum Kind { Charity, Mall, Savings }

        [Serializable]
        public struct QuickAmountButtonRef
        {
            public Button button;
            public int amount;
        }

        private static readonly Color MutedTextColor = new Color32(0xA9, 0xB6, 0xC4, 0xFF);
        private static readonly Color WarningColor = new Color32(0xE0, 0x6A, 0x6A, 0xFF);
        private static readonly Color SuccessColor = new Color32(0x3D, 0xB8, 0x9C, 0xFF);

        [Header("Shared Elements (see hierarchy under this GameObject)")]
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_InputField amountInput;
        [SerializeField] private QuickAmountButtonRef[] quickAmountButtons;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text validationMessage;

        [Header("Savings Only - Goal Progress")]
        [SerializeField] private TMP_Text currentSavedText;
        [SerializeField] private TMP_Text goalTargetText;
        [SerializeField] private TMP_Text remainingAmountText;
        [SerializeField] private TMP_Text progressPercentText;
        [SerializeField] private Image progressFillImage;

        private Kind kind;
        private Action<PlayerSetupData, float> addAmount;
        private Action onCloseRequested;
        private bool touched;
        private bool bound;
        private float shownAtTime;

        // Fired after a transaction is actually committed (i.e. after addAmount runs at the end
        // of OnConfirmClicked - never on a cancelled/invalid attempt). Lets external systems
        // (see Dhaheen.DhaheenGameTracker) observe completed Savings/Mall/Charity decisions
        // without this view needing to know anything about them.
        public event Action<Kind, float, float> Confirmed;

        // Wires runtime behavior (validation, quick-amount fill, confirm, close) to the
        // already-built prefab hierarchy. Everything about how the popup looks - sprites, font,
        // sizes, positions, baked button labels - lives in the prefab and is never touched here.
        public void Bind(Kind popupKind, Action<PlayerSetupData, float> amountAdder, Action closeRequested)
        {
            if (bound) return;
            bound = true;

            kind = popupKind;
            addAmount = amountAdder;
            onCloseRequested = closeRequested;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => onCloseRequested?.Invoke());
            }

            if (amountInput != null)
            {
                amountInput.onValueChanged.RemoveAllListeners();
                amountInput.onValueChanged.AddListener(_ => { touched = true; Validate(); });
            }

            if (quickAmountButtons != null)
            {
                foreach (QuickAmountButtonRef quick in quickAmountButtons)
                {
                    if (quick.button == null) continue;
                    int capturedAmount = quick.amount;
                    quick.button.onClick.RemoveAllListeners();
                    quick.button.onClick.AddListener(() =>
                    {
                        touched = true;
                        if (amountInput != null) amountInput.text = capturedAmount.ToString(CultureInfo.InvariantCulture);
                    });
                }
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }
        }

        public void RefreshOnShow()
        {
            touched = false;
            shownAtTime = Time.unscaledTime;
            if (amountInput != null) amountInput.text = string.Empty;
            Validate();
        }

        private bool TryGetAmount(out int amount)
        {
            amount = 0;
            if (amountInput == null) return false;
            string raw = amountInput.text.Trim();
            return !string.IsNullOrEmpty(raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        }

        private void Validate()
        {
            PlayerSetupData data = PlayerDataManager.GetOrCreate().Data;
            bool hasValue = TryGetAmount(out int amount);
            bool isValid = hasValue && amount > 0 && amount <= data.currentAvailableMoney;

            if (confirmButton != null) confirmButton.interactable = isValid;

            if (validationMessage != null)
            {
                if (touched && hasValue && amount <= 0)
                {
                    validationMessage.text = ArabicTextShaper.Shape("أدخل مبلغًا أكبر من صفر");
                    validationMessage.color = WarningColor;
                }
                else if (touched && hasValue && amount > data.currentAvailableMoney)
                {
                    validationMessage.text = ArabicTextShaper.Shape("المبلغ يتجاوز رصيدك المتاح");
                    validationMessage.color = WarningColor;
                }
                else if (kind == Kind.Savings && data.goalTargetAmount > 0f && data.savedAmount >= data.goalTargetAmount)
                {
                    // Small positive completion state once the savings goal has been reached.
                    validationMessage.text = ArabicTextShaper.Shape("لقد حققت هدفك الادخاري!");
                    validationMessage.color = SuccessColor;
                }
                else
                {
                    validationMessage.text = ArabicTextShaper.Shape(
                        $"الرصيد المتاح: {ToArabicIndicDigits(FormatAmount(data.currentAvailableMoney))} ريال");
                    validationMessage.color = MutedTextColor;
                }
            }

            RefreshGoalProgress(data);
        }

        private void RefreshGoalProgress(PlayerSetupData data)
        {
            if (kind != Kind.Savings) return;

            float target = Mathf.Max(0f, data.goalTargetAmount);
            float saved = Mathf.Max(0f, data.savedAmount);
            float remaining = Mathf.Max(0f, target - saved);
            // Clamp01 guarantees the fill (and reported percentage) never exceeds 100%, even if
            // savedAmount ever ends up above goalTargetAmount.
            float percent = target > 0f ? Mathf.Clamp01(saved / target) : 0f;

            if (currentSavedText != null)
                currentSavedText.text = ArabicTextShaper.Shape($"المدخر: {ToArabicIndicDigits(FormatAmount(saved))} ريال");

            if (goalTargetText != null)
                goalTargetText.text = ArabicTextShaper.Shape($"الهدف: {ToArabicIndicDigits(FormatAmount(target))} ريال");

            if (remainingAmountText != null)
                remainingAmountText.text = ArabicTextShaper.Shape($"{ToArabicIndicDigits(FormatAmount(remaining))} ريال");

            if (progressPercentText != null)
                progressPercentText.text = $"{Mathf.RoundToInt(percent * 100f)}%";

            if (progressFillImage != null)
                progressFillImage.fillAmount = percent;
        }

        private static string FormatAmount(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);

        // TMP's isRightToLeftText mirrors character order for layout (see ArabicTextShaper's
        // own comment) rather than running a real bidi algorithm, so a plain digit run embedded
        // inside an RTL-rendered sentence renders with its digit order visually reversed.
        // MonthlyMoneyScreenController's own embedded-number validation message
        // ("...الشهرية هو ٠٠٠١ ريال" for 1000) already establishes the fix used in this project:
        // convert to Eastern Arabic-Indic digits, then reverse that digit run before embedding,
        // so it reads correctly once TMP mirrors the whole sentence for RTL display.
        private static string ToArabicIndicDigits(string westernDigits)
        {
            var sb = new System.Text.StringBuilder(westernDigits.Length);
            foreach (char c in westernDigits)
                sb.Append(c >= '0' && c <= '9' ? (char)('٠' + (c - '0')) : c);

            char[] chars = sb.ToString().ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        private void OnConfirmClicked()
        {
            if (addAmount == null) return;
            if (!TryGetAmount(out int amount)) return;

            PlayerDataManager manager = PlayerDataManager.GetOrCreate();
            // Centralized wallet check - never subtracts twice, never goes negative, and
            // refreshes the City HUD itself. A failed spend changes nothing (returns here
            // before addAmount ever runs, so savedAmount/spentAmount/donatedAmount stay intact).
            if (!manager.TrySpendMoney(amount)) return;

            addAmount(manager.Data, amount);

            float responseTimeSeconds = Mathf.Max(0f, Time.unscaledTime - shownAtTime);
            Confirmed?.Invoke(kind, amount, responseTimeSeconds);

            touched = false;
            if (amountInput != null) amountInput.text = string.Empty;
            Validate();
        }
    }
}
