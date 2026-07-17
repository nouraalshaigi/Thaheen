using System.Collections.Generic;
using InvestmentTowerUI.MarketData;
using StartFlow.Arabic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InvestmentTowerUI.Editor
{
    // Narrow, non-destructive maintenance operations on an ALREADY-BUILT
    // AI_Alinma_Investment_Tower_UI - never calls Build()/RebuildManually() (which recreates
    // every panel and prefab from scratch). Each entry point here only touches exactly the
    // GameObjects named in its own summary, and only via reparenting/renaming/replacing content
    // that's explicitly in scope - never a wholesale rebuild.
    internal static partial class InvestmentTowerUIBuilder
    {
        // Rebuilds ONLY CompanyListPanel's own children in place, using the current
        // BuildCompanyListContents (Part 1's structure: Header / TopInformationRow /
        // CompanyListContent / OpenPortfolioButton / FooterNote under a "Popup" child). Re-wires
        // only the CompanyList-related fields on the EXISTING InvestmentTowerUIController -
        // every other panel/field is left completely untouched.
        [MenuItem("Tools/Investment Tower/Rebuild CompanyListPanel Only")]
        public static void RebuildCompanyListPanelOnly()
        {
            if (!TryGetOpenScene(out Scene scene)) return;
            if (!TryFindRoot(scene, out GameObject uiRoot)) return;

            InvestmentTowerUIController controller = uiRoot.GetComponent<InvestmentTowerUIController>();
            if (controller == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: AI_Alinma_Investment_Tower_UI has no InvestmentTowerUIController.");
                return;
            }

            Transform overlay = FindOverlay(uiRoot);
            if (overlay == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: overlay ('DimOverlay'/'Overlay') not found under AI_Alinma_Investment_Tower_UI.");
                return;
            }

            TMP_FontAsset regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (regularFont == null || boldFont == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: Cairo TMP font assets not found - run Tools/Investment Tower/Regenerate Cairo Font Assets first.");
                return;
            }
            var art = new Art(regularFont, boldFont);
            if (!art.LoadAll()) return;

            // CompanyCard's own layout/art changed (per-company accent art, corrected L-to-R
            // order, merged sector/price row) - force just this one prefab asset to regenerate.
            // Every other cached prefab (buttons, headers, PortfolioHoldingCard, TransactionRow,
            // SummaryRow, QuantitySelector...) is left completely untouched, since EnsurePrefabs
            // below still runs with force:false for all of them.
            string companyCardPrefabPath = $"{PrefabFolder}/CompanyCard.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(companyCardPrefabPath) != null)
                AssetDatabase.DeleteAsset(companyCardPrefabPath);

            if (!EnsurePrefabs(art, force: false, out Prefabs prefabs))
                return;

            Transform existing = overlay.Find("CompanyListPanel");
            int siblingIndex = existing != null ? existing.GetSiblingIndex() : -1;
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var r = new ControllerRefs();
            BuildCompanyList(overlay, art, prefabs, r);
            if (siblingIndex >= 0) r.companyListPanel.transform.SetSiblingIndex(siblingIndex);
            r.companyListPanel.SetActive(false);

            SerializedObject so = new SerializedObject(controller);
            Set(so, "companyListPanel", r.companyListPanel);
            Set(so, "companyListHeader", r.companyListHeader);
            Set(so, "availableBalanceText", r.availableBalanceText);
            Set(so, "companyListContent", r.companyListContent);
            SetCardArray(so, "companyListCards", r.companyListCards);
            Set(so, "openPortfolioButton", r.openPortfolioButton);
            Set(so, "openPortfolioButtonRoot", r.openPortfolioButtonRoot);
            Set(so, "footerNoteText", r.footerNoteText);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Isolated market-data component (Assets/InvesmentUI/Scripts/MarketData) - added
            // once, non-destructively, to the same GameObject as InvestmentTowerUIController if
            // not already present. Nothing else about uiRoot is touched.
            if (uiRoot.GetComponent<SaudiMarketService>() == null)
            {
                uiRoot.AddComponent<SaudiMarketService>();
                Debug.Log("InvestmentTowerUIBuilder: added SaudiMarketService to AI_Alinma_Investment_Tower_UI.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("InvestmentTowerUIBuilder: CompanyListPanel rebuilt in place (no other panel touched).");
        }

        // One-off, idempotent fix for a real layout-shift bug: QuickAmountRow/SummaryRow used to
        // be SetActive(false)/(true) inside CompanyDetailsPanel's Content VerticalLayoutGroup,
        // which changed the panel's total required height every time quantity crossed the 0
        // boundary, shifting/overlapping the existing elements. InvestmentTowerUIController now
        // keeps both rows always active (fixed reserved LayoutElement space, flexibleHeight 0)
        // and toggles a CanvasGroup on each instead - this menu item brings the ALREADY-SAVED
        // scene in line with that. Touches only QuickAmountRow and SummaryRow - adds a
        // CanvasGroup to each if missing, sets flexibleHeight 0 on their existing LayoutElement
        // (never touching an existing preferredHeight - only adds one, using the current
        // intended design's value, if a LayoutElement is genuinely missing), force-activates
        // both if either was left inactive, and rewires the 2 new controller fields. No
        // RectTransform, anchor, position, size, or any other panel is touched.
        [MenuItem("Tools/Investment Tower/Fix Company Details Quantity Layout Shift")]
        public static void FixCompanyDetailsQuantityLayoutShift()
        {
            if (!TryGetOpenScene(out Scene scene)) return;
            if (!TryFindRoot(scene, out GameObject uiRoot)) return;

            InvestmentTowerUIController controller = uiRoot.GetComponent<InvestmentTowerUIController>();
            if (controller == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: AI_Alinma_Investment_Tower_UI has no InvestmentTowerUIController.");
                return;
            }

            SerializedObject so = new SerializedObject(controller);

            SerializedProperty summaryRowProp = so.FindProperty("detailsSummaryRow");
            GameObject summaryRow = summaryRowProp != null ? summaryRowProp.objectReferenceValue as GameObject : null;

            GameObject quickRow = null;
            SerializedProperty quickButtonsProp = so.FindProperty("detailsQuickButtons");
            if (quickButtonsProp != null && quickButtonsProp.arraySize > 0)
            {
                SerializedProperty firstButtonProp = quickButtonsProp.GetArrayElementAtIndex(0).FindPropertyRelative("button");
                Button firstButton = firstButtonProp != null ? firstButtonProp.objectReferenceValue as Button : null;
                if (firstButton != null && firstButton.transform.parent != null)
                    quickRow = firstButton.transform.parent.gameObject;
            }

            if (summaryRow == null || quickRow == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: could not locate SummaryRow/QuickAmountRow via InvestmentTowerUIController's existing fields - nothing to fix.");
                return;
            }

            ConfigureAlwaysVisibleRow(quickRow, 40f);
            ConfigureAlwaysVisibleRow(summaryRow, 36f);

            CanvasGroup quickGroup = quickRow.GetComponent<CanvasGroup>();
            if (quickGroup == null) quickGroup = quickRow.AddComponent<CanvasGroup>();
            SetHiddenGroup(quickGroup);

            CanvasGroup summaryGroup = summaryRow.GetComponent<CanvasGroup>();
            if (summaryGroup == null) summaryGroup = summaryRow.AddComponent<CanvasGroup>();
            SetHiddenGroup(summaryGroup);

            Set(so, "detailsQuickAmountGroup", quickGroup);
            Set(so, "detailsSummaryGroup", summaryGroup);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("InvestmentTowerUIBuilder: QuickAmountRow/SummaryRow now stay always-active with reserved layout space, toggled via CanvasGroup only.");
        }

        // Never overwrites an existing preferredHeight (that would be a layout change) - only
        // ensures the row is active and its flexibleHeight is 0, adding a LayoutElement (with
        // the given fallback preferredHeight, matching the current intended design) only if one
        // is genuinely missing.
        private static void ConfigureAlwaysVisibleRow(GameObject row, float fallbackPreferredHeight)
        {
            if (!row.activeSelf) row.SetActive(true);

            LayoutElement le = row.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = row.AddComponent<LayoutElement>();
                le.preferredHeight = fallbackPreferredHeight;
            }
            le.flexibleHeight = 0f;
        }

        // One-off, idempotent fix for a real routing bug: AI_Alinma_Investment_Tower_UI's root
        // GameObject was being saved to the scene as inactive (m_IsActive: 0). Unity never calls
        // Awake() on a component whose GameObject starts inactive, so
        // InvestmentTowerUIController.Awake() never ran on scene/Play load, Instance stayed null
        // forever, and BuildingPopupController.Show() silently fell back to the generic beige
        // popup for BuildingId.InvestmentTower instead of opening this UI - the exact cause of
        // "clicking Investment Tower opens the old popup". InvestmentTowerUIController now keeps
        // its own GameObject always active and toggles DimOverlay instead (see its Awake/Open/
        // Close) - this menu item brings the ALREADY-SAVED scene in line with that: root active,
        // DimOverlay inactive (closed, matching the resting state Awake() enforces at runtime
        // anyway). Touches only those 2 GameObjects' active flags - nothing else.
        [MenuItem("Tools/Investment Tower/Fix Root Activation Bug")]
        public static void FixRootActivationBug()
        {
            if (!TryGetOpenScene(out Scene scene)) return;
            if (!TryFindRoot(scene, out GameObject uiRoot)) return;

            bool changed = false;

            if (!uiRoot.activeSelf)
            {
                uiRoot.SetActive(true);
                changed = true;
                Debug.Log("InvestmentTowerUIBuilder: AI_Alinma_Investment_Tower_UI was inactive - activated it.");
            }

            Transform overlay = FindOverlay(uiRoot);
            if (overlay == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: overlay ('DimOverlay'/'Overlay') not found under AI_Alinma_Investment_Tower_UI.");
            }
            else if (overlay.gameObject.activeSelf)
            {
                overlay.gameObject.SetActive(false);
                changed = true;
                Debug.Log("InvestmentTowerUIBuilder: DimOverlay was active - deactivated it (closed resting state).");
            }

            if (!changed)
            {
                Debug.Log("InvestmentTowerUIBuilder: root/overlay activation already correct - nothing to fix.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("InvestmentTowerUIBuilder: root activation fixed and scene saved.");
        }

        // Rebuilds ONLY PortfolioPanel's own children in place, using the current
        // BuildPortfolioContents (Header / TabsRow / TabArea(PortfolioContent(EmptyState,
        // HoldingsState) / HistoryTabContent / SummaryTabContent) / BottomActions under a "Popup"
        // child). Re-wires only the Portfolio-related fields on the EXISTING
        // InvestmentTowerUIController - every other panel/field is left completely untouched.
        [MenuItem("Tools/Investment Tower/Rebuild PortfolioPanel Only")]
        public static void RebuildPortfolioPanelOnly()
        {
            if (!TryGetOpenScene(out Scene scene)) return;
            if (!TryFindRoot(scene, out GameObject uiRoot)) return;

            InvestmentTowerUIController controller = uiRoot.GetComponent<InvestmentTowerUIController>();
            if (controller == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: AI_Alinma_Investment_Tower_UI has no InvestmentTowerUIController.");
                return;
            }

            Transform overlay = FindOverlay(uiRoot);
            if (overlay == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: overlay ('DimOverlay'/'Overlay') not found under AI_Alinma_Investment_Tower_UI.");
                return;
            }

            TMP_FontAsset regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
            TMP_FontAsset boldFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
            if (regularFont == null || boldFont == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: Cairo TMP font assets not found - run Tools/Investment Tower/Regenerate Cairo Font Assets first.");
                return;
            }
            var art = new Art(regularFont, boldFont);
            if (!art.LoadAll()) return;

            // PortfolioHoldingCard/TransactionRow/SummaryRow layouts changed (PortfolioHoldingCard:
            // renamed/restructured from PortfolioCompanyCard; TransactionRow: rebuilt around
            // TypeBadge/CompanyInfo/TransactionValues with a reserved LayoutElement height;
            // SummaryRow: gained a reserved LayoutElement height to fix rows collapsing to zero
            // height and overlapping) - force just these 3 cached prefab assets to regenerate.
            // Every other cached prefab (buttons, headers, CompanyCard, QuantitySelector...) is
            // left completely untouched, since EnsurePrefabs below still runs with force:false for
            // all of them.
            foreach (string prefabName in new[] { "PortfolioHoldingCard", "TransactionRow", "SummaryRow" })
            {
                string prefabPath = $"{PrefabFolder}/{prefabName}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                    AssetDatabase.DeleteAsset(prefabPath);
            }

            if (!EnsurePrefabs(art, force: false, out Prefabs prefabs))
                return;

            Transform existing = overlay.Find("PortfolioPanel");
            int siblingIndex = existing != null ? existing.GetSiblingIndex() : -1;
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var r = new ControllerRefs();
            BuildPortfolio(overlay, art, prefabs, r);
            if (siblingIndex >= 0) r.portfolioPanel.transform.SetSiblingIndex(siblingIndex);
            r.portfolioPanel.SetActive(false);

            SerializedObject so = new SerializedObject(controller);

            // PortfolioPanel's own close button has no dedicated header field like
            // CompanyListPanel does - it only exists inside the shared "closeButtons" array. The
            // old PortfolioPanel (and its close button) was just destroyed above, so read the
            // existing array, drop that now-destroyed entry, and add the freshly-built one back -
            // every other panel's close button already in the array is preserved untouched.
            SerializedProperty closeButtonsProp = so.FindProperty("closeButtons");
            var preservedCloseButtons = new List<Button>();
            for (int i = 0; i < closeButtonsProp.arraySize; i++)
            {
                Button existingCloseButton = closeButtonsProp.GetArrayElementAtIndex(i).objectReferenceValue as Button;
                if (existingCloseButton != null) preservedCloseButtons.Add(existingCloseButton);
            }
            preservedCloseButtons.AddRange(r.closeButtons);
            SetButtonArray(so, "closeButtons", preservedCloseButtons);

            Set(so, "portfolioPanel", r.portfolioPanel);
            Set(so, "portfolioTabButton", r.portfolioTabButton);
            Set(so, "historyTabButton", r.historyTabButton);
            Set(so, "summaryTabButton", r.summaryTabButton);
            Set(so, "portfolioTabContent", r.portfolioTabContent);
            Set(so, "historyTabContent", r.historyTabContent);
            Set(so, "summaryTabContent", r.summaryTabContent);
            Set(so, "portfolioEmptyState", r.portfolioEmptyState);
            Set(so, "portfolioEmptyStateButton", r.portfolioEmptyStateButton);
            Set(so, "portfolioHoldingsState", r.portfolioHoldingsState);
            Set(so, "portfolioValueText", r.portfolioValueText);
            Set(so, "portfolioRemainingBalanceText", r.portfolioRemainingBalanceText);
            Set(so, "portfolioProfitLossText", r.portfolioProfitLossText);
            Set(so, "portfolioHoldingsContent", r.portfolioHoldingsContent);
            Set(so, "portfolioHoldingCardPrefab", r.portfolioHoldingCardPrefab);
            Set(so, "historyScrollView", r.historyScrollView);
            Set(so, "historyContent", r.historyContent);
            Set(so, "transactionRowPrefab", r.transactionRowPrefab);
            Set(so, "historyEmptyState", r.historyEmptyState);
            Set(so, "historyEmptyTitleText", r.historyEmptyTitleText);
            Set(so, "historyEmptySubtitleText", r.historyEmptySubtitleText);
            Set(so, "summaryScrollView", r.summaryScrollView);
            Set(so, "summaryTotalInvestedText", r.summaryTotalInvestedText);
            Set(so, "summaryCurrentValueText", r.summaryCurrentValueText);
            Set(so, "summaryRemainingBalanceText", r.summaryRemainingBalanceText);
            Set(so, "summaryTotalProfitLossText", r.summaryTotalProfitLossText);
            Set(so, "summaryBestInvestmentRow", r.summaryBestInvestmentRow);
            Set(so, "summaryWorstInvestmentRow", r.summaryWorstInvestmentRow);
            Set(so, "summaryTransactionsCountRow", r.summaryTransactionsCountRow);
            Set(so, "summaryEmptyState", r.summaryEmptyState);
            Set(so, "summaryEmptyText", r.summaryEmptyText);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("InvestmentTowerUIBuilder: PortfolioPanel rebuilt in place (no other panel touched).");
        }

        // One-off, narrowly-scoped fix: SellSharesPanel's sellOwnedText used to hold one
        // composed sentence mixing multiple Arabic phrases with embedded numbers ("تملك 5 سهم •
        // متوسط 28.40 ريال"), which TMP_Text.isRightToLeftText can't reliably mirror. Splits it,
        // in place, into two separate TMP fields inside the exact same row slot: a static Arabic
        // label ("الأسهم المملوكة") and a dynamic numeric value ("5 أسهم") - no other
        // RectTransform, font, color, or gameplay logic is touched. Idempotent - a no-op if
        // already split (e.g. after a fresh BuildSellShares, which now builds this same
        // structure directly).
        [MenuItem("Tools/Investment Tower/Fix Sell Owned Text Rendering")]
        public static void FixSellOwnedTextRendering()
        {
            if (!TryGetOpenScene(out Scene scene)) return;
            if (!TryFindRoot(scene, out GameObject uiRoot)) return;

            InvestmentTowerUIController controller = uiRoot.GetComponent<InvestmentTowerUIController>();
            if (controller == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: AI_Alinma_Investment_Tower_UI has no InvestmentTowerUIController.");
                return;
            }

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty prop = so.FindProperty("sellOwnedText");
            TMP_Text current = prop != null ? prop.objectReferenceValue as TMP_Text : null;
            if (current == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: 'sellOwnedText' is not assigned on InvestmentTowerUIController - nothing to fix.");
                return;
            }

            GameObject rowGO = current.gameObject;
            if (rowGO.name == "OwnedSharesRow")
            {
                Debug.Log("InvestmentTowerUIBuilder: sellOwnedText is already split into a label+value row - nothing to fix.");
                return;
            }

            TMP_FontAsset regularFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontPath);
            if (regularFont == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: Cairo Regular TMP font asset not found - run Tools/Investment Tower/Regenerate Cairo Font Assets first.");
                return;
            }

            RectTransform ownedRow = (RectTransform)rowGO.transform;
            ownedRow.gameObject.name = "OwnedSharesRow";
            Object.DestroyImmediate(current);
            AddHorizontalLayout(ownedRow.gameObject, 4f, alignment: TextAnchor.MiddleRight);

            TMP_Text ownedValue = CreateText(ownedRow, "OwnedSharesValueText", "0 أسهم", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineLeft, false, regularFont);
            AddLayoutElement(ownedValue.gameObject, flexibleWidth: 1);
            CreateText(ownedRow, "OwnedSharesLabelText", "الأسهم المملوكة", 13f, InvestmentPalette.TextMuted, TextAlignmentOptions.MidlineRight, true, regularFont);

            SerializedObject soAfter = new SerializedObject(controller);
            Set(soAfter, "sellOwnedText", ownedValue);
            soAfter.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("InvestmentTowerUIBuilder: sellOwnedText split into a separate label ('الأسهم المملوكة') and dynamic value field.");
        }

        // Non-destructive hierarchy cleanup for every panel EXCEPT CompanyListPanel (already
        // clean - see above): inserts the "Popup" wrapper (panel = empty anchor, Popup = the
        // actual card with Image + content, matching the requested tree) via reparenting, and
        // strips the "(Clone)" suffix Unity appends to every prefab instance name. Reparenting
        // and renaming never touch component references - every existing serialized wiring
        // (button listeners, controller fields) keeps working exactly as before.
        [MenuItem("Tools/Investment Tower/Reorganize Hierarchy (non-destructive)")]
        public static void ReorganizeHierarchy()
        {
            if (!TryGetOpenScene(out Scene scene)) return;
            if (!TryFindRoot(scene, out GameObject uiRoot)) return;

            Transform overlay = FindOverlay(uiRoot);
            if (overlay == null)
            {
                Debug.LogError("InvestmentTowerUIBuilder: overlay ('DimOverlay'/'Overlay') not found under AI_Alinma_Investment_Tower_UI.");
                return;
            }
            overlay.gameObject.name = OverlayObjectName;

            string[] panelsToWrap =
            {
                "TutorialStep1Panel", "TutorialStep2Panel", "CompanyDetailsPanel",
                "PurchaseConfirmationPanel", "PortfolioPanel", "SellSharesPanel",
            };

            foreach (string panelName in panelsToWrap)
            {
                Transform panel = overlay.Find(panelName);
                if (panel == null)
                {
                    Debug.LogWarning($"InvestmentTowerUIBuilder: '{panelName}' not found - skipping (nothing to reorganize).");
                    continue;
                }
                WrapInPopup(panel.gameObject);
            }

            int renamed = StripCloneSuffixRecursive(uiRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"InvestmentTowerUIBuilder: hierarchy reorganized - {panelsToWrap.Length} panels wrapped in 'Popup', {renamed} '(Clone)' names cleaned up.");
        }

        // If 'panel' already has an Image (i.e. it IS the visual card, from the old single-level
        // layout), move it under a new empty "Popup"-less anchor: creates an empty parent with
        // the panel's old name/position, reparents the original (now named "Popup") underneath.
        // If 'panel' already has a "Popup" child (already reorganized), this is a no-op.
        private static void WrapInPopup(GameObject panel)
        {
            if (panel.transform.Find("Popup") != null) return; // already done

            if (panel.GetComponent<Image>() == null) return; // not the old single-level layout - nothing to wrap

            Transform parent = panel.transform.parent;
            int siblingIndex = panel.transform.GetSiblingIndex();
            string originalName = panel.name;

            GameObject newOuter = new GameObject(originalName, typeof(RectTransform));
            newOuter.transform.SetParent(parent, false);
            newOuter.transform.SetSiblingIndex(siblingIndex);
            StretchFull((RectTransform)newOuter.transform);

            panel.transform.SetParent(newOuter.transform, false);
            panel.name = "Popup";
        }

        private static int StripCloneSuffixRecursive(Transform t)
        {
            int count = 0;
            const string suffix = "(Clone)";
            if (t.name.EndsWith(suffix))
            {
                t.name = t.name.Substring(0, t.name.Length - suffix.Length).TrimEnd();
                count++;
            }
            for (int i = 0; i < t.childCount; i++)
                count += StripCloneSuffixRecursive(t.GetChild(i));
            return count;
        }

        // ------------------------------------------------------------------------ shared scene helpers

        private static bool TryGetOpenScene(out Scene scene)
        {
            scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError($"InvestmentTowerUIBuilder: '{ScenePath}' is not open - open it in the Editor first.");
                return false;
            }
            return true;
        }

        // Tolerates both the current name ("DimOverlay") and the legacy one ("Overlay") that
        // scenes built before the hierarchy reorganization still have.
        private static Transform FindOverlay(GameObject uiRoot) =>
            uiRoot.transform.Find(OverlayObjectName) ?? uiRoot.transform.Find("Overlay");

        private static bool TryFindRoot(Scene scene, out GameObject uiRoot)
        {
            GameObject canvas = FindInScene(scene, CanvasObjectName);
            if (canvas == null)
            {
                Debug.LogError($"InvestmentTowerUIBuilder: '{CanvasObjectName}' not found in the scene.");
                uiRoot = null;
                return false;
            }
            Transform rootT = canvas.transform.Find(RootObjectName);
            if (rootT == null)
            {
                Debug.LogError($"InvestmentTowerUIBuilder: '{RootObjectName}' not found under '{CanvasObjectName}' - run the full build first.");
                uiRoot = null;
                return false;
            }
            uiRoot = rootT.gameObject;
            return true;
        }
    }
}
