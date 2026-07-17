using TMPro;
using UnityEngine;

namespace StartFlow.Arabic
{
    // Makes a TMP_InputField render connected Arabic while typing. TMP_InputField.text (the
    // model that validation/PlayerDataManager read) is left completely untouched.
    //
    // Uses TMP's own ITextPreprocessor hook rather than rewriting textComponent.text from
    // onValueChanged: TMP_InputField resyncs its text component's .text from its internal raw
    // string on its own update pass, which can happen *after* a onValueChanged listener runs
    // and silently stomp a one-off assignment back to the unshaped string. textPreprocessor is
    // called by TMP itself at mesh-generation time, on the current raw text, every time - so
    // there is no ordering race and no leftover unshaped frame.
    //
    // Ordering/caret movement is left entirely to TMP's own isRightToLeftText handling.
    // ArabicTextShaper.Shape() only substitutes glyph forms - it never reorders or changes
    // string length - so caret indices computed against the preprocessed text still line up
    // 1:1 with the raw text TMP_InputField itself tracks.
    [RequireComponent(typeof(TMP_InputField))]
    public class ArabicInputShaper : MonoBehaviour, ITextPreprocessor
    {
        private TMP_InputField field;

        private void Awake()
        {
            field = GetComponent<TMP_InputField>();
            if (field.textComponent != null)
            {
                field.textComponent.isRightToLeftText = true;
                field.textComponent.textPreprocessor = this;
            }
        }

        public string PreprocessText(string text) => ArabicTextShaper.Shape(text);
    }
}
