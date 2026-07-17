using StartFlow.Arabic;
using TMPro;

namespace InvestmentTowerUI
{
    // Single, project-consistent entry point for every dynamic Arabic string assigned to a
    // TMP_Text at runtime inside the Investment Tower. Wraps - never replaces - the project's
    // existing, proven Arabic solution (StartFlow.Arabic.ArabicTextShaper): contextual glyph
    // shaping only, no manual character/array reversal. Ordering still comes entirely from
    // TMP_Text.isRightToLeftText, exactly as ArabicTextShaper's own header documents. Safe to
    // call on any string, including pure numbers/symbols/dates/prices/stock symbols - Shape()
    // only ever touches actual Arabic letters and passes everything else through unchanged.
    public static class ArabicTextUtility
    {
        public static string Format(string value) => ArabicTextShaper.Shape(value);

        // True when a string should render right-to-left, i.e. its first non-whitespace
        // character is Arabic - "142.00 ر" stays left-to-right (correct: the number leads),
        // while "الإنماء" or "منخفضة" become right-to-left. Lets callers whose TMP_Text
        // sometimes holds Arabic-only content and sometimes numeric/Latin content (company logo
        // letters like "STC", summary values that are either a money amount or a company name)
        // set isRightToLeftText correctly per assignment instead of relying on a single fixed
        // value chosen once at build time.
        public static bool IsArabicLed(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c)) continue;
                return ArabicTextShaper.IsArabicLetter(c);
            }
            return false;
        }

        // Shapes 'value', assigns it to 'target', and sets isRightToLeftText to match the
        // ORIGINAL (unshaped) content - the one-call option for TMP_Text fields whose content
        // isn't guaranteed to always be (or never be) Arabic.
        //
        // IsArabicLed must run on 'value' BEFORE Format() shapes it, never after: shaping
        // replaces many base Arabic letters (the only characters IsArabicLetter recognizes -
        // see ArabicTextShaper.FormTable, keyed by base letters) with Initial/Medial/Final
        // presentation-form glyphs, which are different Unicode codepoints entirely. Checking
        // the already-shaped string caused IsArabicLed to silently return false - and
        // isRightToLeftText to end up wrong - for any word whose first letter joins to the
        // next one (most Arabic words other than those starting with a right-joining-only
        // letter like ا/د/ر), which was the actual cause of text still appearing reversed
        // after shaping was already correct.
        public static void Apply(TMP_Text target, string value)
        {
            if (target == null) return;
            bool isArabicLed = IsArabicLed(value);
            target.text = Format(value);
            target.isRightToLeftText = isArabicLed;
        }
    }
}
