using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Selected unit strip showing active unit summary.
    /// </summary>
    public partial class SelectedUnitStrip : HudPanel
    {
        private Label _nameLabel;
        private Label _raceClassLabel;
        private ProgressBar _hpBar;
        private Label _hpLabel;
        private PanelContainer _mainPanel;
        private float _currentHpPercent = 100f;

        public SelectedUnitStrip()
        {
            PanelTitle = "";
            ShowDragHandle = true;
            Draggable = true;
        }

        protected override void BuildContent(Control parent)
        {
            _mainPanel = new PanelContainer();
            _mainPanel.AddThemeStyleboxOverride("panel", 
                HudTheme.CreatePanelStyle(
                    bgColor: HudTheme.SecondaryDark,
                    borderColor: HudTheme.HealthGreen,
                    borderWidth: 2
                ));
            parent.AddChild(_mainPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 3);
            _mainPanel.AddChild(vbox);

            // Name
            _nameLabel = new Label();
            _nameLabel.Text = "";
            HudTheme.StyleHeader(_nameLabel, HudTheme.FontMedium);
            vbox.AddChild(_nameLabel);

            // Race/Class
            _raceClassLabel = new Label();
            _raceClassLabel.Text = "";
            HudTheme.StyleLabel(_raceClassLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            vbox.AddChild(_raceClassLabel);

            // HP bar
            _hpBar = new ProgressBar();
            _hpBar.CustomMinimumSize = new Vector2(0, 10);
            _hpBar.ShowPercentage = false;
            _hpBar.MaxValue = 100;
            _hpBar.Value = 100;
            _hpBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            _hpBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(HudTheme.HealthGreen));
            vbox.AddChild(_hpBar);

            // HP text
            _hpLabel = new Label();
            _hpLabel.Text = "";
            HudTheme.StyleLabel(_hpLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            vbox.AddChild(_hpLabel);
        }

        /// <summary>
        /// Set the combatant information.
        /// </summary>
        public void SetCombatant(string name, string raceClass, int hp, int maxHp)
        {
            _nameLabel.Text = name;
            _raceClassLabel.Text = raceClass;
            _hpLabel.Text = $"{hp} / {maxHp}";

            float hpPercent = maxHp > 0 ? (hp / (float)maxHp) * 100 : 0;
            _currentHpPercent = hpPercent;

            _hpBar.Value = hpPercent;
            _hpBar.AddThemeStyleboxOverride("fill", 
                HudTheme.CreateProgressBarFill(HudTheme.GetHealthColor(hpPercent)));

            // Update border color based on health
            var borderColor = HudTheme.GetHealthColor(hpPercent);
            _mainPanel.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    bgColor: HudTheme.SecondaryDark,
                    borderColor: borderColor,
                    borderWidth: 2
                ));

            _mainPanel.Visible = true;
        }

        /// <summary>
        /// Clear the display.
        /// </summary>
        public void Clear()
        {
            _nameLabel.Text = "";
            _raceClassLabel.Text = "";
            _hpLabel.Text = "";
            _hpBar.Value = 0;
            _mainPanel.Visible = false;
        }
    }
}
