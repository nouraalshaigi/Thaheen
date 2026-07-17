using BuildingInteractionSystem;
using CityHud;
using Dhaheen;
using InvestmentTowerUI;
using InvestmentTowerUI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityHud.Editor
{
    // Builds the permanent City_HUD top bar (+ its AI Report placeholder panel) directly into
    // the already-open OGscene, as a sibling of AI_Alinma_Investment_Tower_UI under the existing
    // BuildingInteraction_Canvas - never creates a new Canvas/EventSystem, never touches the
    // tower UI, camera, player, buildings, or city layout. Idempotent: destroys and rebuilds only
    // its own "City_HUD" GameObject in place, so re-running never leaves duplicates.
    //
    // Reuses InvestmentTowerUIBuilder's internal UI-construction helpers (CreateUIObject,
    // CreateText, AddHorizontalLayout, AddVerticalLayout, AddLayoutElement, StretchFull) rather
    // than duplicating them - both classes live in the same assembly (no .asmdef splits this
    // project) so InvestmentTowerUI.Editor's `internal` members are directly reachable here.
    internal static class CityHudBuilder
    {
        private const string ScenePath = "Assets/OGscene.unity";
        private const string CanvasObjectName = "BuildingInteraction_Canvas";
        private const string TowerRootObjectName = "AI_Alinma_Investment_Tower_UI";
        private const string HudObjectName = "City_HUD";

        private const string RegularFontPath = "Assets/InvesmentUI/Fonts/TMP/Cairo-Regular SDF.asset";
        private const string BoldFontPath = "Assets/InvesmentUI/Fonts/TMP/Cairo-Bold SDF.asset";

        private const string ArtRoot = "Assets/City_HUB";

        private static readonly Color BarTextGreen = new Color(0.239f, 0.722f, 0.416f, 1f); // matches InvestmentPalette.Positive
        private static readonly Color TextPrimary = new Color(0.949f, 0.961f, 0.969f, 1f);   // matches InvestmentPalette.TextPrimary
        private static readonly Color TextMuted = new Color(0.663f, 0.714f, 0.769f, 1f);     // matches InvestmentPalette.TextMuted
        private static readonly Color32 CardNavy = new Color32(8, 20, 34, 217);              // sampled from Player_recored.png

        [MenuItem("Tools/City HUD/Build City HUD", true)]
        private static bool ValidateNotPlaying() => !EditorApplication.isPlaying;

        [MenuItem("Tools/City HUD/Build City HUD")]
        public static void BuildCityHud()
        {
            Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"CityHudBuilder: '{ScenePath}' is not open - open it in the Editor first.");
                return;
            }

            GameObject canvasGO = FindInScene(scene, CanvasObjectName);
            if (canvasGO == null)
            {
                Debug.LogError($"CityHudBuilder: '{CanvasObjectName}' not found in '{ScenePath}' - not creating a new canvas.");
                return;
            }

            TMP_FontAsset regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (regularFont == null || boldFont == null)
            {
                Debug.LogError("CityHudBuilder: Cairo TMP font assets not found at the expected InvesmentUI paths.");
                return;
            }

            Sprite barSprite = LoadAsSprite($"{ArtRoot}/Containers/Player_recored.png", new Vector4(21, 0, 21, 0));
            Sprite dividerSprite = LoadAsSprite($"{ArtRoot}/Containers/Spacing.png", Vector4.zero);
            Sprite moneyIcon = LoadAsSprite($"{ArtRoot}/Icons/Remaining_icon.png", Vector4.zero);
            Sprite goalIcon = LoadAsSprite($"{ArtRoot}/Icons/Precentage_icon.png", Vector4.zero);
            Sprite nameIcon = LoadAsSprite($"{ArtRoot}/Icons/Name_Icon.png", Vector4.zero);
            Sprite reportIcon = LoadAsSprite($"{ArtRoot}/Buttons/AI_financial_report_And_Logout_Button.png", Vector4.zero);
            if (barSprite == null || dividerSprite == null || moneyIcon == null || goalIcon == null || nameIcon == null || reportIcon == null)
            {
                Debug.LogError("CityHudBuilder: one or more Assets/City_HUB art files could not be loaded as sprites.");
                return;
            }

            // Idempotent rebuild-in-place - never leaves a duplicate City_HUD behind.
            Transform existing = canvasGO.transform.Find(HudObjectName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            RectTransform hud = InvestmentTowerUIBuilder.CreateUIObject(HudObjectName, canvasGO.transform);
            InvestmentTowerUIBuilder.StretchFull(hud);

            // Sits behind the tower UI in draw order so an open Investment Tower popup still
            // renders on top of this permanent bar rather than being covered by it.
            Transform towerRoot = canvasGO.transform.Find(TowerRootObjectName);
            hud.SetSiblingIndex(towerRoot != null ? towerRoot.GetSiblingIndex() : 0);

            var refs = new HudRefs();
            BuildTopBar(hud, boldFont, barSprite, dividerSprite, moneyIcon, goalIcon, nameIcon, reportIcon, refs);
            BuildAiReportPanel(hud, regularFont, boldFont, refs);

            CityHudController controller = hud.gameObject.AddComponent<CityHudController>();
            WireController(controller, refs);

            DhaheenResultsUI resultsUI = refs.aiReportPanel.AddComponent<DhaheenResultsUI>();
            WireResultsUI(resultsUI, refs);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("CityHudBuilder: City_HUD built under BuildingInteraction_Canvas and scene saved.");
        }

        private class HudRefs
        {
            public TMP_Text remainingMoneyText, goalProgressText, playerNameText;
            public Button settingsButton, aiReportButton, exitButton, aiReportCloseButton;
            public GameObject aiReportPanel;

            // AI report result display (Dhaheen integration) - see DhaheenResultsUI.
            public GameObject statusState, resultState;
            public TMP_Text statusText, personalityText, reportTitleText, reportSummaryText, reportTipText;
            public SummaryRowView overallScoreRow, savingScoreRow, spendingControlScoreRow, investmentScoreRow, emergencyReadinessScoreRow;
            public Transform recommendationsContent;
            public TMP_Text recommendationTemplate;
        }

        // ------------------------------------------------------------------------ TopBar

        private static void BuildTopBar(RectTransform hud, TMP_FontAsset bold,
            Sprite barSprite, Sprite dividerSprite, Sprite moneyIcon, Sprite goalIcon, Sprite nameIcon, Sprite reportIcon,
            HudRefs refs)
        {
            RectTransform bar = InvestmentTowerUIBuilder.CreateUIObject("TopBar", hud);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(-32f, 48f);
            bar.anchoredPosition = new Vector2(0f, -12f);

            Image barBg = bar.gameObject.AddComponent<Image>();
            barBg.sprite = barSprite;
            barBg.type = Image.Type.Sliced;
            barBg.raycastTarget = true;

            RectTransform content = InvestmentTowerUIBuilder.CreateUIObject("Content", bar);
            InvestmentTowerUIBuilder.StretchFull(content);
            content.offsetMin = new Vector2(10f, 4f);
            content.offsetMax = new Vector2(-10f, -4f);
            InvestmentTowerUIBuilder.AddHorizontalLayout(content.gameObject, 12f);

            // LeftGroup - Settings (its own single-child layout group so the button's
            // LayoutElement is actually honored, matching RightGroup's approach below)
            RectTransform leftGroup = InvestmentTowerUIBuilder.CreateUIObject("LeftGroup", content);
            InvestmentTowerUIBuilder.AddLayoutElement(leftGroup.gameObject, preferredWidth: 40).flexibleWidth = 0;
            InvestmentTowerUIBuilder.AddHorizontalLayout(leftGroup.gameObject, 0f);
            refs.settingsButton = BuildIconButton(leftGroup, "SettingsButton", BuildGearIcon(), TextPrimary);

            // CenterGroup - flexible, 3 stat clusters + dividers, centered
            RectTransform centerGroup = InvestmentTowerUIBuilder.CreateUIObject("CenterGroup", content);
            InvestmentTowerUIBuilder.AddLayoutElement(centerGroup.gameObject, flexibleWidth: 1);
            InvestmentTowerUIBuilder.AddHorizontalLayout(centerGroup.gameObject, 10f);

            refs.remainingMoneyText = BuildStatCluster(centerGroup, bold, "MoneyGroup", moneyIcon, "0", BarTextGreen, 80f);
            BuildDivider(centerGroup, dividerSprite);
            refs.goalProgressText = BuildStatCluster(centerGroup, bold, "GoalGroup", goalIcon, "0%", BarTextGreen, 46f);
            BuildDivider(centerGroup, dividerSprite);
            refs.playerNameText = BuildStatCluster(centerGroup, bold, "NameGroup", nameIcon, "-", TextPrimary, 100f);

            // RightGroup - AI Report + Exit
            RectTransform rightGroup = InvestmentTowerUIBuilder.CreateUIObject("RightGroup", content);
            InvestmentTowerUIBuilder.AddLayoutElement(rightGroup.gameObject, preferredWidth: 84).flexibleWidth = 0;
            InvestmentTowerUIBuilder.AddHorizontalLayout(rightGroup.gameObject, 8f);
            refs.aiReportButton = BuildIconButton(rightGroup, "AIReportButton", reportIcon, TextPrimary);
            refs.exitButton = BuildIconButton(rightGroup, "ExitButton", BuildExitIcon(), TextPrimary);
        }

        // icon + value pair (e.g. Riyal icon before the money amount). valueWidth reserves
        // enough room for that field's longest expected text without disturbing its siblings.
        private static TMP_Text BuildStatCluster(Transform parent, TMP_FontAsset bold, string groupName,
            Sprite icon, string placeholder, Color valueColor, float valueWidth)
        {
            RectTransform group = InvestmentTowerUIBuilder.CreateUIObject(groupName, parent);
            InvestmentTowerUIBuilder.AddLayoutElement(group.gameObject, flexibleWidth: 0);
            InvestmentTowerUIBuilder.AddHorizontalLayout(group.gameObject, 5f);

            RectTransform iconRT = InvestmentTowerUIBuilder.CreateUIObject("Icon", group);
            InvestmentTowerUIBuilder.AddLayoutElement(iconRT.gameObject, preferredWidth: 14, preferredHeight: 14).flexibleWidth = 0;
            Image iconImg = iconRT.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.color = valueColor;

            TMP_Text value = InvestmentTowerUIBuilder.CreateText(group, "ValueText", placeholder, 13f, valueColor, TextAlignmentOptions.MidlineLeft, false, bold, FontStyles.Bold);
            InvestmentTowerUIBuilder.AddLayoutElement(value.gameObject, preferredWidth: valueWidth).flexibleWidth = 0;
            return value;
        }

        private static void BuildDivider(Transform parent, Sprite dividerSprite)
        {
            RectTransform div = InvestmentTowerUIBuilder.CreateUIObject("Divider", parent);
            InvestmentTowerUIBuilder.AddLayoutElement(div.gameObject, preferredWidth: 2, preferredHeight: 18).flexibleWidth = 0;
            Image img = div.gameObject.AddComponent<Image>();
            img.sprite = dividerSprite;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
        }

        // Circular subtle-background icon button - matches the settings/report/exit slots.
        private static Button BuildIconButton(Transform parent, string name, Sprite icon, Color iconColor)
        {
            RectTransform btnRT = InvestmentTowerUIBuilder.CreateUIObject(name, parent);
            InvestmentTowerUIBuilder.AddLayoutElement(btnRT.gameObject, preferredWidth: 34, preferredHeight: 34).flexibleWidth = 0;
            Image bg = btnRT.gameObject.AddComponent<Image>();
            bg.sprite = RoundedRectSpriteFactory.GetRoundedSprite(17, 68);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.06f);
            Button btn = btnRT.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;

            RectTransform iconRT = InvestmentTowerUIBuilder.CreateUIObject("Icon", btnRT);
            iconRT.anchorMin = new Vector2(0.5f, 0.5f);
            iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.sizeDelta = new Vector2(16f, 16f);
            Image iconImg = iconRT.gameObject.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.color = iconColor;

            return btn;
        }

        // ------------------------------------------------------------------------ AI Report panel (placeholder)

        // Same DimBlocker/Card/CloseButton identity and rounded-navy visual style as before -
        // only the Card's content grew (it now displays the Dhaheen analysis result instead of a
        // static "coming soon" placeholder) and its size increased to fit that content.
        private static void BuildAiReportPanel(RectTransform hud, TMP_FontAsset regular, TMP_FontAsset bold, HudRefs refs)
        {
            RectTransform panel = InvestmentTowerUIBuilder.CreateUIObject("AIReportPanel", hud);
            InvestmentTowerUIBuilder.StretchFull(panel);
            panel.gameObject.SetActive(false);
            refs.aiReportPanel = panel.gameObject;

            RectTransform blocker = InvestmentTowerUIBuilder.CreateUIObject("DimBlocker", panel);
            InvestmentTowerUIBuilder.StretchFull(blocker);
            Image blockerImg = blocker.gameObject.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.55f);
            blockerImg.raycastTarget = true; // blocks city clicks while the panel is open

            RectTransform card = InvestmentTowerUIBuilder.CreateUIObject("Card", panel);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(460f, 560f);
            Image cardBg = card.gameObject.AddComponent<Image>();
            cardBg.sprite = RoundedRectSpriteFactory.GetRoundedSprite(20, 160);
            cardBg.type = Image.Type.Sliced;
            cardBg.color = CardNavy;
            InvestmentTowerUIBuilder.AddVerticalLayout(card.gameObject, 12f, new RectOffset(22, 22, 20, 20), TextAnchor.UpperCenter);

            InvestmentTowerUIBuilder.CreateText(card, "TitleText", "التقرير المالي الذكي", 18f, TextPrimary, TextAlignmentOptions.Center, true, bold, FontStyles.Bold);

            RectTransform scrollRoot = BuildScrollArea(card, "ResultScrollView", 10f, out Transform resultContent);
            InvestmentTowerUIBuilder.AddLayoutElement(scrollRoot.gameObject, flexibleHeight: 1);

            // StatusState / ResultState - only one active at a time, exactly like the
            // History/Summary empty-state pattern in InvestmentTowerUIBuilder.Portfolio.cs.
            RectTransform statusState = InvestmentTowerUIBuilder.CreateUIObject("StatusState", resultContent);
            InvestmentTowerUIBuilder.AddVerticalLayout(statusState.gameObject, 6f, new RectOffset(4, 4, 20, 20), TextAnchor.MiddleCenter);
            TMP_Text statusText = InvestmentTowerUIBuilder.CreateText(statusState, "StatusText", "جاري تحليل قراراتك المالية...", 14f, TextMuted, TextAlignmentOptions.Center, true, regular);
            statusText.enableWordWrapping = true;
            refs.statusState = statusState.gameObject;
            refs.statusText = statusText;

            RectTransform resultState = InvestmentTowerUIBuilder.CreateUIObject("ResultState", resultContent);
            InvestmentTowerUIBuilder.AddVerticalLayout(resultState.gameObject, 10f);
            resultState.gameObject.SetActive(false);
            refs.resultState = resultState.gameObject;

            refs.personalityText = InvestmentTowerUIBuilder.CreateText(resultState, "PersonalityText", "-", 17f, BarTextGreen, TextAlignmentOptions.Center, false, bold, FontStyles.Bold);

            TMP_Text reportTitleText = InvestmentTowerUIBuilder.CreateText(resultState, "ReportTitleText", "-", 15f, TextPrimary, TextAlignmentOptions.Center, false, bold, FontStyles.Bold);
            reportTitleText.enableWordWrapping = true;
            refs.reportTitleText = reportTitleText;

            TMP_Text reportSummaryText = InvestmentTowerUIBuilder.CreateText(resultState, "ReportSummaryText", "-", 13f, TextMuted, TextAlignmentOptions.Center, false, regular);
            reportSummaryText.enableWordWrapping = true;
            refs.reportSummaryText = reportSummaryText;

            TMP_Text reportTipText = InvestmentTowerUIBuilder.CreateText(resultState, "ReportTipText", "-", 13f, BarTextGreen, TextAlignmentOptions.Center, false, regular);
            reportTipText.enableWordWrapping = true;
            refs.reportTipText = reportTipText;

            GameObject summaryRowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/InvesmentUI/Prefabs/SummaryRow.prefab");
            if (summaryRowPrefab == null)
                Debug.LogError("CityHudBuilder: Assets/InvesmentUI/Prefabs/SummaryRow.prefab not found - run Tools/Investment Tower/Rebuild UI (manual only) first, then re-run this.");
            else
            {
                RectTransform scoresSection = InvestmentTowerUIBuilder.CreateUIObject("ScoresSection", resultState);
                InvestmentTowerUIBuilder.AddVerticalLayout(scoresSection.gameObject, 6f);
                refs.overallScoreRow = BuildScoreRow(scoresSection, summaryRowPrefab, "OverallScoreRow");
                refs.savingScoreRow = BuildScoreRow(scoresSection, summaryRowPrefab, "SavingScoreRow");
                refs.spendingControlScoreRow = BuildScoreRow(scoresSection, summaryRowPrefab, "SpendingControlScoreRow");
                refs.investmentScoreRow = BuildScoreRow(scoresSection, summaryRowPrefab, "InvestmentScoreRow");
                refs.emergencyReadinessScoreRow = BuildScoreRow(scoresSection, summaryRowPrefab, "EmergencyReadinessScoreRow");
            }

            RectTransform recSection = InvestmentTowerUIBuilder.CreateUIObject("RecommendationsSection", resultState);
            InvestmentTowerUIBuilder.AddVerticalLayout(recSection.gameObject, 6f, new RectOffset(4, 4, 6, 0));
            refs.recommendationsContent = recSection;

            TMP_Text recTemplate = InvestmentTowerUIBuilder.CreateText(recSection, "RecommendationTemplate", "-", 12.5f, TextMuted, TextAlignmentOptions.MidlineRight, false, regular);
            recTemplate.enableWordWrapping = true;
            recTemplate.gameObject.SetActive(false);
            refs.recommendationTemplate = recTemplate;

            RectTransform closeBtn = InvestmentTowerUIBuilder.CreateUIObject("CloseButton", card);
            InvestmentTowerUIBuilder.AddLayoutElement(closeBtn.gameObject, preferredHeight: 44);
            Image closeBg = closeBtn.gameObject.AddComponent<Image>();
            closeBg.sprite = RoundedRectSpriteFactory.GetRoundedSprite(12, 96);
            closeBg.type = Image.Type.Sliced;
            closeBg.color = new Color(1f, 1f, 1f, 0.08f);
            Button closeButton = closeBtn.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeBg;
            TMP_Text closeLabel = InvestmentTowerUIBuilder.CreateText(closeBtn, "Label", "إغلاق", 15f, TextPrimary, TextAlignmentOptions.Center, true, bold, FontStyles.Bold);
            InvestmentTowerUIBuilder.StretchFull(closeLabel.GetComponent<RectTransform>());
            refs.aiReportCloseButton = closeButton;
        }

        // Local ScrollRect/Viewport/Content builder - InvestmentTowerUIBuilder.BuildScrollList
        // does exactly this but is private to that class, so this mirrors it rather than
        // reaching across classes for a private member.
        private static RectTransform BuildScrollArea(Transform parent, string name, float spacing, out Transform content)
        {
            RectTransform root = InvestmentTowerUIBuilder.CreateUIObject(name, parent);
            ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = InvestmentTowerUIBuilder.CreateUIObject("Viewport", root);
            InvestmentTowerUIBuilder.StretchFull(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

            RectTransform contentRT = InvestmentTowerUIBuilder.CreateUIObject("Content", viewport);
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;
            InvestmentTowerUIBuilder.AddVerticalLayout(contentRT.gameObject, spacing);
            ContentSizeFitter fitter = contentRT.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = contentRT;
            content = contentRT;
            return root;
        }

        // Instantiates one real, saved SummaryRow prefab instance (reused as-is from
        // Assets/InvesmentUI/Prefabs, not a preview/throwaway clone) for a fixed score row.
        private static SummaryRowView BuildScoreRow(Transform parent, GameObject summaryRowPrefab, string rowName)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(summaryRowPrefab, parent);
            go.name = rowName;
            return go.GetComponent<SummaryRowView>();
        }

        // ------------------------------------------------------------------------ procedural icons

        // No gear/exit art exists in the project yet - generated in code (same technique as
        // RoundedRectSpriteFactory) rather than adding a new art dependency.
        private static Sprite BuildGearIcon()
        {
            const int size = 64;
            const int teeth = 8;
            float outerR = size * 0.46f, bodyR = size * 0.34f, holeR = size * 0.15f;
            float angleStep = Mathf.PI * 2f / teeth;
            float toothHalfWidth = angleStep * 0.26f;
            Vector2 center = new Vector2(size / 2f, size / 2f);

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - center.x;
                    float py = y + 0.5f - center.y;
                    float dist = Mathf.Sqrt(px * px + py * py);
                    float angle = Mathf.Atan2(py, px);
                    if (angle < 0f) angle += Mathf.PI * 2f;
                    float angleInStep = angle % angleStep;
                    float distFromToothCenter = Mathf.Min(angleInStep, angleStep - angleInStep);
                    float maxR = distFromToothCenter <= toothHalfWidth ? outerR : bodyR;
                    bool filled = dist <= maxR && dist >= holeR;
                    pixels[y * size + x] = filled ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            }
            return CreateIconSprite(size, pixels);
        }

        private static Sprite BuildExitIcon()
        {
            const int size = 64;
            float center = size / 2f;
            float half = size * 0.32f;
            float thickness = size * 0.11f;
            float sqrt2 = Mathf.Sqrt(2f);

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - center;
                    float py = y + 0.5f - center;
                    float radial = Mathf.Max(Mathf.Abs(px), Mathf.Abs(py));
                    float d1 = Mathf.Abs(py - px) / sqrt2;
                    float d2 = Mathf.Abs(py + px) / sqrt2;
                    bool filled = radial <= half && (d1 <= thickness * 0.5f || d2 <= thickness * 0.5f);
                    pixels[y * size + x] = filled ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            }
            return CreateIconSprite(size, pixels);
        }

        private static Sprite CreateIconSprite(int size, Color[] pixels)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------------ art import / wiring / scene helpers

        // The Assets/City_HUB art was handed off with no Sprite import settings configured yet
        // (plain Texture, spriteMode 0) - reconfigures only the specific file at 'path' to a
        // usable UI Sprite, idempotently (a no-op once already configured).
        private static Sprite LoadAsSprite(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void WireController(CityHudController controller, HudRefs r)
        {
            SerializedObject so = new SerializedObject(controller);
            Set(so, "remainingMoneyText", r.remainingMoneyText);
            Set(so, "goalProgressText", r.goalProgressText);
            Set(so, "playerNameText", r.playerNameText);
            Set(so, "settingsButton", r.settingsButton);
            Set(so, "aiReportButton", r.aiReportButton);
            Set(so, "exitButton", r.exitButton);
            Set(so, "aiReportPanel", r.aiReportPanel);
            Set(so, "aiReportCloseButton", r.aiReportCloseButton);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireResultsUI(DhaheenResultsUI resultsUI, HudRefs r)
        {
            SerializedObject so = new SerializedObject(resultsUI);
            Set(so, "statusState", r.statusState);
            Set(so, "statusText", r.statusText);
            Set(so, "resultState", r.resultState);
            Set(so, "personalityText", r.personalityText);
            Set(so, "reportTitleText", r.reportTitleText);
            Set(so, "reportSummaryText", r.reportSummaryText);
            Set(so, "reportTipText", r.reportTipText);
            Set(so, "overallScoreRow", r.overallScoreRow);
            Set(so, "savingScoreRow", r.savingScoreRow);
            Set(so, "spendingControlScoreRow", r.spendingControlScoreRow);
            Set(so, "investmentScoreRow", r.investmentScoreRow);
            Set(so, "emergencyReadinessScoreRow", r.emergencyReadinessScoreRow);
            Set(so, "recommendationsContent", r.recommendationsContent);
            Set(so, "recommendationTemplate", r.recommendationTemplate);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogError($"CityHudBuilder: CityHudController has no serialized field '{propertyName}'.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject rootGO in scene.GetRootGameObjects())
            {
                GameObject found = FindInHierarchy(rootGO.transform, name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindInHierarchy(Transform t, string name)
        {
            if (t.name == name) return t.gameObject;
            for (int i = 0; i < t.childCount; i++)
            {
                GameObject found = FindInHierarchy(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
