using Godot;
using System;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Help overlay showing keyboard shortcuts and controls.
    /// Toggle visibility with '?' or F2 key.
    /// </summary>
    public partial class HelpOverlay : Control
    {
        private PanelContainer _panel;
        private VBoxContainer _content;
        private Label _title;
        
        public override void _Ready()
        {
            SetupUI();
            Visible = false; // Hidden by default
        }
        
        public override void _UnhandledInput(InputEvent @event)
        {
            if (Visible && @event.IsPressed() && !@event.IsEcho())
            {
                // Any key dismisses the overlay when it's visible
                ToggleVisibility();
                GetViewport().SetInputAsHandled();
            }
            else if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            {
                // Check for '?' or F2 to show
                if (keyEvent.Keycode == Key.Question || keyEvent.Keycode == Key.Slash && keyEvent.ShiftPressed ||
                    keyEvent.Keycode == Key.F2)
                {
                    ToggleVisibility();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
        
        public void ToggleVisibility()
        {
            Visible = !Visible;
            GD.Print($"[HelpOverlay] Visibility: {Visible}");
        }
        
        private void SetupUI()
        {
            // Main panel - centered
            _panel = new PanelContainer();
            _panel.SetAnchorsPreset(LayoutPreset.Center);
            _panel.CustomMinimumSize = new Vector2(500, 400);
            _panel.AnchorLeft = 0.5f;
            _panel.AnchorRight = 0.5f;
            _panel.AnchorTop = 0.5f;
            _panel.AnchorBottom = 0.5f;
            _panel.OffsetLeft = -250;
            _panel.OffsetRight = 250;
            _panel.OffsetTop = -200;
            _panel.OffsetBottom = 200;
            
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            style.SetCornerRadiusAll(8);
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.3f, 0.5f, 0.8f);
            _panel.AddThemeStyleboxOverride("panel", style);
            AddChild(_panel);
            
            _content = new VBoxContainer();
            _content.AddThemeConstantOverride("separation", 8);
            _panel.AddChild(_content);
            
            // Title
            _title = new Label();
            _title.Text = "COMBAT CONTROLS HELP";
            _title.HorizontalAlignment = HorizontalAlignment.Center;
            _title.AddThemeFontSizeOverride("font_size", 20);
            _title.Modulate = new Color(0.3f, 0.6f, 1.0f);
            _content.AddChild(_title);
            
            _content.AddChild(new HSeparator());
            
            // Sections
            AddSection("GENERAL", new[]
            {
                "?  or  F2    - Show this help",
                "Space / Enter - End turn",
                "Escape       - Cancel selection"
            });
            
            AddSection("ABILITIES", new[]
            {
                "1-6          - Select ability slot 1-6",
                "Left Click   - Target/Execute ability",
                "Right Click  - Cancel ability selection"
            });
            
            AddSection("MOVEMENT", new[]
            {
                "M            - Enter movement mode",
                "Left Click   - Move to target position",
                "Right Click  - Cancel movement"
            });
            
            AddSection("CAMERA", new[]
            {
                "WASD / Arrows - Pan camera",
                "Q / E        - Rotate camera",
                "Mouse Wheel  - Zoom in/out"
            });
            
            AddSection("DEBUG", new[]
            {
                "F1           - Toggle debug panel"
            });
            
            _content.AddChild(new HSeparator());
            
            // Footer
            var footer = new Label();
            footer.Text = "Press any key to close";
            footer.HorizontalAlignment = HorizontalAlignment.Center;
            footer.AddThemeFontSizeOverride("font_size", 12);
            footer.Modulate = new Color(0.7f, 0.7f, 0.7f);
            _content.AddChild(footer);
        }
        
        private void AddSection(string sectionName, string[] controls)
        {
            var sectionLabel = new Label();
            sectionLabel.Text = sectionName;
            sectionLabel.AddThemeFontSizeOverride("font_size", 14);
            sectionLabel.Modulate = new Color(1.0f, 0.8f, 0.3f);
            _content.AddChild(sectionLabel);
            
            foreach (var control in controls)
            {
                var controlLabel = new Label();
                controlLabel.Text = "  " + control;
                controlLabel.AddThemeFontSizeOverride("font_size", 12);
                _content.AddChild(controlLabel);
            }
            
            _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) }); // Spacer
        }
    }
}
