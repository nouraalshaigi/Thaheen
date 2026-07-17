using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildingInteractionSystem.Editor
{
    // One-shot setup step that connects the Mall building to the popup system. Kept as a menu
    // item rather than running automatically since it touches a saved scene and should happen
    // deliberately, once, under the developer's control. (The prefab's own UI hierarchy is built
    // by BuildingPopupPrefabBuilder, not here - this script only touches OGscene.)
    internal static class BuildingsPopupSetup
    {
        private const string ScenePath = "Assets/OGscene.unity";
        private const string MallBuildingDataPath = "Assets/UI/BuildingInteraction/Data/BuildingData_ShoppingMall.asset";
        private const string MallObjectName = "Burj_Almammlkah_mall";

        [MenuItem("Tools/Buildings Popup/Wire Mall Building In OGscene")]
        public static void WireMallBuildingInScene()
        {
            BuildingData mallData = AssetDatabase.LoadAssetAtPath<BuildingData>(MallBuildingDataPath);
            if (mallData == null)
            {
                Debug.LogError($"BuildingsPopupSetup: '{MallBuildingDataPath}' not found.");
                return;
            }

            // Reuse the scene if it's already open (the active-editing instance) rather than
            // opening a second copy - EditorSceneManager silently no-ops a save to a path that's
            // already owned by a loaded scene, so operating on a fresh Scene handle here would
            // look like it worked while writing nothing.
            Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool openedHere = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                openedHere = true;
            }

            GameObject mallObject = FindInScene(scene, MallObjectName);
            if (mallObject == null)
            {
                Debug.LogError($"BuildingsPopupSetup: GameObject '{MallObjectName}' not found in '{ScenePath}'.");
                return;
            }

            BuildingInteraction existing = mallObject.GetComponent<BuildingInteraction>();
            if (existing != null)
            {
                Debug.Log($"BuildingsPopupSetup: '{MallObjectName}' already has a BuildingInteraction - leaving it as-is.");
                return;
            }

            BuildingInteraction interaction = Undo.AddComponent<BuildingInteraction>(mallObject);
            SerializedObject so = new SerializedObject(interaction);
            so.FindProperty("buildingData").objectReferenceValue = mallData;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"BuildingsPopupSetup: added BuildingInteraction (BuildingData_ShoppingMall) to '{MallObjectName}' " +
                $"and saved '{ScenePath}'.{(openedHere ? " (scene was opened by this utility)" : string.Empty)}");
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject found = FindInHierarchy(root.transform, name);
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
