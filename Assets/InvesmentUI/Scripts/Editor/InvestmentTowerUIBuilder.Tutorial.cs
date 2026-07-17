using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InvestmentTowerUI.Editor
{
    internal static partial class InvestmentTowerUIBuilder
    {
        // Tutorial backgrounds (FirstPage/SeconedPage) already bake the entire educational
        // content (title, progress dots, example card, feature rows) as static art - matching
        // "text images only when decorative and cannot be reproduced correctly" (this content
        // has no dynamic data at all). Only two real interactive elements are layered on top:
        // a close button and the "next"/"start" primary button.
        private static void BuildTutorialStep1(Transform parent, Art art, Prefabs prefabs, ControllerRefs r)
        {
            RectTransform popup = BuildPanelWithPopup(parent, "TutorialStep1Panel", art.FirstPage, out GameObject panelRoot);
            r.tutorialStep1Panel = panelRoot;

            r.tutorialStep1Header = InstantiateHeader(popup, prefabs, art, string.Empty, showBack: false);

            RectTransform bottomActions = CreateUIObject("BottomActions", popup);
            bottomActions.anchorMin = new Vector2(0.5f, 0f);
            bottomActions.anchorMax = new Vector2(0.5f, 0f);
            bottomActions.pivot = new Vector2(0.5f, 0f);
            bottomActions.anchoredPosition = new Vector2(0f, 24f);
            bottomActions.sizeDelta = new Vector2(347f, 56f);

            GameObject buttonGO = Object.Instantiate(prefabs.PrimaryButton, bottomActions);
            buttonGO.name = "NextButton";
            StretchFull((RectTransform)buttonGO.transform);
            SetButtonLabel(buttonGO, "فهمت، وش الخطوة الجاية؟", art);
            r.tutorialStep1NextButton = buttonGO.GetComponent<Button>();
        }

        private static void BuildTutorialStep2(Transform parent, Art art, Prefabs prefabs, ControllerRefs r)
        {
            RectTransform popup = BuildPanelWithPopup(parent, "TutorialStep2Panel", art.SeconedPage, out GameObject panelRoot);
            r.tutorialStep2Panel = panelRoot;

            r.tutorialStep2Header = InstantiateHeader(popup, prefabs, art, string.Empty, showBack: false);

            RectTransform bottomActions = CreateUIObject("BottomActions", popup);
            bottomActions.anchorMin = new Vector2(0.5f, 0f);
            bottomActions.anchorMax = new Vector2(0.5f, 0f);
            bottomActions.pivot = new Vector2(0.5f, 0f);
            bottomActions.anchoredPosition = new Vector2(0f, 24f);
            bottomActions.sizeDelta = new Vector2(347f, 56f);

            GameObject buttonGO = Object.Instantiate(prefabs.PrimaryButton, bottomActions);
            buttonGO.name = "StartInvestingButton";
            StretchFull((RectTransform)buttonGO.transform);
            SetButtonLabel(buttonGO, "ابدأ الاستثمار", art);
            r.tutorialStep2StartButton = buttonGO.GetComponent<Button>();
        }

        // ------------------------------------------------------------------------ shared small helpers

        private static InvestmentPopupHeaderView InstantiateHeader(Transform panel, Prefabs prefabs, Art art, string titleArabic, bool showBack)
        {
            GameObject headerGO = Object.Instantiate(prefabs.PopupHeader, panel);
            RectTransform rt = (RectTransform)headerGO.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;

            InvestmentPopupHeaderView view = headerGO.GetComponent<InvestmentPopupHeaderView>();
            view.SetTitle(string.IsNullOrEmpty(titleArabic) ? string.Empty : StartFlow.Arabic.ArabicTextShaper.Shape(titleArabic));
            view.SetMode(showBack);
            return view;
        }

        private static void SetButtonLabel(GameObject buttonGO, string arabicText, Art art)
        {
            TMPro.TMP_Text label = buttonGO.transform.Find("Label")?.GetComponent<TMPro.TMP_Text>();
            if (label != null) label.text = StartFlow.Arabic.ArabicTextShaper.Shape(arabicText);
        }
    }
}
