using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace StartFlow.EditorTools
{
    // Generates real TMP Font Assets (Dynamic SDF atlas) from the Cairo TTF files at
    // Assets/Fonts/, using TextMeshPro's own official factory API rather than a hand-authored
    // asset. Runs automatically after Unity finishes compiling this script; safe to run
    // repeatedly (skips generation if the asset already exists and already has Arabic glyphs).
    [InitializeOnLoad]
    internal static class CairoFontAssetSetup
    {
        private const string FontsFolder = "Assets/Fonts";
        private const string RegularTtfPath = FontsFolder + "/Cairo-Regular.ttf";
        private const string BoldTtfPath = FontsFolder + "/Cairo-Bold.ttf";
        public const string RegularAssetPath = FontsFolder + "/Cairo-Regular SDF.asset";
        public const string BoldAssetPath = FontsFolder + "/Cairo-Bold SDF.asset";

        // A representative Arabic letter (beh, U+0628) used to verify the atlas actually
        // contains real Arabic glyphs, not just the Latin set CreateFontAsset adds by default.
        private const int ProbeCharacter = 0x0628;

        static CairoFontAssetSetup()
        {
            EditorApplication.delayCall += EnsureFontsGenerated;
        }

        [MenuItem("Tools/StartFlow/Regenerate Cairo Font Assets")]
        private static void ForceRegenerate()
        {
            DeleteIfExists(RegularAssetPath);
            DeleteIfExists(BoldAssetPath);
            EnsureFontsGenerated();
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        // Idempotent - safe to call from anywhere (e.g. StartFlowSceneBuilder) to guarantee
        // the font assets exist before they're needed. If a previously-generated asset is
        // missing Arabic glyphs (from the earlier broken generation order), it is deleted and
        // rebuilt automatically rather than being trusted as "already done".
        public static void EnsureFontsGenerated()
        {
            GenerateIfMissingOrBroken(RegularTtfPath, RegularAssetPath);
            GenerateIfMissingOrBroken(BoldTtfPath, BoldAssetPath);
        }

        private static void GenerateIfMissingOrBroken(string ttfPath, string assetPath)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                if (HasArabicGlyph(existing))
                    return;

                Debug.LogWarning($"CairoFontAssetSetup: '{assetPath}' exists but has no Arabic glyphs (leftover from a broken generation) - deleting and regenerating.");
                AssetDatabase.DeleteAsset(assetPath);
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"CairoFontAssetSetup: source font not found at '{ttfPath}' - skipping TMP Font Asset generation. Re-import the Cairo TTF and reopen the project if this persists.");
                return;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                60,
                6,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError($"CairoFontAssetSetup: TMP_FontAsset.CreateFontAsset returned null for '{ttfPath}'. Generate the font asset manually via Window > TextMeshPro > Font Asset Creator instead.");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);

            // Persist the asset (with its default/initial character set) to disk FIRST.
            // TryAddCharacters must run against an already-persisted asset - calling it on a
            // purely in-memory TMP_FontAsset before AssetDatabase.CreateAsset silently fails
            // to add anything (this was the root cause of the original Arabic-glyphs-missing
            // bug: the Latin set that did appear came from CreateFontAsset's own defaults,
            // not from the pre-warm call, which had no effect at all).
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            SaveSubAssets(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);

            TMP_FontAsset persisted = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (persisted == null)
            {
                Debug.LogError($"CairoFontAssetSetup: failed to reload '{assetPath}' after saving - aborting glyph population.");
                return;
            }

            int beforeCount = persisted.characterTable.Count;
            bool added = persisted.TryAddCharacters(BuildPrewarmCharacterSet());
            int afterCount = persisted.characterTable.Count;

            SaveSubAssets(persisted);
            EditorUtility.SetDirty(persisted);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);

            TMP_FontAsset verify = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            bool arabicConfirmed = verify != null && HasArabicGlyph(verify);

            if (arabicConfirmed)
            {
                Debug.Log($"CairoFontAssetSetup: generated '{assetPath}' from '{ttfPath}' - {beforeCount} -> {afterCount} characters, Arabic glyphs CONFIRMED present (TryAddCharacters returned {added}).");
            }
            else
            {
                Debug.LogError($"CairoFontAssetSetup: generated '{assetPath}' but Arabic glyphs are STILL missing after TryAddCharacters (before={beforeCount}, after={afterCount}, allSucceeded={added}). This font asset will show tofu boxes for Arabic text - manual generation via Window > TextMeshPro > Font Asset Creator (source: '{ttfPath}', Character Set: Unicode Range, include the Arabic block 0600-06FF) is required.");
            }
        }

        private static bool HasArabicGlyph(TMP_FontAsset fontAsset)
        {
            return fontAsset.characterLookupTable != null && fontAsset.characterLookupTable.ContainsKey((uint)ProbeCharacter);
        }

        private static void SaveSubAssets(TMP_FontAsset fontAsset)
        {
            if (fontAsset.atlasTextures != null)
            {
                foreach (Texture2D atlas in fontAsset.atlasTextures)
                {
                    if (atlas != null && AssetDatabase.GetAssetPath(atlas) != AssetDatabase.GetAssetPath(fontAsset))
                        AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }

            if (fontAsset.material != null && AssetDatabase.GetAssetPath(fontAsset.material) != AssetDatabase.GetAssetPath(fontAsset))
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        private static string BuildPrewarmCharacterSet()
        {
            var sb = new StringBuilder();

            AppendRange(sb, 0x0020, 0x007E); // Basic Latin: digits, punctuation, spaces
            AppendRange(sb, 0x0600, 0x06FF); // Arabic
            AppendRange(sb, 0x0750, 0x077F); // Arabic Supplement
            AppendRange(sb, 0xFB50, 0xFDFF); // Arabic Presentation Forms-A (contextual/joined forms)
            AppendRange(sb, 0xFE70, 0xFEFF); // Arabic Presentation Forms-B (contextual/joined forms)

            return sb.ToString();
        }

        private static void AppendRange(StringBuilder sb, int start, int end)
        {
            for (int c = start; c <= end; c++)
                sb.Append((char)c);
        }
    }
}
