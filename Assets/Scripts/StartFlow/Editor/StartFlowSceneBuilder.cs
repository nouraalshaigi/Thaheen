using System.Collections.Generic;
using System.IO;
using StartFlow.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StartFlow.EditorTools
{
    // Builds StartFlowScene.unity entirely from code (real Canvas/RectTransform/Image/
    // Button/TMP_InputField/TextMeshProUGUI components, not hand-authored scene YAML) and
    // saves it to disk. This is a manual, on-demand build (Tools/StartFlow/Rebuild
    // StartFlowScene) plus a one-shot rebuild on the next Editor recompile if the on-disk
    // scene is stale - it does not poll or run in a background loop.
    //
    // Flow: Welcome -> Name -> Goal -> MonthlyMoney -> OGscene. Every screen is built from the
    // real exported Assets/UI_final assets (Backgrounds/Buttons/Inputs/Logo) as actual Image/
    // Button/TMP_InputField components. Assets/UI_final/References/*-refrence.png are used
    // only here, at build time, to measure exact positions/sizes - they are never loaded into
    // or displayed by the scene itself.
    //
    // Arabic strings are left in their natural logical (typed) order everywhere - nothing here
    // reorders or reverses them. StartFlowUIFactory shapes each string through
    // ArabicTextShaper.Shape() (see Assets/Scripts/StartFlow/Arabic/) for connected letter
    // glyphs, and TMP_Text.isRightToLeftText is set wherever Arabic is shown for RTL layout -
    // see ArabicTextShaper's own header comment for why plain TMP RTL alone isn't enough here.
    [InitializeOnLoad]
    internal static class StartFlowSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/StartFlowScene.unity";
        private const string LayoutVersionMarkerPath = "Assets/Scenes/StartFlowLayoutVersion.txt";

        // Bump this whenever the generated layout changes, so a stale previously-generated
        // scene gets rebuilt once on the next Editor recompile instead of silently kept.
        private const string CurrentLayoutVersion = "11-precise-positions-arabic-shaping";

        private const string AssetRoot = "Assets/UI_final";

        static StartFlowSceneBuilder()
        {
            EditorApplication.delayCall += RebuildIfNeededOnce;
        }

        private static void RebuildIfNeededOnce()
        {
            if (File.Exists(ScenePath) && ReadMarker(LayoutVersionMarkerPath) == CurrentLayoutVersion) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Build();
            File.WriteAllText(LayoutVersionMarkerPath, CurrentLayoutVersion);
        }

        private static string ReadMarker(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path).Trim() : null; }
            catch { return null; }
        }

        [MenuItem("Tools/StartFlow/Rebuild StartFlowScene")]
        private static void ForceRebuild()
        {
            // Building constructs GameObjects directly in the currently active scene; in Play
            // Mode that makes AddComponent() invoke Awake() immediately (screensInOrder etc.
            // aren't populated yet, so controllers NullReferenceException), and
            // EditorSceneManager.MarkSceneDirty/SaveScene throw outright ("cannot be used
            // during play mode"). Refuse instead of half-building and corrupting the open scene.
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("StartFlowSceneBuilder: exit Play Mode before rebuilding StartFlowScene - rebuilding while playing corrupts the open scene.");
                return;
            }

            Build();
            File.WriteAllText(LayoutVersionMarkerPath, CurrentLayoutVersion);
        }

        private class BuildContext
        {
            public Transform Canvas;
            public TMP_FontAsset RegularFont;
            public TMP_FontAsset BoldFont;
            public Sprite BackButtonSprite;
            public Sprite BlueButtonSprite;
            public Sprite PlainButtonSprite;
            public Sprite RiyalSprite;
        }

        private static void Build()
        {
            CairoFontAssetSetup.EnsureFontsGenerated();

            TMP_FontAsset regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CairoFontAssetSetup.RegularAssetPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CairoFontAssetSetup.BoldAssetPath);

            if (regularFont == null || boldFont == null)
                Debug.LogWarning("StartFlowSceneBuilder: Cairo TMP font assets are not ready yet - building the scene now with TMP's default font.");

            Sprite welcomeBg = LoadOrFixSprite($"{AssetRoot}/Backgrounds/WelcomeScreen.png");
            Sprite nameBg = LoadOrFixSprite($"{AssetRoot}/Backgrounds/NamePhoto.png");
            Sprite goalBg = LoadOrFixSprite($"{AssetRoot}/Backgrounds/GoalScreen.png");
            Sprite moneyBg = LoadOrFixSprite($"{AssetRoot}/Backgrounds/MonthlyMoney.png");

            Sprite backButtonSprite = LoadOrFixSprite($"{AssetRoot}/Buttons/BackButton.png");
            Sprite blueButtonSprite = LoadOrFixSprite($"{AssetRoot}/Buttons/BlueButton-plain.png");
            Sprite plainButtonSprite = LoadOrFixSprite($"{AssetRoot}/Buttons/Button-plain.png");

            Sprite nameInputSprite = LoadOrFixSprite($"{AssetRoot}/Inputs/NameInputbar.png");
            Sprite goalInputSprite = LoadOrFixSprite($"{AssetRoot}/Inputs/GoalPlain.png");
            Sprite moneyInputSprite = LoadOrFixSprite($"{AssetRoot}/Inputs/MonthlyMoneyPlain.png");

            Sprite riyalSprite = LoadOrFixSprite($"{AssetRoot}/Logo/ريال.png");

            EnsureFolder("Assets/Scenes");

            // If StartFlowScene.unity is already open (the common case while iterating on it),
            // creating a *second* Scene object and calling SaveScene(newScene, ScenePath)
            // silently fails to land on disk - Unity already has that exact path claimed by
            // the currently-loaded scene. Rebuild the already-open scene in place instead.
            Scene existing = EditorSceneManager.GetSceneByPath(ScenePath);
            bool reuseOpenScene = existing.IsValid() && existing.isLoaded;

            Scene scene;
            if (reuseOpenScene)
            {
                scene = existing;
                EditorSceneManager.SetActiveScene(scene);
                foreach (GameObject root in scene.GetRootGameObjects())
                    Object.DestroyImmediate(root);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SetActiveScene(scene);
            }

            GameObject cameraGO = BuildCamera();
            GameObject canvasGO = BuildCanvas();
            GameObject eventSystemGO = BuildEventSystem();
            SceneManager.MoveGameObjectToScene(cameraGO, scene);
            SceneManager.MoveGameObjectToScene(canvasGO, scene);
            SceneManager.MoveGameObjectToScene(eventSystemGO, scene);

            GameObject flowGO = new GameObject("StartFlowController");
            SceneManager.MoveGameObjectToScene(flowGO, scene);
            StartFlowController flowController = flowGO.AddComponent<StartFlowController>();

            var context = new BuildContext
            {
                Canvas = canvasGO.transform,
                RegularFont = regularFont,
                BoldFont = boldFont,
                BackButtonSprite = backButtonSprite,
                BlueButtonSprite = blueButtonSprite,
                PlainButtonSprite = plainButtonSprite,
                RiyalSprite = riyalSprite
            };

            CanvasGroup welcome = BuildWelcomeScreen(context, flowController, welcomeBg);
            CanvasGroup name = BuildNameScreen(context, flowController, nameBg, nameInputSprite);
            CanvasGroup goal = BuildGoalScreen(context, flowController, goalBg, goalInputSprite);
            CanvasGroup money = BuildMonthlyMoneyScreen(context, flowController, moneyBg, moneyInputSprite);

            BindArray(flowController, "screensInOrder", new Object[] { welcome, name, goal, money });
            BindString(flowController, "gameSceneName", "OGscene");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!reuseOpenScene)
                EditorSceneManager.CloseScene(scene, true);

            AddSceneToBuildSettings();

            Debug.Log("StartFlowSceneBuilder: StartFlowScene.unity rebuilt at " + ScenePath);
        }

        // ------------------------------------------------------------------ infrastructure

        private static GameObject BuildCamera()
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0B, 0x1B, 0x2E, 0xFF); // matches the night-sky background's own dark navy
            cam.cullingMask = 0; // nothing to render in this UI-only scene
            cam.orthographic = false;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>();
            go.transform.position = new Vector3(0f, 1f, -10f);
            return go;
        }

        private static GameObject BuildCanvas()
        {
            GameObject go = new GameObject("StartFlowCanvas", typeof(RectTransform));
            go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static GameObject BuildEventSystem()
        {
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            return go;
        }

        // UI_final assets import with Texture Type = Default by default (this project isn't
        // primarily a 2D/UI project), so AssetDatabase.LoadAssetAtPath<Sprite> silently
        // returns null even though the PNG exists. Fix the import settings in place.
        private static Sprite LoadOrFixSprite(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"StartFlowSceneBuilder: asset missing at '{path}'.");
                return null;
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(string.IsNullOrEmpty(parent) ? "Assets" : parent, leaf);
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void BindPrivate(Object target, string fieldName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"StartFlowSceneBuilder: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindArray(Object target, string fieldName, Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"StartFlowSceneBuilder: array field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindString(Object target, string fieldName, string value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------- shared pieces

        // All fractions below were measured directly off Assets/UI_final/References/*-refrence.png
        // (1196x682) - each crop was upscaled 2.2-6x with a 10-20px pixel grid overlaid and
        // read directly (not color-thresholded, which proved unreliable against this art's low
        // contrast) - then converted to normalized (0..1) coordinates, which hold regardless of
        // how the 1920x1080 canvas is scaled. Card/button horizontal span (0.3110-0.6906) is
        // shared across Name/Goal/MonthlyMoney - they line up on the same column in the
        // reference art.
        private static readonly Rect CommonBackButtonRect = new Rect(0.3110f, 0.7390f, 0.0343f, 0.0587f);

        private static (GameObject root, CanvasGroup group) BuildScreenRoot(Transform canvas, string name, Sprite background)
        {
            RectTransform root = StartFlowUIFactory.CreateUIObject(name, canvas);
            StartFlowUIFactory.StretchFull(root);
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            StartFlowUIFactory.CreateFullBleedImage(root, "Background", background);
            return (root.gameObject, group);
        }

        private static void ApplyRect(RectTransform rt, Rect rect)
        {
            StartFlowUIFactory.AnchorFraction(rt, rect.xMin, rect.yMin, rect.xMin + rect.width, rect.yMin + rect.height);
        }

        private static void BuildBackButton(RectTransform screenRoot, BuildContext ctx, StartFlowController flow)
        {
            Button backButton = StartFlowUIFactory.CreateIconButton(screenRoot, "BackButton", ctx.BackButtonSprite);
            ApplyRect(backButton.GetComponent<RectTransform>(), CommonBackButtonRect);

            BackButtonController controller = backButton.gameObject.AddComponent<BackButtonController>();
            BindPrivate(controller, "flowController", flow);
            BindPrivate(controller, "backButton", backButton);
        }

        // ----------------------------------------------------------------------- screen 01 (Welcome)

        private static readonly Rect WelcomeButtonRect = new Rect(0.3136f, 0.0689f, 0.3762f, 0.1071f);

        private static CanvasGroup BuildWelcomeScreen(BuildContext ctx, StartFlowController flow, Sprite background)
        {
            var (rootGO, group) = BuildScreenRoot(ctx.Canvas, "01_Welcome", background);
            RectTransform root = rootGO.GetComponent<RectTransform>();

            Button startButton = StartFlowUIFactory.CreateButton(root, "StartButton", ctx.BlueButtonSprite,
                "ابدأ المغامرة", 28f, Color.white, ctx.BoldFont);
            ApplyRect(startButton.GetComponent<RectTransform>(), WelcomeButtonRect);

            WelcomeScreenController controller = rootGO.AddComponent<WelcomeScreenController>();
            BindPrivate(controller, "flowController", flow);
            BindPrivate(controller, "startButton", startButton);

            return group;
        }

        // ----------------------------------------------------------------------- screen 02 (Name)

        private static readonly Rect NameCardRect = new Rect(0.3110f, 0.3255f, 0.3796f, 0.2141f);
        private static readonly Rect NameButtonRect = new Rect(0.3110f, 0.1906f, 0.3796f, 0.0850f);
        private static readonly Rect NameInputWithinCardRect = new Rect(0.0617f, 0.1644f, 0.8767f, 0.4246f);

        private static CanvasGroup BuildNameScreen(BuildContext ctx, StartFlowController flow, Sprite background, Sprite inputPanelSprite)
        {
            var (rootGO, group) = BuildScreenRoot(ctx.Canvas, "02_Name", background);
            RectTransform root = rootGO.GetComponent<RectTransform>();

            BuildBackButton(root, ctx, flow);

            RectTransform card = StartFlowUIFactory.CreateImage(root, "NameCard", inputPanelSprite).GetComponent<RectTransform>();
            ApplyRect(card, NameCardRect);

            TMP_InputField nameField = StartFlowUIFactory.CreateInputField(card, "NameField", null,
                "اكتب اسمك هنا", ctx.RegularFont, StartFlowUIFactory.Palette.InputText,
                TMP_InputField.ContentType.Standard, 30);
            ApplyRect(nameField.GetComponent<RectTransform>(), NameInputWithinCardRect);

            Button continueButton = StartFlowUIFactory.CreateButton(root, "PrimaryButton", ctx.PlainButtonSprite,
                "يلا نبدأ", 24f, StartFlowUIFactory.Palette.TextPrimary, ctx.BoldFont);
            ApplyRect(continueButton.GetComponent<RectTransform>(), NameButtonRect);

            NameScreenController controller = rootGO.AddComponent<NameScreenController>();
            BindPrivate(controller, "flowController", flow);
            BindPrivate(controller, "nameInputField", nameField);
            BindPrivate(controller, "continueButton", continueButton);

            return group;
        }

        // ----------------------------------------------------------------------- screen 03 (Goal)

        private static readonly Rect GoalCardRect = new Rect(0.3110f, 0.2229f, 0.3796f, 0.3695f);
        private static readonly Rect GoalButtonRect = new Rect(0.3110f, 0.0982f, 0.3796f, 0.0850f);
        private static readonly Rect GoalNameFieldWithinCardRect = new Rect(0.0595f, 0.5476f, 0.8722f, 0.2262f);
        private static readonly Rect GoalAmountFieldWithinCardRect = new Rect(0.0595f, 0.1151f, 0.8722f, 0.2420f);
        private static readonly Rect GoalRiyalIconWithinCardRect = new Rect(0.1057f, 0.1706f, 0.0837f, 0.0794f);

        private static CanvasGroup BuildGoalScreen(BuildContext ctx, StartFlowController flow, Sprite background, Sprite inputPanelSprite)
        {
            var (rootGO, group) = BuildScreenRoot(ctx.Canvas, "03_Goal", background);
            RectTransform root = rootGO.GetComponent<RectTransform>();

            BuildBackButton(root, ctx, flow);

            RectTransform card = StartFlowUIFactory.CreateImage(root, "GoalCard", inputPanelSprite).GetComponent<RectTransform>();
            ApplyRect(card, GoalCardRect);

            TMP_InputField goalNameField = StartFlowUIFactory.CreateInputField(card, "GoalNameField", null,
                "مثال: أبي أشتري لابتوب", ctx.RegularFont, StartFlowUIFactory.Palette.InputText,
                TMP_InputField.ContentType.Standard, 40);
            ApplyRect(goalNameField.GetComponent<RectTransform>(), GoalNameFieldWithinCardRect);

            TMP_InputField goalAmountField = StartFlowUIFactory.CreateInputField(card, "GoalAmountField", null,
                "0", ctx.RegularFont, StartFlowUIFactory.Palette.InputText, TMP_InputField.ContentType.IntegerNumber, 9);
            ApplyRect(goalAmountField.GetComponent<RectTransform>(), GoalAmountFieldWithinCardRect);

            if (ctx.RiyalSprite != null)
            {
                Image riyal = StartFlowUIFactory.CreateIcon(card, "RiyalIcon", ctx.RiyalSprite);
                ApplyRect(riyal.GetComponent<RectTransform>(), GoalRiyalIconWithinCardRect);
            }

            Button continueButton = StartFlowUIFactory.CreateButton(root, "PrimaryButton", ctx.PlainButtonSprite,
                "هذا هدفي", 24f, StartFlowUIFactory.Palette.TextPrimary, ctx.BoldFont);
            ApplyRect(continueButton.GetComponent<RectTransform>(), GoalButtonRect);

            GoalScreenController controller = rootGO.AddComponent<GoalScreenController>();
            BindPrivate(controller, "flowController", flow);
            BindPrivate(controller, "goalNameField", goalNameField);
            BindPrivate(controller, "goalAmountField", goalAmountField);
            BindPrivate(controller, "continueButton", continueButton);

            return group;
        }

        // ----------------------------------------------------------------------- screen 04 (MonthlyMoney)

        private static readonly Rect MoneyCardRect = new Rect(0.3110f, 0.2962f, 0.3796f, 0.2287f);
        private static readonly Rect MoneyValidationRect = new Rect(0.3110f, 0.2625f, 0.3796f, 0.0293f);
        private static readonly Rect MoneyStaticSentenceRect = new Rect(0.3110f, 0.2155f, 0.3796f, 0.0440f);
        private static readonly Rect MoneyButtonRect = new Rect(0.3110f, 0.1158f, 0.3796f, 0.0850f);
        private static readonly Rect MoneyInputWithinCardRect = new Rect(0.0617f, 0.2949f, 0.8810f, 0.3846f);
        private static readonly Rect MoneyRiyalIconWithinCardRect = new Rect(0.1013f, 0.3974f, 0.0837f, 0.1282f);

        private static CanvasGroup BuildMonthlyMoneyScreen(BuildContext ctx, StartFlowController flow, Sprite background, Sprite inputPanelSprite)
        {
            var (rootGO, group) = BuildScreenRoot(ctx.Canvas, "04_MonthlyMoney", background);
            RectTransform root = rootGO.GetComponent<RectTransform>();

            BuildBackButton(root, ctx, flow);

            RectTransform card = StartFlowUIFactory.CreateImage(root, "MoneyCard", inputPanelSprite).GetComponent<RectTransform>();
            ApplyRect(card, MoneyCardRect);

            TMP_InputField amountField = StartFlowUIFactory.CreateInputField(card, "AmountField", null,
                "0", ctx.RegularFont, StartFlowUIFactory.Palette.InputText, TMP_InputField.ContentType.IntegerNumber, 5);
            ApplyRect(amountField.GetComponent<RectTransform>(), MoneyInputWithinCardRect);

            if (ctx.RiyalSprite != null)
            {
                Image riyal = StartFlowUIFactory.CreateIcon(card, "RiyalIcon", ctx.RiyalSprite);
                ApplyRect(riyal.GetComponent<RectTransform>(), MoneyRiyalIconWithinCardRect);
            }

            TMP_Text validationMessage = StartFlowUIFactory.CreateWrappingText(root, "ValidationMessage", string.Empty, 18f,
                StartFlowUIFactory.Palette.Warning, TextAlignmentOptions.Center, true, ctx.RegularFont, FontStyles.Bold);
            ApplyRect(validationMessage.GetComponent<RectTransform>(), MoneyValidationRect);

            TMP_Text staticSentence = StartFlowUIFactory.CreateWrappingText(root, "MotivationSentence",
                "قراراتك هي اللي بتحدد إذا بتقرب من هدفك... أو تبعد عنه", 20f,
                StartFlowUIFactory.Palette.TextMuted, TextAlignmentOptions.Center, true, ctx.RegularFont);
            ApplyRect(staticSentence.GetComponent<RectTransform>(), MoneyStaticSentenceRect);

            Button continueButton = StartFlowUIFactory.CreateButton(root, "PrimaryButton", ctx.PlainButtonSprite,
                "ادخل المدينة", 24f, StartFlowUIFactory.Palette.TextPrimary, ctx.BoldFont);
            ApplyRect(continueButton.GetComponent<RectTransform>(), MoneyButtonRect);

            MonthlyMoneyScreenController controller = rootGO.AddComponent<MonthlyMoneyScreenController>();
            BindPrivate(controller, "flowController", flow);
            BindPrivate(controller, "amountField", amountField);
            BindPrivate(controller, "continueButton", continueButton);
            BindPrivate(controller, "validationMessage", validationMessage);

            return group;
        }
    }
}
