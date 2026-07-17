using System.Collections.Generic;
using System.Text;

namespace StartFlow.Arabic
{
    // Contextual Arabic letter-joining shaper. This is the project's Arabic-support solution -
    // TMP_Text.isRightToLeftText only mirrors character order for layout, it never selects the
    // initial/medial/final glyph a connected Arabic letter needs, so typed/displayed Arabic
    // renders as disconnected isolated letters without this.
    //
    // 1:1 character substitution only - no reordering, no reversal, no ligature merging.
    // TMP's own isRightToLeftText handles RTL layout/ordering; this only fixes which glyph
    // renders for each letter, so it composes safely with TMP's RTL and never changes string
    // length (safe for live TMP_InputField text, where caret indices must stay aligned to the
    // raw string).
    //
    // IMPORTANT (verified directly against the shipped Cairo-Regular/Bold SDF source TTFs via
    // their cmap tables, not assumed): Cairo's presentation-forms glyph set covers the
    // Initial/Medial/Final forms (U+FE70-FEFC) but does NOT include the isolated-form
    // codepoints - the isolated glyph for every letter is only reachable through the letter's
    // own base codepoint (U+0600-06FF). Feeding an isolated-form presentation codepoint (as a
    // naive shaper would) renders tofu, because that glyph does not exist in either font. So
    // "isolated position" here always resolves to the base character, never a presentation
    // form - this is the one deviation from the "obvious" table-driven approach, and it's the
    // fix for the square/disconnected-glyph symptom.
    public static class ArabicTextShaper
    {
        private readonly struct Forms
        {
            public readonly char Initial;
            public readonly char Medial;
            public readonly char Final;

            public Forms(char initial, char medial, char final)
            {
                Initial = initial;
                Medial = medial;
                Final = final;
            }
        }

        private enum JoinType
        {
            Dual,       // connects to both a valid previous and a valid next letter
            RightOnly,  // only ever isolated (base char) or final (alef, dal, reh, waw, ...)
            NonJoining  // always isolated/base (hamza alone)
        }

        // Base Arabic letter -> its Arabic Presentation Forms-B glyphs for the Initial/Medial/
        // Final join contexts (all confirmed present in Cairo-Regular/Bold SDF's source TTFs).
        // For RightOnly letters, Initial/Medial are unused (never selected below) and just
        // mirror Final so the table stays a flat 3-tuple per letter.
        private static readonly Dictionary<char, Forms> FormTable = new Dictionary<char, Forms>
        {
            ['ئ'] = new Forms('ﺋ', 'ﺌ', 'ﺊ'), // yeh hamza (dual)
            ['ب'] = new Forms('ﺑ', 'ﺒ', 'ﺐ'), // beh
            ['ت'] = new Forms('ﺗ', 'ﺘ', 'ﺖ'), // teh
            ['ث'] = new Forms('ﺛ', 'ﺜ', 'ﺚ'), // theh
            ['ج'] = new Forms('ﺟ', 'ﺠ', 'ﺞ'), // jeem
            ['ح'] = new Forms('ﺣ', 'ﺤ', 'ﺢ'), // hah
            ['خ'] = new Forms('ﺧ', 'ﺨ', 'ﺦ'), // khah
            ['د'] = new Forms('ﺩ', 'ﺩ', 'ﺪ'), // dal (right-only)
            ['ذ'] = new Forms('ﺫ', 'ﺫ', 'ﺬ'), // thal (right-only)
            ['ر'] = new Forms('ﺭ', 'ﺭ', 'ﺮ'), // reh (right-only)
            ['ز'] = new Forms('ﺯ', 'ﺯ', 'ﺰ'), // zain (right-only)
            ['س'] = new Forms('ﺳ', 'ﺴ', 'ﺲ'), // seen
            ['ش'] = new Forms('ﺷ', 'ﺸ', 'ﺶ'), // sheen
            ['ص'] = new Forms('ﺻ', 'ﺼ', 'ﺺ'), // sad
            ['ض'] = new Forms('ﺿ', 'ﻀ', 'ﺾ'), // dad
            ['ط'] = new Forms('ﻃ', 'ﻄ', 'ﻂ'), // tah
            ['ظ'] = new Forms('ﻇ', 'ﻈ', 'ﻆ'), // zah
            ['ع'] = new Forms('ﻋ', 'ﻌ', 'ﻊ'), // ain
            ['غ'] = new Forms('ﻏ', 'ﻐ', 'ﻎ'), // ghain
            ['ف'] = new Forms('ﻓ', 'ﻔ', 'ﻒ'), // feh
            ['ق'] = new Forms('ﻗ', 'ﻘ', 'ﻖ'), // qaf
            ['ك'] = new Forms('ﻛ', 'ﻜ', 'ﻚ'), // kaf
            ['ل'] = new Forms('ﻟ', 'ﻠ', 'ﻞ'), // lam
            ['م'] = new Forms('ﻣ', 'ﻤ', 'ﻢ'), // meem
            ['ن'] = new Forms('ﻧ', 'ﻨ', 'ﻦ'), // noon
            ['ه'] = new Forms('ﻫ', 'ﻬ', 'ﻪ'), // heh
            ['و'] = new Forms('ﻭ', 'ﻭ', 'ﻮ'), // waw (right-only)
            ['ى'] = new Forms('ﻯ', 'ﻯ', 'ﻰ'), // alef maksura (right-only)
            ['ي'] = new Forms('ﻳ', 'ﻴ', 'ﻲ'), // yeh
            ['آ'] = new Forms('ﺁ', 'ﺁ', 'ﺂ'), // alef madda (right-only)
            ['أ'] = new Forms('ﺃ', 'ﺃ', 'ﺄ'), // alef hamza above (right-only)
            ['ؤ'] = new Forms('ﺅ', 'ﺅ', 'ﺆ'), // waw hamza (right-only)
            ['إ'] = new Forms('ﺇ', 'ﺇ', 'ﺈ'), // alef hamza below (right-only)
            ['ا'] = new Forms('ﺍ', 'ﺍ', 'ﺎ'), // alef (right-only)
            ['ة'] = new Forms('ﺓ', 'ﺓ', 'ﺔ'), // teh marbuta (right-only)
        };

        private static readonly HashSet<char> RightJoiningOnly = new HashSet<char>
        {
            'آ', 'أ', 'ؤ', 'إ', 'ا',
            'د', 'ذ', 'ر', 'ز', 'و',
            'ة', 'ى'
        };

        private static readonly HashSet<char> NonJoining = new HashSet<char> { 'ء' };

        // Arabic combining diacritics (harakat/tanween/shadda/sukun + dagger alef). These are
        // Unicode "joining type: Transparent" - they must not break the connection between the
        // base letters on either side of them.
        private static readonly HashSet<char> CombiningMarks = new HashSet<char>
        {
            'ً', 'ٌ', 'ٍ', 'َ', 'ُ', 'ِ', 'ّ', 'ْ', 'ٰ',
        };

        private static JoinType GetJoinType(char c)
        {
            if (NonJoining.Contains(c)) return JoinType.NonJoining;
            if (RightJoiningOnly.Contains(c)) return JoinType.RightOnly;
            if (FormTable.ContainsKey(c)) return JoinType.Dual;
            return JoinType.NonJoining; // not an Arabic letter at all
        }

        public static bool IsArabicLetter(char c) => FormTable.ContainsKey(c) || NonJoining.Contains(c);

        public static bool IsCombiningMark(char c) => CombiningMarks.Contains(c);

        public static bool ContainsArabic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
                if (IsArabicLetter(c)) return true;
            return false;
        }

        // Nearest real base letter before/after index i, skipping over combining marks -
        // harakat don't break a joining connection between the letters around them.
        private static int PrevBaseIndex(string s, int i)
        {
            int j = i - 1;
            while (j >= 0 && IsCombiningMark(s[j])) j--;
            return j >= 0 && IsArabicLetter(s[j]) ? j : -1;
        }

        private static int NextBaseIndex(string s, int i)
        {
            int j = i + 1;
            while (j < s.Length && IsCombiningMark(s[j])) j++;
            return j < s.Length && IsArabicLetter(s[j]) ? j : -1;
        }

        // Shapes Arabic text for display: selects the correct joined glyph for every letter
        // based on its neighbors, in place, without reordering or changing string length.
        // Safe to use on live TMP_InputField text (see ArabicInputShaper) and on any static
        // TMP_Text - pair with TMP_Text.isRightToLeftText = true for RTL layout in both cases.
        public static string Shape(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
                sb.Append(ShapeCharAt(input, i));
            return sb.ToString();
        }

        private static char ShapeCharAt(string s, int i)
        {
            char c = s[i];
            if (!FormTable.TryGetValue(c, out Forms forms)) return c;

            int prevIdx = PrevBaseIndex(s, i);
            int nextIdx = NextBaseIndex(s, i);

            bool linksToPrev = prevIdx >= 0 && GetJoinType(s[prevIdx]) == JoinType.Dual;
            bool linksToNext = GetJoinType(c) == JoinType.Dual && nextIdx >= 0 && GetJoinType(s[nextIdx]) != JoinType.NonJoining;

            if (linksToPrev && linksToNext) return forms.Medial;
            if (linksToPrev) return forms.Final;
            if (linksToNext) return forms.Initial;
            return c; // isolated position - base codepoint (Cairo has no isolated presentation-form glyphs)
        }
    }
}
