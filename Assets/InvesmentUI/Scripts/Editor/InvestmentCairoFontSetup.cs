using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace InvestmentTowerUI.Editor
{
    // Generates the minimum required TMP Font Assets (Regular + Bold) from the Cairo TTFs
    // already provided at Assets/InvesmentUI/Fonts/, scoped entirely to the Investment Tower UI
    // - does not touch or replace Assets/Fonts/Cairo-Regular SDF.asset (the separate font asset
    // used by StartFlow/BuildingInteraction popups). Mirrors StartFlow's own
    // CairoFontAssetSetup.cs pattern exactly (same TMP_FontAsset.CreateFontAsset + TryAddCharacters
    // + persisted-asset-first sequencing, and the same [InitializeOnLoad]/delayCall auto-run).
    [InitializeOnLoad]
    internal static class InvestmentCairoFontSetup
    {
        private const string FontsFolder = "Assets/InvesmentUI/Fonts";
        private const string TmpFolder = FontsFolder + "/TMP";
        private const string RegularTtfPath = FontsFolder + "/Cairo-Regular.ttf";
        private const string BoldTtfPath = FontsFolder + "/Cairo-Bold.ttf";
        public const string RegularAssetPath = TmpFolder + "/Cairo-Regular SDF.asset";
        public const string BoldAssetPath = TmpFolder + "/Cairo-Bold SDF.asset";

        private const int ProbeCharacter = 0x0628; // beh - confirms real Arabic glyphs, not just Latin

        static InvestmentCairoFontSetup()
        {
            EditorApplication.delayCall += EnsureFontsGenerated;
        }

        [MenuItem("Tools/Investment Tower/Regenerate Cairo Font Assets")]
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

        public static void EnsureFontsGenerated()
        {
            if (!AssetDatabase.IsValidFolder(TmpFolder))
                AssetDatabase.CreateFolder(FontsFolder, "TMP");

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

                Debug.LogWarning($"InvestmentCairoFontSetup: '{assetPath}' exists but has no Arabic glyphs - deleting and regenerating.");
                AssetDatabase.DeleteAsset(assetPath);
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"InvestmentCairoFontSetup: source font not found at '{ttfPath}' - skipping.");
                return;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 60, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);

            if (fontAsset == null)
            {
                Debug.LogError($"InvestmentCairoFontSetup: TMP_FontAsset.CreateFontAsset returned null for '{ttfPath}'.");
                return;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            SaveSubAssets(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath);

            TMP_FontAsset persisted = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (persisted == null)
            {
                Debug.LogError($"InvestmentCairoFontSetup: failed to reload '{assetPath}' after saving.");
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
                Debug.Log($"InvestmentCairoFontSetup: generated '{assetPath}' - {beforeCount} -> {afterCount} characters, Arabic glyphs confirmed (TryAddCharacters={added}).");
            else
                Debug.LogError($"InvestmentCairoFontSetup: generated '{assetPath}' but Arabic glyphs are still missing (before={beforeCount}, after={afterCount}, allSucceeded={added}).");
        }

        private static bool HasArabicGlyph(TMP_FontAsset fontAsset) =>
            fontAsset.characterLookupTable != null && fontAsset.characterLookupTable.ContainsKey((uint)ProbeCharacter);

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
            AppendRange(sb, 0x0020, 0x007E);
            AppendRange(sb, 0x0600, 0x06FF);
            AppendRange(sb, 0x0750, 0x077F);
            AppendRange(sb, 0xFB50, 0xFDFF);
            AppendRange(sb, 0xFE70, 0xFEFF);
            return sb.ToString();
        }

        private static void AppendRange(StringBuilder sb, int start, int end)
        {
            for (int c = start; c <= end; c++)
                sb.Append((char)c);
        }
    }
}
