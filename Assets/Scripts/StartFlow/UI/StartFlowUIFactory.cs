using StartFlow.Arabic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StartFlow.UI
{
    // Shared, dependency-free UI construction helpers for the StartFlow scene. Deliberately
    // separate from BuildingInteractionSystem's equivalent factory - this system must not
    // touch or depend on the existing building popup code at all.
    //
    // Every screen is built from the real exported UI_final assets (Backgrounds/Buttons/
    // Inputs/Logo) as actual Image/Button/TMP_InputField components - the References/ folder
    // is only consulted (in the Editor build script) to measure where things go; it is never
    // displayed in the scene.
    //
    // Arabic text uses ArabicTextShaper.Shape() (see Assets/Scripts/StartFlow/Arabic/) for
    // connected letter glyphs, combined with TMP's own isRightToLeftText for RTL layout -
    // strings are never manually reordered/reversed. See ArabicTextShaper's own comment for
    // why: Cairo's presentation-forms glyph set is missing every isolated-form codepoint, so
    // shaping falls back to the base codepoint at isolated position instead of tofu.
    public static class StartFlowUIFactory
    {
        public static class Palette
        {
            // Sampled from the UI_final reference art (dark navy night-sky theme).
            public static readonly Color TextPrimary = new Color32(0xF2, 0xF5, 0xF7, 0xFF);
            public static readonly Color TextMuted = new Color32(0xA9, 0xB6, 0xC4, 0xFF);
            public static readonly Color TealAccent = new Color32(0x3D, 0xB8, 0x9C, 0xFF);
            public static readonly Color GoldAccent = new Color32(0xE8, 0xC0, 0x5A, 0xFF);
            public static readonly Color InputText = new Color32(0xE7, 0xEC, 0xEF, 0xFF);
            public static readonly Color Placeholder = new Color32(0x8B, 0x97, 0xA6, 0xFF);
            public static readonly Color Warning = new Color32(0xE0, 0x6A, 0x6A, 0xFF);
        }

        public static RectTransform CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Positions/sizes rt as an exact fractional box of its parent - used to place every
        // overlay (cards, inputs, buttons) using fractions measured directly off the
        // reference composites, so alignment holds regardless of how the canvas is scaled.
        public static void AnchorFraction(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        // Full-bleed Image (backgrounds) - stretched to fill its parent's whole rect. Never a
        // raycast target - it must never be able to intercept clicks meant for controls drawn
        // on top of it.
        public static Image CreateFullBleedImage(Transform parent, string name, Sprite sprite)
        {
            RectTransform rt = CreateUIObject(name, parent);
            StretchFull(rt);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        // Plain Image using a real asset sprite - caller positions/sizes it (usually via
        // AnchorFraction). Decorative only (card backgrounds) - never a raycast target, so it
        // can never sit in front of and block the interactive control drawn over it.
        public static Image CreateImage(Transform parent, string name, Sprite sprite)
        {
            RectTransform rt = CreateUIObject(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        // Small icon Image that preserves its own aspect ratio (the Riyal currency mark).
        public static Image CreateIcon(Transform parent, string name, Sprite sprite)
        {
            RectTransform rt = CreateUIObject(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        // TMP_Text for real, native UI content. Arabic strings are shaped for connected glyphs
        // (ArabicTextShaper.Shape - 1:1 substitution, no reordering) and isRightToLeftText
        // tells TMP itself to lay the text out right-to-left.
        public static TMP_Text CreateText(Transform parent, string name, string content, float fontSize, Color color,
            TextAlignmentOptions alignment, bool rtl, TMP_FontAsset font, FontStyles style = FontStyles.Normal)
        {
            RectTransform rt = CreateUIObject(name, parent);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = rtl ? ArabicTextShaper.Shape(content) : content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.isRightToLeftText = rtl;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.fontStyle = style;
            text.raycastTarget = false;
            if (font != null) text.font = font;
            return text;
        }

        public static TMP_Text CreateWrappingText(Transform parent, string name, string content, float fontSize, Color color,
            TextAlignmentOptions alignment, bool rtl, TMP_FontAsset font, FontStyles style = FontStyles.Normal)
        {
            RectTransform rt = CreateUIObject(name, parent);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = rtl ? ArabicTextShaper.Shape(content) : content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.isRightToLeftText = rtl;
            text.enableWordWrapping = true;
            text.fontStyle = style;
            text.raycastTarget = false;
            if (font != null) text.font = font;
            return text;
        }

        // Native Button using a real background sprite plus a centered TMP_Text label.
        // Button.interactable=false automatically applies Selectable's disabledColor tint
        // (ColorTint transition, the default), which is enough to convey the disabled state
        // without needing separate baked-text sprite variants per state.
        public static Button CreateButton(Transform parent, string name, Sprite bgSprite,
            string label, float fontSize, Color textColor, TMP_FontAsset font, FontStyles style = FontStyles.Bold)
        {
            RectTransform rt = CreateUIObject(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = bgSprite;
            img.raycastTarget = true;

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;

            if (!string.IsNullOrEmpty(label))
            {
                TMP_Text labelText = CreateText(rt, "Label", label, fontSize, textColor,
                    TextAlignmentOptions.Center, true, font, style);
                StretchFull(labelText.GetComponent<RectTransform>());
            }

            return button;
        }

        // Icon-only Button (the back chevron) - background sprite, no label.
        public static Button CreateIconButton(Transform parent, string name, Sprite sprite)
        {
            RectTransform rt = CreateUIObject(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = true;

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            return button;
        }

        // Native TMP_InputField using a real background sprite (or fully transparent when
        // bgSprite is null, for overlaying directly on a card image drawn behind it). rtl=true
        // (Standard content type) gets an ArabicInputShaper for connected-glyph typing, with
        // RTL layout/caret handled natively by TMP (isRightToLeftText).
        //
        // Explicitly sets every property called out as a common "input field doesn't respond
        // to clicks/typing" cause when a TMP_InputField is built via script instead of the
        // Editor menu: interactable, readOnly, richText, characterLimit, targetGraphic, and
        // raycastTarget on both the field's own background and its text/placeholder children.
        public static TMP_InputField CreateInputField(Transform parent, string name, Sprite bgSprite,
            string placeholderText, TMP_FontAsset font, Color textColor,
            TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard, int characterLimit = 40)
        {
            bool rtl = contentType == TMP_InputField.ContentType.Standard;

            RectTransform rt = CreateUIObject(name, parent);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.raycastTarget = true; // the field's own hit area - must catch the click even when invisible
            if (bgSprite != null) bg.sprite = bgSprite;
            else bg.color = new Color(0f, 0f, 0f, 0f);

            TMP_InputField input = rt.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.interactable = true;
            input.readOnly = false;
            input.richText = false;

            RectTransform textArea = CreateUIObject("Text Area", rt);
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(24f, 4f);
            textArea.offsetMax = new Vector2(-24f, -4f);
            textArea.gameObject.AddComponent<RectMask2D>();

            TMP_Text placeholder = CreateBareText(textArea, "Placeholder", rtl ? ArabicTextShaper.Shape(placeholderText) : placeholderText,
                22f, Palette.Placeholder, TextAlignmentOptions.MidlineRight, font, FontStyles.Normal, rtl);
            StretchFull(placeholder.GetComponent<RectTransform>());

            TMP_Text textComponent = CreateBareText(textArea, "Text", string.Empty, 22f,
                textColor, TextAlignmentOptions.MidlineRight, font, FontStyles.Normal, rtl);
            StretchFull(textComponent.GetComponent<RectTransform>());

            input.textViewport = textArea;
            input.textComponent = textComponent;
            input.placeholder = placeholder;
            input.contentType = contentType;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = characterLimit;

            if (rtl)
                rt.gameObject.AddComponent<ArabicInputShaper>();

            return input;
        }

        private static TMP_Text CreateBareText(Transform parent, string name, string content, float fontSize,
            Color color, TextAlignmentOptions alignment, TMP_FontAsset font, FontStyles style, bool rtl)
        {
            RectTransform rt = CreateUIObject(name, parent);
            TextMeshProUGUI text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.isRightToLeftText = rtl;
            text.enableWordWrapping = false;
            text.fontStyle = style;
            text.raycastTarget = false;
            if (font != null) text.font = font;
            return text;
        }
    }
}
