using UnityEngine;

namespace BuildingInteractionSystem
{
    [CreateAssetMenu(menuName = "Building Interaction/Building Data", fileName = "BuildingData_")]
    public class BuildingData : ScriptableObject
    {
        [Header("Identity")]
        public BuildingId id;

        [Header("Arabic Content (RTL, gender-neutral)")]
        public string titleArabic;
        [TextArea(2, 5)] public string descriptionArabic;
        public string primaryButtonTextArabic = "دخول";

        [Header("Visuals")]
        [Tooltip("Optional. If empty, a colored circle with the fallback letter below is shown instead.")]
        public Sprite icon;
        public string iconFallbackLetter = "؟";

        [Header("AI Role (context for a future real AI provider)")]
        [TextArea(3, 8)] public string aiRoleDescription;

        [Header("AI Panel")]
        [Tooltip("Whether the optional AI panel is shown for this building. The popup's own master switch can still force it off.")]
        public bool aiPanelEnabledByDefault = false;
    }
}
