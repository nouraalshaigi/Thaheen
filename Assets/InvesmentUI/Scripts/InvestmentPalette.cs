using UnityEngine;

namespace InvestmentTowerUI
{
    // Shared colors for the Investment Tower UI, sampled from Assets/InvesmentUI/Refrences.
    public static class InvestmentPalette
    {
        public static readonly Color Positive = new Color32(0x3D, 0xB8, 0x6A, 0xFF);
        public static readonly Color Negative = new Color32(0xE0, 0x5A, 0x5A, 0xFF);
        public static readonly Color Gold = new Color32(0xE8, 0xC0, 0x5A, 0xFF);
        public static readonly Color TextPrimary = new Color32(0xF2, 0xF5, 0xF7, 0xFF);
        public static readonly Color TextMuted = new Color32(0xA9, 0xB6, 0xC4, 0xFF);
        public static readonly Color CardNormal = new Color32(0xFF, 0xFF, 0xFF, 0x0F);
        public static readonly Color CardSelected = new Color32(0x3D, 0xB8, 0x6A, 0x33);
    }
}
