using UI.Themes;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Elements
{
    public class ThemedPanel : UIThemedElement
    {
        
        public enum PanelType { Primary, Background, Accent }
        public PanelType panelType;

        private Image _image;
        
        private void Awake()
        {
            _image = GetComponent<Image>();
        }
        
        public override void ApplyTheme(UITheme theme)
        {
            switch (panelType)
            {
                case PanelType.Primary:
                    _image.color = theme.primaryColor;
                    _image.sprite = theme.panelBackground;
                    break;
                case PanelType.Background:
                    _image.color = theme.backgroundColor;
                    break;
                case PanelType.Accent:
                    _image.color = theme.accentColor;
                    break;
            }
        }
    }
}
