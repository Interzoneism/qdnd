using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Controls
{
    /// <summary>
    /// Displays weapon stats for one weapon set (melee or ranged).
    /// Shows weapon icon slot, attack bonus, and damage range.
    /// </summary>
    public partial class WeaponStatDisplay : VBoxContainer
    {
        private readonly string _label;
        private readonly string _iconPath;
        private readonly int _attackBonus;
        private readonly string _damageRange;

        private Label _attackBonusLabel;
        private Label _damageRangeLabel;
        private TextureRect _iconTexture;

        /// <param name="label">Display label (e.g., "Melee" or "Ranged")</param>
        /// <param name="iconPath">Icon path for the weapon</param>
        /// <param name="attackBonus">Attack bonus value</param>
        /// <param name="damageRange">Damage range string (e.g., "0~9")</param>
        public WeaponStatDisplay(string label, string iconPath, int attackBonus, string damageRange)
        {
            _label = label;
            _iconPath = iconPath;
            _attackBonus = attackBonus;
            _damageRange = damageRange;
        }

        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 4);
            Alignment = AlignmentMode.Center;
            SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // Section label
            var headerLabel = new Label();
            headerLabel.Text = _label;
            headerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(headerLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            AddChild(headerLabel);

            // Weapon icon in a styled container
            var iconPanel = new PanelContainer();
            iconPanel.CustomMinimumSize = new Vector2(48, 48);
            iconPanel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            iconPanel.AddThemeStyleboxOverride("panel", HudTheme.CreatePanelStyle(
                HudTheme.TertiaryDark,
                HudTheme.PanelBorderSubtle,
                HudTheme.CornerRadiusSmall, 1, 2));
            AddChild(iconPanel);

            _iconTexture = new TextureRect();
            _iconTexture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _iconTexture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _iconTexture.CustomMinimumSize = new Vector2(44, 44);
            _iconTexture.MouseFilter = MouseFilterEnum.Ignore;
            iconPanel.AddChild(_iconTexture);

            // Load icon
            if (!string.IsNullOrWhiteSpace(_iconPath))
            {
                var tex = HudIcons.LoadTextureSafe(_iconPath);
                if (tex != null)
                {
                    _iconTexture.Texture = tex;
                    _iconTexture.Modulate = Colors.White;
                }
                else
                {
                    _iconTexture.Modulate = new Color(1, 1, 1, 0.3f);
                }
            }

            // Attack bonus
            _attackBonusLabel = new Label();
            string bonusSign = _attackBonus >= 0 ? "+" : "";
            _attackBonusLabel.Text = $"{bonusSign}{_attackBonus}";
            _attackBonusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_attackBonusLabel, HudTheme.FontMedium, HudTheme.WarmWhite);
            AddChild(_attackBonusLabel);

            // Damage range
            _damageRangeLabel = new Label();
            _damageRangeLabel.Text = _damageRange;
            _damageRangeLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_damageRangeLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            AddChild(_damageRangeLabel);
        }

        public void UpdateStats(string iconPath, int attackBonus, string damageRange)
        {
            if (_attackBonusLabel != null)
            {
                string bonusSign = attackBonus >= 0 ? "+" : "";
                _attackBonusLabel.Text = $"{bonusSign}{attackBonus}";
            }

            if (_damageRangeLabel != null)
                _damageRangeLabel.Text = damageRange;

            if (_iconTexture != null && !string.IsNullOrWhiteSpace(iconPath))
            {
                var tex = HudIcons.LoadTextureSafe(iconPath);
                if (tex != null)
                    _iconTexture.Texture = tex;
            }
        }
    }
}
