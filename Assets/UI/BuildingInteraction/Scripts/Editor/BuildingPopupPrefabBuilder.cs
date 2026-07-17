using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BuildingInteractionSystem.Editor
{
    // Materializes BuildingPopup.prefab's UI as a real, hand-editable GameObject hierarchy
    // (Overlay / CharityPopup / MallPopup / SavingsPopup / GenericPanel and all their children),
    // then wires every BuildingPopupController / TransactionPopupView serialized reference to
    // it. Every value here (colors, sizes, positions, sprites) is deliberate, matching the
    // approved Assets/Buildings_PopUp/PopUp_reference proportions - not guessed.
    //
    // Uses the CLEAN Assets/Buildings_PopUp/PopUp_use backgrounds (title/labels/divider only -
    // no baked buttons, inputs, or values) plus separate real sprites for the close button
    // (cancel_button), quick-amount pills, confirm buttons, input field backgrounds, the Riyal
    // unit icon, and the savings progress track. Every interactive/dynamic element (close
    // button, input, quick-amount buttons, confirm button, validation message, and the savings
    // progress readouts) is a real, separate Unity UI GameObject layered on top - nothing is
    // baked into a background image.
    //
    // [InitializeOnLoad] + delayCall (same pattern as StartFlow's CairoFontAssetSetup) makes this
    // self-healing ONLY for the "completely missing" case: if BuildingPopup.prefab's root has no
    // children at all (fresh clone, or a field rename that orphaned old serialized data), it
    // rebuilds automatically the next time the Editor finishes compiling. It never touches the
    // prefab again once real children exist, so it can never overwrite hand edits made in the
    // Inspector. "Rebuild Prefab Hierarchy" below is the only way to force a full reset, and it
    // must be run manually and deliberately.
    [InitializeOnLoad]
    internal static class BuildingPopupPrefabBuilder
    {
        private const string PrefabPath = "Assets/UI/BuildingInteraction/Prefabs/BuildingPopup.prefab";
        private const string ArtRoot = "Assets/Buildings_PopUp";
        private const string FontPath = "Assets/Fonts/Cairo-Regular SDF.asset";
        private const string GeneratedFolder = "Assets/UI/BuildingInteraction/Generated";

        static BuildingPopupPrefabBuilder()
        {
            EditorApplication.delayCall += EnsureHierarchyBuilt;
        }

        // Auto-repair hook: only rebuilds when the prefab's root has no children at all (the
        // "never built" / "orphaned by a field rename" state). Never runs again once a real
        // hierarchy exists, so it can't overwrite manual Inspector edits.
        private static void EnsureHierarchyBuilt()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
                return; // prefab moved/deleted - nothing to repair

            GameObject probe = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool empty;
            try
            {
                empty = probe.transform.childCount == 0;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(probe);
            }

            if (!empty) return;

            Debug.Log("BuildingPopupPrefabBuilder: BuildingPopup.prefab has no UI hierarchy yet - building it automatically.");
            Build();
        }

        private static readonly int[] CharityQuickAmounts = { 10, 25, 50 };
        private static readonly int[] MallQuickAmounts = { 25, 100, 250 };
        private static readonly int[] SavingsQuickAmounts = { 50, 100, 200 };

        private static class Palette
        {
            public static readonly Color Cream = new Color32(0xFF, 0xF8, 0xE9, 0xFF);
            public static readonly Color Green = new Color32(0x2E, 0x7D, 0x5B, 0xFF);
            public static readonly Color GreenDark = new Color32(0x1E, 0x54, 0x3C, 0xFF);
            public static readonly Color Gold = new Color32(0xD4, 0xAF, 0x37, 0xFF);
            public static readonly Color Blue = new Color32(0x2C, 0x6E, 0x9E, 0xFF);
            public static readonly Color TextDark = new Color32(0x2B, 0x2B, 0x20, 0xFF);
            public static readonly Color TextMuted = new Color32(0x6B, 0x6B, 0x5C, 0xFF);
            public static readonly Color ErrorRed = new Color32(0xB0, 0x3A, 0x2E, 0xFF);
            public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.55f);
            public static readonly Color InputBg = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

            public static readonly Color MutedText = new Color32(0xA9, 0xB6, 0xC4, 0xFF);
            public static readonly Color InputText = new Color32(0xF2, 0xF5, 0xF7, 0xFF);
            public static readonly Color ProgressFill = new Color32(0x3D, 0xB8, 0x9C, 0xFF); // approved turquoise
            public static readonly Color ProgressTrackTint = new Color(1f, 1f, 1f, 0.14f);
        }

        // Manual reset: deliberately rebuilds from scratch even if a hierarchy already exists
        // (unlike the auto-heal hook above, which only fires when the prefab is empty). Must be
        // run manually and deliberately - never called automatically once content exists.
        [MenuItem("Tools/Buildings Popup/Rebuild Prefab Hierarchy")]
        public static void RebuildHierarchy() => Build();

        private static void Build()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogWarning($"BuildingPopupPrefabBuilder: '{FontPath}' not found - text will use TMP's default font.");

            Sprite charityBg = LoadSprite($"{ArtRoot}/PopUp_use/Charity_use.png");
            Sprite mallBg = LoadSprite($"{ArtRoot}/PopUp_use/Mall_use.png");
            Sprite savingsBg = LoadSprite($"{ArtRoot}/PopUp_use/Saveup_use (2).png");
            Sprite riyalIcon = LoadSprite($"{ArtRoot}/PopUp_use/ريال (1).png");
            // cancel_button.png's own pixels are ~7-60% alpha (near-invisible on the dark
            // popup background); cancel_button_opaque.png is a boosted-alpha copy of the same
            // icon (original file left untouched) used for real visibility - see the CloseButton
            // fix applied directly to BuildingPopup.prefab for the full story.
            Sprite cancelButton = LoadSprite($"{ArtRoot}/PopUp_Button/cancel_button_opaque.png");
            Sprite confirmCharity = LoadSprite($"{ArtRoot}/PopUp_Button/GBtn_charity.png");
            Sprite confirmMall = LoadSprite($"{ArtRoot}/PopUp_Button/GBtn_Mall.png");
            Sprite confirmSavings = LoadSprite($"{ArtRoot}/PopUp_Button/Button_saveup.png");
            Sprite quickPill = LoadSprite($"{ArtRoot}/PopUp_Button/Button.png");
            // Input_Charity.png / Input_Saveup.png bake a Riyal symbol and a placeholder "0"
            // into the pixels themselves, which duplicated with the real TMP_InputField value
            // and the real RiyalIcon once those were added. Input_empty.png is the same pill
            // shape with no baked text at all - the only thing that should render the amount is
            // the TMP_InputField itself.
            Sprite inputBg = LoadSprite($"{ArtRoot}/PopUp_input/Input_empty.png");
            Sprite savingsInputBg = inputBg;
            Sprite progressTrackSprite = LoadSprite($"{ArtRoot}/PopUp_input/Saveup_fillup.png");

            if (charityBg == null || mallBg == null || savingsBg == null || riyalIcon == null || cancelButton == null
                || confirmCharity == null || confirmMall == null || confirmSavings == null || quickPill == null
                || inputBg == null || savingsInputBg == null || progressTrackSprite == null)
            {
                Debug.LogError("BuildingPopupPrefabBuilder: one or more Buildings_PopUp sprites failed to load - " +
                    "check the texture import settings (Sprite Mode) and paths under Assets/Buildings_PopUp.");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                BuildingPopupController controller = prefabRoot.GetComponent<BuildingPopupController>();
                if (controller == null)
                {
                    Debug.LogError($"BuildingPopupPrefabBuilder: '{PrefabPath}' has no BuildingPopupController component.");
                    return;
                }

                RectTransform root = prefabRoot.GetComponent<RectTransform>();
                if (root == null) root = prefabRoot.AddComponent<RectTransform>();
                StretchFull(root);

                // Idempotent rebuild: clear anything left from a previous run before rebuilding.
                foreach (string childName in new[] { "Overlay", "GenericPanel", "CharityPopup", "MallPopup", "SavingsPopup" })
                {
                    Transform old = root.Find(childName);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }

                Button overlayButton = BuildOverlay(root);
                GenericPanelRefs generic = BuildGenericPanel(root, font);

                var art = new TransactionArt
                {
                    quickPill = quickPill,
                    cancelButton = cancelButton,
                    riyalIcon = riyalIcon,
                    font = font
                };

                TransactionRefs charity = BuildTransactionPopup(root, "CharityPopup", charityBg, confirmCharity, inputBg,
                    CharityQuickAmounts, withProgress: false, progressTrackSprite, art);
                TransactionRefs mall = BuildTransactionPopup(root, "MallPopup", mallBg, confirmMall, inputBg,
                    MallQuickAmounts, withProgress: false, progressTrackSprite, art);
                TransactionRefs savings = BuildTransactionPopup(root, "SavingsPopup", savingsBg, confirmSavings, savingsInputBg,
                    SavingsQuickAmounts, withProgress: true, progressTrackSprite, art);

                // Default visible state when the prefab is opened for editing: the generic panel
                // (today's fallback for buildings outside Charity/Mall/Savings) is shown; the
                // three transaction popups start hidden. BuildingPopupController.Show() always
                // sets all four explicitly at runtime regardless of this default.
                generic.root.SetActive(true);
                charity.root.SetActive(false);
                mall.root.SetActive(false);
                savings.root.SetActive(false);

                WireController(controller, overlayButton, generic, charity, mall, savings);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                Debug.Log("BuildingPopupPrefabBuilder: BuildingPopup.prefab hierarchy rebuilt and wired.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        // ------------------------------------------------------------------------ wiring

        private static void WireController(BuildingPopupController controller, Button overlayButton,
            GenericPanelRefs generic, TransactionRefs charity, TransactionRefs mall, TransactionRefs savings)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("overlayButton").objectReferenceValue = overlayButton;

            so.FindProperty("genericPanel").objectReferenceValue = generic.root;
            so.FindProperty("genericCloseButton").objectReferenceValue = generic.closeButton;
            so.FindProperty("titleText").objectReferenceValue = generic.titleText;
            so.FindProperty("descriptionText").objectReferenceValue = generic.descriptionText;
            so.FindProperty("iconImage").objectReferenceValue = generic.iconImage;
            so.FindProperty("iconFallbackText").objectReferenceValue = generic.iconFallbackText;
            so.FindProperty("primaryButton").objectReferenceValue = generic.primaryButton;
            so.FindProperty("primaryButtonLabel").objectReferenceValue = generic.primaryButtonLabel;

            so.FindProperty("aiPanelGO").objectReferenceValue = generic.aiPanelGO;
            so.FindProperty("aiInputField").objectReferenceValue = generic.aiInputField;
            so.FindProperty("aiResponseText").objectReferenceValue = generic.aiResponseText;
            so.FindProperty("aiLoadingText").objectReferenceValue = generic.aiLoadingText;
            so.FindProperty("aiErrorText").objectReferenceValue = generic.aiErrorText;
            so.FindProperty("aiSendButton").objectReferenceValue = generic.aiSendButton;

            so.FindProperty("charityPopup").objectReferenceValue = charity.view;
            so.FindProperty("mallPopup").objectReferenceValue = mall.view;
            so.FindProperty("savingsPopup").objectReferenceValue = savings.view;
            so.ApplyModifiedPropertiesWithoutUndo();

            WireTransactionView(charity);
            WireTransactionView(mall);
            WireTransactionView(savings);
        }

        private static void WireTransactionView(TransactionRefs refs)
        {
            SerializedObject so = new SerializedObject(refs.view);
            so.FindProperty("closeButton").objectReferenceValue = refs.closeButton;
            so.FindProperty("amountInput").objectReferenceValue = refs.amountInput;
            so.FindProperty("confirmButton").objectReferenceValue = refs.confirmButton;
            so.FindProperty("validationMessage").objectReferenceValue = refs.validationMessage;

            SerializedProperty quickArray = so.FindProperty("quickAmountButtons");
            quickArray.arraySize = refs.quickButtons.Count;
            for (int i = 0; i < refs.quickButtons.Count; i++)
            {
                SerializedProperty element = quickArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue = refs.quickButtons[i].button;
                element.FindPropertyRelative("amount").intValue = refs.quickButtons[i].amount;
            }

            if (refs.currentSavedText != null)
                so.FindProperty("currentSavedText").objectReferenceValue = refs.currentSavedText;
            if (refs.goalTargetText != null)
                so.FindProperty("goalTargetText").objectReferenceValue = refs.goalTargetText;
            if (refs.remainingAmountText != null)
                so.FindProperty("remainingAmountText").objectReferenceValue = refs.remainingAmountText;
            if (refs.progressPercentText != null)
                so.FindProperty("progressPercentText").objectReferenceValue = refs.progressPercentText;
            if (refs.progressFillImage != null)
                so.FindProperty("progressFillImage").objectReferenceValue = refs.progressFillImage;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------------ Overlay

        private static Button BuildOverlay(Transform root)
        {
            RectTransform rt = CreateUIObject("Overlay", root);
            StretchFull(rt);

            Image img = rt.gameObject.AddComponent<Image>();
            img.color = Palette.Backdrop;

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            return btn;
        }

        // ------------------------------------------------------------------------ GenericPanel
        // (Buildings outside Charity/Mall/Savings, e.g. Investment Tower - unchanged design.)

        private class GenericPanelRefs
        {
            public GameObject root;
            public Button closeButton;
            public TMP_Text titleText;
            public TMP_Text descriptionText;
            public Image iconImage;
            public TMP_Text iconFallbackText;
            public Button primaryButton;
            public TMP_Text primaryButtonLabel;
            public GameObject aiPanelGO;
            public TMP_InputField aiInputField;
            public TMP_Text aiResponseText;
            public TMP_Text aiLoadingText;
            public TMP_Text aiErrorText;
            public Button aiSendButton;
        }

        private static GenericPanelRefs BuildGenericPanel(Transform overlay, TMP_FontAsset font)
        {
            var refs = new GenericPanelRefs();

            RectTransform panel = CreateUIObject("GenericPanel", overlay);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 560f);
            panel.anchoredPosition = Vector2.zero;
            refs.root = panel.gameObject;

            Image panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = GetOrCreateRoundedSprite(30, 128);
            panelImg.type = Image.Type.Sliced;
            panelImg.color = Palette.Cream;
            panelImg.raycastTarget = true; // intercepts clicks so Overlay behind it doesn't close the popup

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.Gold;
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            refs.closeButton = CreateButton(panel, "CloseButton", Palette.Green, GetOrCreateRoundedSprite(22, 64));
            RectTransform closeRT = refs.closeButton.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(0f, 1f);
            closeRT.anchorMax = new Vector2(0f, 1f);
            closeRT.pivot = new Vector2(0f, 1f);
            closeRT.sizeDelta = new Vector2(42f, 42f);
            closeRT.anchoredPosition = new Vector2(14f, -14f);
            TMP_Text closeLabel = CreateText(closeRT, "Label", "✕", 20f, Color.white, TextAlignmentOptions.Center, false, font);
            StretchFull(closeLabel.GetComponent<RectTransform>());

            RectTransform content = CreateUIObject("Content", panel);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(28f, 88f);
            content.offsetMax = new Vector2(-28f, -84f);
            VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 14f;

            RectTransform wrapper = CreateUIObject("IconWrapper", content);
            wrapper.gameObject.AddComponent<LayoutElement>().preferredHeight = 84f;
            RectTransform circle = CreateUIObject("IconCircle", wrapper);
            circle.anchorMin = new Vector2(0.5f, 0.5f);
            circle.anchorMax = new Vector2(0.5f, 0.5f);
            circle.pivot = new Vector2(0.5f, 0.5f);
            circle.sizeDelta = new Vector2(84f, 84f);
            circle.anchoredPosition = Vector2.zero;
            Image circleBg = circle.gameObject.AddComponent<Image>();
            circleBg.sprite = GetOrCreateRoundedSprite(42, 96);
            circleBg.type = Image.Type.Sliced;
            circleBg.color = Palette.Blue;
            RectTransform iconRT = CreateUIObject("Icon", circle);
            iconRT.anchorMin = new Vector2(0.18f, 0.18f);
            iconRT.anchorMax = new Vector2(0.82f, 0.82f);
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            refs.iconImage = iconRT.gameObject.AddComponent<Image>();
            refs.iconImage.preserveAspect = true;
            refs.iconImage.gameObject.SetActive(false);
            refs.iconFallbackText = CreateText(circle, "Fallback", "؟", 30f, Color.white, TextAlignmentOptions.Center, false, font);
            StretchFull(refs.iconFallbackText.GetComponent<RectTransform>());

            refs.titleText = CreateText(content, "Title", string.Empty, 32f, Palette.GreenDark, TextAlignmentOptions.Center, true, font);
            refs.titleText.fontStyle = FontStyles.Bold;
            refs.titleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            refs.descriptionText = CreateText(content, "Description", string.Empty, 22f, Palette.TextDark, TextAlignmentOptions.Center, true, font);
            refs.descriptionText.enableWordWrapping = true;
            refs.descriptionText.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

            RectTransform aiPanel = CreateUIObject("AIPanel", content);
            refs.aiPanelGO = aiPanel.gameObject;
            aiPanel.gameObject.AddComponent<LayoutElement>().preferredHeight = 200f;
            Image aiBg = aiPanel.gameObject.AddComponent<Image>();
            aiBg.sprite = GetOrCreateRoundedSprite(18, 96);
            aiBg.type = Image.Type.Sliced;
            aiBg.color = new Color(1f, 1f, 1f, 0.5f);
            VerticalLayoutGroup aiVlg = aiPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            aiVlg.childAlignment = TextAnchor.UpperCenter;
            aiVlg.childControlWidth = true;
            aiVlg.childControlHeight = true;
            aiVlg.childForceExpandWidth = true;
            aiVlg.childForceExpandHeight = false;
            aiVlg.spacing = 8f;
            aiVlg.padding = new RectOffset(14, 14, 12, 12);

            refs.aiResponseText = CreateText(aiPanel, "Response", string.Empty, 20f, Palette.TextDark, TextAlignmentOptions.Center, true, font);
            refs.aiResponseText.enableWordWrapping = true;
            refs.aiResponseText.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

            RectTransform row = CreateUIObject("InputRow", aiPanel);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
            HorizontalLayoutGroup hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 8f;

            refs.aiSendButton = CreateButton(row, "SendButton", Palette.Green, GetOrCreateRoundedSprite(12, 64));
            refs.aiSendButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;
            TMP_Text sendLabel = CreateText(refs.aiSendButton.transform, "Label", "إرسال", 20f, Color.white, TextAlignmentOptions.Center, false, font);
            StretchFull(sendLabel.GetComponent<RectTransform>());

            refs.aiInputField = CreateGenericInputField(row, "اكتب سؤالك هنا...", font);
            refs.aiInputField.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            refs.aiLoadingText = CreateText(aiPanel, "Loading", "جاري التحميل...", 18f, Palette.TextMuted, TextAlignmentOptions.Center, false, font);
            refs.aiLoadingText.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            refs.aiLoadingText.gameObject.SetActive(false);

            refs.aiErrorText = CreateText(aiPanel, "Error", string.Empty, 18f, Palette.ErrorRed, TextAlignmentOptions.Center, false, font);
            refs.aiErrorText.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            refs.aiErrorText.gameObject.SetActive(false);

            aiPanel.gameObject.SetActive(false);

            refs.primaryButton = CreateButton(panel, "PrimaryButton", Palette.Green, GetOrCreateRoundedSprite(20, 64));
            RectTransform primaryRT = refs.primaryButton.GetComponent<RectTransform>();
            primaryRT.anchorMin = new Vector2(0.5f, 0f);
            primaryRT.anchorMax = new Vector2(0.5f, 0f);
            primaryRT.pivot = new Vector2(0.5f, 0f);
            primaryRT.sizeDelta = new Vector2(220f, 54f);
            primaryRT.anchoredPosition = new Vector2(0f, 18f);
            refs.primaryButtonLabel = CreateText(primaryRT, "Label", "دخول", 24f, Color.white, TextAlignmentOptions.Center, false, font);
            refs.primaryButtonLabel.fontStyle = FontStyles.Bold;
            StretchFull(refs.primaryButtonLabel.GetComponent<RectTransform>());

            return refs;
        }

        private static TMP_InputField CreateGenericInputField(Transform parent, string placeholderText, TMP_FontAsset font)
        {
            RectTransform rt = CreateUIObject("InputField", parent);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = GetOrCreateRoundedSprite(10, 48);
            bg.type = Image.Type.Sliced;
            bg.color = Palette.InputBg;

            TMP_InputField input = rt.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;

            RectTransform textArea = CreateUIObject("Text Area", rt);
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(10f, 4f);
            textArea.offsetMax = new Vector2(-10f, -4f);
            textArea.gameObject.AddComponent<RectMask2D>();

            TMP_Text placeholder = CreateText(textArea, "Placeholder", placeholderText, 20f, new Color(0.35f, 0.35f, 0.3f, 0.6f), TextAlignmentOptions.MidlineRight, true, font);
            placeholder.fontStyle = FontStyles.Italic;
            StretchFull(placeholder.GetComponent<RectTransform>());

            TMP_Text textComponent = CreateText(textArea, "Text", string.Empty, 20f, Palette.TextDark, TextAlignmentOptions.MidlineRight, true, font);
            StretchFull(textComponent.GetComponent<RectTransform>());

            input.textViewport = textArea;
            input.textComponent = textComponent;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 200;

            return input;
        }

        // ------------------------------------------------------------------------ Transaction popups

        private class TransactionArt
        {
            public Sprite quickPill;
            public Sprite cancelButton;
            public Sprite riyalIcon;
            public TMP_FontAsset font;
        }

        private class TransactionRefs
        {
            public GameObject root;
            public TransactionPopupView view;
            public Button closeButton;
            public TMP_InputField amountInput;
            public List<(Button button, int amount)> quickButtons = new List<(Button, int)>();
            public Button confirmButton;
            public TMP_Text validationMessage;
            public TMP_Text currentSavedText;
            public TMP_Text goalTargetText;
            public TMP_Text remainingAmountText;
            public TMP_Text progressPercentText;
            public Image progressFillImage;
        }

        private static TransactionRefs BuildTransactionPopup(Transform overlay, string name, Sprite background,
            Sprite confirmSprite, Sprite inputBgSprite, int[] quickAmounts, bool withProgress, Sprite progressTrackSprite,
            TransactionArt art)
        {
            var refs = new TransactionRefs();
            TMP_FontAsset font = art.font;

            RectTransform root = CreateUIObject(name, overlay);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            refs.root = root.gameObject;
            refs.view = root.gameObject.AddComponent<TransactionPopupView>();

            float w = background.rect.width;
            float h = background.rect.height;
            const float footerHeight = 46f;
            root.sizeDelta = new Vector2(w, h + footerHeight);

            RectTransform bgRT = CreateUIObject("Background", root);
            bgRT.anchorMin = new Vector2(0.5f, 1f);
            bgRT.anchorMax = new Vector2(0.5f, 1f);
            bgRT.pivot = new Vector2(0.5f, 1f);
            bgRT.sizeDelta = new Vector2(w, h);
            bgRT.anchoredPosition = Vector2.zero;
            Image bgImage = bgRT.gameObject.AddComponent<Image>();
            bgImage.sprite = background;
            bgImage.raycastTarget = true; // swallow clicks so Overlay behind it doesn't close the popup

            Rect inputRect, quickRowRect, confirmRect;
            Rect remainingRect = default, savedRect = default, targetRect = default, trackRect = default;

            if (name == "CharityPopup")
            {
                inputRect = NormRect(0.176f, 0.470f, 0.8185f, 0.538f);
                quickRowRect = NormRect(0.176f, 0.384f, 0.8185f, 0.458f);
                confirmRect = NormRect(0.176f, 0.2792f, 0.8185f, 0.3654f);
            }
            else if (name == "MallPopup")
            {
                inputRect = NormRect(0.176f, 0.5227f, 0.8185f, 0.5955f);
                quickRowRect = NormRect(0.176f, 0.4045f, 0.8185f, 0.5227f);
                confirmRect = NormRect(0.176f, 0.2955f, 0.8185f, 0.3864f);
            }
            else
            {
                // Re-measured directly off PopUp_reference/Saveup_popup.png (see
                // BuildingPopupInputFix, which applies these same values as a surgical repair).
                inputRect = NormRect(0.176f, 0.4286f, 0.8185f, 0.5086f);
                quickRowRect = NormRect(0.176f, 0.3295f, 0.8185f, 0.4286f);
                confirmRect = NormRect(0.176f, 0.2438f, 0.8185f, 0.3238f);
                savedRect = NormRect(0.7407f, 0.7295f, 0.8241f, 0.7752f); // "444 ريال" row (top-right)
                targetRect = NormRect(0.7407f, 0.5905f, 0.8241f, 0.6095f); // "4404" (card bottom-right)
                remainingRect = NormRect(0.176f, 0.543f, 0.8185f, 0.581f); // gap below the card
                trackRect = NormRect(0.176f, 0.5905f, 0.8185f, 0.7048f); // progress card
            }

            // CloseButton - real sprite (cancel_button), top-left inset.
            RectTransform closeRT = CreateUIObject("CloseButton", bgRT);
            closeRT.anchorMin = new Vector2(0f, 1f);
            closeRT.anchorMax = new Vector2(0f, 1f);
            closeRT.pivot = new Vector2(0f, 1f);
            closeRT.sizeDelta = new Vector2(36f, 36f);
            closeRT.anchoredPosition = new Vector2(20f, -20f);
            Image closeImg = closeRT.gameObject.AddComponent<Image>();
            closeImg.sprite = art.cancelButton;
            closeImg.preserveAspect = true;
            closeImg.raycastTarget = true;
            refs.closeButton = closeRT.gameObject.AddComponent<Button>();
            refs.closeButton.targetGraphic = closeImg;

            // InputField - real background sprite + real TMP_InputField + Riyal unit icon.
            RectTransform inputRT = CreateUIObject("InputField", bgRT);
            AnchorFraction(inputRT, inputRect);
            Image inputBgImg = inputRT.gameObject.AddComponent<Image>();
            inputBgImg.sprite = inputBgSprite;
            inputBgImg.type = Image.Type.Sliced;
            inputBgImg.raycastTarget = true;

            RectTransform riyalRT = CreateUIObject("RiyalIcon", inputRT);
            riyalRT.anchorMin = new Vector2(0f, 0.5f);
            riyalRT.anchorMax = new Vector2(0f, 0.5f);
            riyalRT.pivot = new Vector2(0f, 0.5f);
            riyalRT.sizeDelta = new Vector2(28f, 16f);
            riyalRT.anchoredPosition = new Vector2(16f, 0f);
            Image riyalImg = riyalRT.gameObject.AddComponent<Image>();
            riyalImg.sprite = art.riyalIcon;
            riyalImg.preserveAspect = true;
            riyalImg.raycastTarget = false;

            refs.amountInput = inputRT.gameObject.AddComponent<TMP_InputField>();
            refs.amountInput.targetGraphic = inputBgImg;
            refs.amountInput.interactable = true;
            refs.amountInput.readOnly = false;
            refs.amountInput.richText = false;

            RectTransform textArea = CreateUIObject("Text Area", inputRT);
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(52f, 2f); // leave room for the Riyal icon on the left
            textArea.offsetMax = new Vector2(-28f, -2f);
            textArea.gameObject.AddComponent<RectMask2D>();

            TMP_Text placeholder = CreateBareText(textArea, "Placeholder", "0", 24f,
                Palette.MutedText, TextAlignmentOptions.MidlineRight, false, font);
            StretchFull(placeholder.GetComponent<RectTransform>());

            TMP_Text textComponent = CreateBareText(textArea, "Text", string.Empty, 24f,
                Palette.InputText, TextAlignmentOptions.MidlineRight, false, font);
            StretchFull(textComponent.GetComponent<RectTransform>());

            refs.amountInput.textViewport = textArea;
            refs.amountInput.textComponent = textComponent;
            refs.amountInput.placeholder = placeholder;
            refs.amountInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            refs.amountInput.lineType = TMP_InputField.LineType.SingleLine;
            refs.amountInput.characterLimit = 9;

            // QuickAmountButtons
            RectTransform quickRow = CreateUIObject("QuickAmountButtons", bgRT);
            AnchorFraction(quickRow, quickRowRect);
            HorizontalLayoutGroup hlg = quickRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 14f;

            foreach (int amount in quickAmounts)
            {
                RectTransform pillRT = CreateUIObject($"Quick_{amount}", quickRow);
                Image pillImg = pillRT.gameObject.AddComponent<Image>();
                pillImg.sprite = art.quickPill;
                pillImg.type = Image.Type.Sliced;
                pillImg.raycastTarget = true;

                Button pillBtn = pillRT.gameObject.AddComponent<Button>();
                pillBtn.targetGraphic = pillImg;

                TMP_Text label = CreateBareText(pillRT, "Label", amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    22f, Palette.InputText, TextAlignmentOptions.Center, false, font);
                label.fontStyle = FontStyles.Bold;
                StretchFull(label.GetComponent<RectTransform>());

                refs.quickButtons.Add((pillBtn, amount));
            }

            // PrimaryButton (label baked into confirmSprite: تبرع / اشتري / ادخر)
            RectTransform confirmRT = CreateUIObject("PrimaryButton", bgRT);
            AnchorFraction(confirmRT, confirmRect);
            Image confirmImg = confirmRT.gameObject.AddComponent<Image>();
            confirmImg.sprite = confirmSprite;
            confirmImg.type = Image.Type.Sliced;
            confirmImg.raycastTarget = true;
            refs.confirmButton = confirmRT.gameObject.AddComponent<Button>();
            refs.confirmButton.targetGraphic = confirmImg;

            if (withProgress)
            {
                RectTransform dynamicTexts = CreateUIObject("DynamicTexts", bgRT);
                StretchFull(dynamicTexts);

                // Not shown with an example value in the reference (it depicts the 0% empty
                // state) - placed centered in the small gap between the progress card and the
                // baked "كم ودك تدخر؟" label.
                refs.remainingAmountText = CreateBareText(dynamicTexts, "RemainingAmountText", string.Empty, 16f,
                    Palette.MutedText, TextAlignmentOptions.Center, true, font);
                AnchorFraction(refs.remainingAmountText.GetComponent<RectTransform>(), remainingRect);

                refs.currentSavedText = CreateBareText(dynamicTexts, "CurrentSavedText", string.Empty, 17f,
                    Palette.MutedText, TextAlignmentOptions.MidlineRight, true, font);
                AnchorFraction(refs.currentSavedText.GetComponent<RectTransform>(), savedRect);

                refs.goalTargetText = CreateBareText(dynamicTexts, "GoalTargetText", string.Empty, 17f,
                    Palette.MutedText, TextAlignmentOptions.MidlineRight, true, font);
                AnchorFraction(refs.goalTargetText.GetComponent<RectTransform>(), targetRect);

                // ProgressTrack: the approved track frame sprite + a mask, so the fill below is
                // always clipped to the rounded track shape and can never visually exceed it.
                RectTransform trackRT = CreateUIObject("ProgressTrack", bgRT);
                AnchorFraction(trackRT, trackRect);
                Image trackImg = trackRT.gameObject.AddComponent<Image>();
                trackImg.sprite = progressTrackSprite;
                trackImg.type = Image.Type.Sliced;
                trackImg.color = Palette.ProgressTrackTint;
                trackImg.raycastTarget = false;
                trackRT.gameObject.AddComponent<RectMask2D>();

                RectTransform fillRT = CreateUIObject("ProgressFill", trackRT);
                StretchFull(fillRT);
                refs.progressFillImage = fillRT.gameObject.AddComponent<Image>();
                refs.progressFillImage.color = Palette.ProgressFill;
                refs.progressFillImage.type = Image.Type.Filled;
                refs.progressFillImage.fillMethod = Image.FillMethod.Horizontal;
                refs.progressFillImage.fillOrigin = (int)Image.OriginHorizontal.Right; // RTL: fills right-to-left
                refs.progressFillImage.fillAmount = 0f;
                refs.progressFillImage.raycastTarget = false;

                // Top-left inside the card ("0%" in the reference) - the empty/start end of the
                // bar, since the fill grows from the right (RTL).
                refs.progressPercentText = CreateBareText(trackRT, "ProgressPercentageText", "0%", 16f,
                    Color.white, TextAlignmentOptions.MidlineLeft, false, font);
                refs.progressPercentText.fontStyle = FontStyles.Bold;
                RectTransform percentRT = refs.progressPercentText.GetComponent<RectTransform>();
                percentRT.anchorMin = new Vector2(0f, 1f);
                percentRT.anchorMax = new Vector2(0f, 1f);
                percentRT.pivot = new Vector2(0f, 1f);
                percentRT.sizeDelta = new Vector2(70f, 22f);
                percentRT.anchoredPosition = new Vector2(12f, -8f);
            }

            // ValidationMessage
            RectTransform footerRT = CreateUIObject("ValidationMessage", root);
            footerRT.anchorMin = new Vector2(0f, 0f);
            footerRT.anchorMax = new Vector2(1f, 0f);
            footerRT.pivot = new Vector2(0.5f, 0f);
            footerRT.sizeDelta = new Vector2(0f, footerHeight);
            footerRT.anchoredPosition = Vector2.zero;
            refs.validationMessage = CreateBareText(footerRT, "Text", string.Empty, 18f,
                Palette.MutedText, TextAlignmentOptions.Center, true, font);
            refs.validationMessage.enableWordWrapping = true;
            StretchFull(refs.validationMessage.GetComponent<RectTransform>());

            return refs;
        }

        private static Rect NormRect(float xMin, float yMin, float xMax, float yMax) =>
            new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

        // ------------------------------------------------------------------------ shared helpers

        private static RectTransform CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorFraction(RectTransform rt, Rect norm)
        {
            rt.anchorMin = new Vector2(norm.xMin, norm.yMin);
            rt.anchorMax = new Vector2(norm.xMax, norm.yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        // Matches the old generic-panel runtime CreateText: assigns content as-is, no Arabic
        // shaping applied at build time (dynamic fields like Title/Description/AIResponse get
        // real shaped Arabic later, at runtime, from BuildingPopupController.Show()).
        private static TMP_Text CreateText(Transform parent, string name, string content, float fontSize,
            Color color, TextAlignmentOptions alignment, bool rtl, TMP_FontAsset font)
        {
            RectTransform rt = CreateUIObject(name, parent);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.isRightToLeftText = rtl;
            text.enableWordWrapping = false;
            if (font != null) text.font = font;
            return text;
        }

        private static TMP_Text CreateBareText(Transform parent, string name, string content, float fontSize,
            Color color, TextAlignmentOptions alignment, bool rtl, TMP_FontAsset font)
        {
            RectTransform rt = CreateUIObject(name, parent);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.isRightToLeftText = rtl;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            if (font != null) text.font = font;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, Color bgColor, Sprite sprite)
        {
            RectTransform rt = CreateUIObject(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = bgColor;
            return rt.gameObject.AddComponent<Button>();
        }

        // ------------------------------------------------------------------------ generated rounded-rect sprites

        // The generic (non-transaction) popup's "playful rounded" look was previously generated
        // at runtime by RoundedRectSpriteFactory and never saved anywhere - fine for a Play-mode-
        // only Texture2D, but not something a saved prefab can reference. This bakes the exact
        // same pixels (same algorithm, called directly) to a real PNG asset once, so the prefab
        // holds a normal, Inspector-editable Sprite reference exactly like every other Image.
        private static Sprite GetOrCreateRoundedSprite(int radius, int size)
        {
            string fileName = $"RoundedRect_r{radius}_s{size}.png";
            string path = $"{GeneratedFolder}/{fileName}";

            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/UI/BuildingInteraction", "Generated");

            Sprite runtimeSprite = RoundedRectSpriteFactory.GetRoundedSprite(radius, size);
            byte[] png = ImageConversion.EncodeToPNG(runtimeSprite.texture);
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = new Vector4(radius, radius, radius, radius);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.spritePixelsPerUnit = 100;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
