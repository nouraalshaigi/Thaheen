using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BuildingInteractionSystem.Editor
{
    // Narrow, surgical repair for BuildingPopup.prefab - does NOT rebuild the hierarchy (see
    // BuildingPopupPrefabBuilder for that). Run manually and deliberately via the menu item
    // below. Idempotent: safe to re-run, never creates a duplicate RiyalIcon.
    //
    // Fixes applied:
    // 1. Every transaction popup's InputField: clears manual-edit drift (non-uniform
    //    LocalScale, non-zero AnchoredPosition/SizeDelta on what should be a clean stretch
    //    rect) and ensures exactly one real "RiyalIcon" Image child (the separate Riyal
    //    sprite - never baked into the input background, never a duplicate text).
    // 2. SavingsPopup's progress readouts (ProgressTrack/ProgressPercentageText/
    //    CurrentSavedText/GoalTargetText/RemainingAmountText) are repositioned to match
    //    Assets/Buildings_PopUp/PopUp_reference/Saveup_popup.png, measured directly off that
    //    image (grid-overlay pixel measurement, not guessed).
    internal static class BuildingPopupInputFix
    {
        private const string PrefabPath = "Assets/UI/BuildingInteraction/Prefabs/BuildingPopup.prefab";
        private const string RiyalIconPath = "Assets/Buildings_PopUp/PopUp_use/ريال (1).png";

        [MenuItem("Tools/Buildings Popup/Fix Input Backgrounds And Savings Layout")]
        public static void Fix()
        {
            Sprite riyalIcon = AssetDatabase.LoadAssetAtPath<Sprite>(RiyalIconPath);
            if (riyalIcon == null)
            {
                Debug.LogError($"BuildingPopupInputFix: '{RiyalIconPath}' not found.");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform overlay = prefabRoot.transform.Find("Overlay");
                if (overlay == null)
                {
                    Debug.LogError("BuildingPopupInputFix: 'Overlay' not found - prefab hierarchy looks incomplete. " +
                        "This script only repairs an existing hierarchy; it will not build one.");
                    return;
                }

                bool ok = true;
                ok &= FixInputField(overlay, "CharityPopup", riyalIcon);
                ok &= FixInputField(overlay, "MallPopup", riyalIcon);
                ok &= FixInputField(overlay, "SavingsPopup", riyalIcon);
                ok &= FixSavingsProgressLayout(overlay);

                if (!ok)
                {
                    Debug.LogError("BuildingPopupInputFix: one or more expected child objects were missing - " +
                        "see errors above. No changes were saved.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                Debug.Log("BuildingPopupInputFix: input backgrounds, Riyal icons, and Savings progress layout fixed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool FixInputField(Transform overlay, string popupName, Sprite riyalIcon)
        {
            Transform popup = overlay.Find(popupName);
            Transform inputField = popup != null ? popup.Find("Background/InputField") : null;
            if (inputField == null)
            {
                Debug.LogError($"BuildingPopupInputFix: '{popupName}/Background/InputField' not found.");
                return false;
            }

            RectTransform inputRT = (RectTransform)inputField;

            // Clear manual-edit drift: a stretch-anchored rect (anchorMin != anchorMax) should
            // always have zero AnchoredPosition/SizeDelta and uniform scale - anything else is
            // a leftover Rect-tool nudge, not an intentional layout value.
            inputRT.localScale = Vector3.one;
            inputRT.anchoredPosition = Vector2.zero;
            inputRT.sizeDelta = Vector2.zero;

            // Riyal icon: exactly one, real sprite, never baked into the background.
            Transform existingIcon = inputField.Find("RiyalIcon");
            RectTransform riyalRT = existingIcon != null ? (RectTransform)existingIcon : null;
            if (riyalRT == null)
            {
                GameObject go = new GameObject("RiyalIcon", typeof(RectTransform));
                go.transform.SetParent(inputField, false);
                riyalRT = (RectTransform)go.transform;
                riyalRT.SetAsFirstSibling(); // draw under/before the text, matching the reference
                Image img = go.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            riyalRT.anchorMin = new Vector2(0f, 0.5f);
            riyalRT.anchorMax = new Vector2(0f, 0.5f);
            riyalRT.pivot = new Vector2(0f, 0.5f);
            riyalRT.sizeDelta = new Vector2(28f, 16f);
            riyalRT.anchoredPosition = new Vector2(16f, 0f);
            riyalRT.localScale = Vector3.one;
            Image riyalImg = riyalRT.GetComponent<Image>();
            if (riyalImg == null) riyalImg = riyalRT.gameObject.AddComponent<Image>();
            riyalImg.sprite = riyalIcon;
            riyalImg.preserveAspect = true;
            riyalImg.raycastTarget = false;

            // Leave room for the icon so the input value doesn't render on top of it.
            Transform textArea = inputField.Find("Text Area");
            if (textArea != null)
            {
                RectTransform textAreaRT = (RectTransform)textArea;
                textAreaRT.offsetMin = new Vector2(52f, textAreaRT.offsetMin.y);
            }

            // "0" is the TMP_InputField's Placeholder, not a real value - it disappears the
            // instant the player types anything (standard TMP_InputField behavior) and is never
            // written to PlayerDataManager. The actual typed amount only ever comes from
            // TMP_InputField.text (see TransactionPopupView.TryGetAmount).
            TMP_InputField field = inputField.GetComponent<TMP_InputField>();
            if (field != null && field.placeholder is TMP_Text placeholderText)
                placeholderText.text = "0";

            return true;
        }

        // Fractions measured directly off Assets/Buildings_PopUp/PopUp_reference/Saveup_popup.png
        // (540x525) via a pixel-grid overlay - not guessed. The reference's example values
        // (SavedAmount=444, GoalTargetAmount=4404) match this project's own worked example.
        private static bool FixSavingsProgressLayout(Transform overlay)
        {
            Transform savings = overlay.Find("SavingsPopup");
            Transform background = savings != null ? savings.Find("Background") : null;
            if (background == null)
            {
                Debug.LogError("BuildingPopupInputFix: 'SavingsPopup/Background' not found.");
                return false;
            }

            Transform track = background.Find("ProgressTrack");
            Transform dynamicTexts = background.Find("DynamicTexts");
            Transform inputField = background.Find("InputField");
            if (track == null || dynamicTexts == null)
            {
                Debug.LogError("BuildingPopupInputFix: 'SavingsPopup/Background/ProgressTrack' or 'DynamicTexts' not found.");
                return false;
            }

            Transform percentText = track.Find("ProgressPercentageText");
            Transform savedText = dynamicTexts.Find("CurrentSavedText");
            Transform targetText = dynamicTexts.Find("GoalTargetText");
            Transform remainingText = dynamicTexts.Find("RemainingAmountText");
            if (percentText == null || savedText == null || targetText == null || remainingText == null)
            {
                Debug.LogError("BuildingPopupInputFix: one or more Savings progress text children not found.");
                return false;
            }

            // ProgressTrack - the progress "card". Reference: y155-215 (60px tall), x95-445.
            RectTransform trackRT = (RectTransform)track;
            SetStretch(trackRT, 0.176f, 0.5905f, 0.8185f, 0.7048f);

            // ProgressPercentageText - top-left inside the card ("0%" in the reference), not centered.
            RectTransform percentRT = (RectTransform)percentText;
            percentRT.anchorMin = new Vector2(0f, 1f);
            percentRT.anchorMax = new Vector2(0f, 1f);
            percentRT.pivot = new Vector2(0f, 1f);
            percentRT.sizeDelta = new Vector2(70f, 22f);
            percentRT.anchoredPosition = new Vector2(12f, -8f);
            percentRT.localScale = Vector3.one;
            TMP_Text percentTMP = percentText.GetComponent<TMP_Text>();
            if (percentTMP != null) percentTMP.alignment = TextAlignmentOptions.MidlineLeft;

            // CurrentSavedText - top-right, same row as the baked "قرّب من هدفك" label
            // (reference shows "444 ريال" here - matches this project's SavedAmount example).
            RectTransform savedRT = (RectTransform)savedText;
            SetStretch(savedRT, 0.7407f, 0.7295f, 0.8241f, 0.7752f);

            // GoalTargetText - bottom-right, at the card's lower edge (reference shows "4404" here).
            RectTransform targetRT = (RectTransform)targetText;
            SetStretch(targetRT, 0.7407f, 0.5905f, 0.8241f, 0.6095f);

            // RemainingAmountText - not shown with an example value in the reference (it depicts
            // the 0% empty state); placed in the small gap between the card and the baked
            // "كم ودك تدخر؟" label so it doesn't collide with either.
            RectTransform remainingRT = (RectTransform)remainingText;
            SetStretch(remainingRT, 0.176f, 0.543f, 0.8185f, 0.581f);
            TMP_Text remainingTMP = remainingText.GetComponent<TMP_Text>();
            if (remainingTMP != null) remainingTMP.alignment = TextAlignmentOptions.Center;

            // InputField - reference shows the pill running y258-300 (42px), a touch taller
            // than the previous estimate.
            if (inputField != null)
                SetStretch((RectTransform)inputField, 0.176f, 0.4286f, 0.8185f, 0.5086f);

            return true;
        }

        private static void SetStretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}
