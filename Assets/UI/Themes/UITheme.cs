using TMPro;
using UnityEngine;

namespace UI.Themes
{
    [CreateAssetMenu(menuName = "UI/Theme", fileName = "UITheme")]
    public class UITheme : ScriptableObject
    {
        [Header("Colors")]
        public Color primaryColor;
        public Color secondaryColor;
        public Color backgroundColor;
        public Color borderColor;
        public Color accentColor;
        public Color warningColor;

        [Header("Fonts")]
        public TMP_FontAsset headerFont;
        public TMP_FontAsset bodyFont;

        [Header("Sprites")]
        public Sprite panelBackground;
        public Sprite buttonBackground;
        public Sprite borderDecoration;

        [Header("Optional FX")]
        public Material glowEffect;
    }
}