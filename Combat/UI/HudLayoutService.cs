using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI
{
    /// <summary>
    /// Persists HUD panel layout (positions and sizes) to disk.
    /// Saves to user:// directory and automatically restores on next launch.
    /// </summary>
    public static class HudLayoutService
    {
        private const string ConfigPath = "user://hud_layout.cfg";

        /// <summary>
        /// Save current panel layout to disk.
        /// </summary>
        /// <param name="panels">Dictionary mapping panel identifiers to panel instances.</param>
        public static void SaveLayout(Dictionary<string, HudPanel> panels)
        {
            if (panels == null || panels.Count == 0)
            {
                GD.PrintErr("[HudLayoutService] No panels to save.");
                return;
            }

            var config = new ConfigFile();
            int savedCount = 0;

            foreach (var kvp in panels)
            {
                string panelId = kvp.Key;
                var panel = kvp.Value;

                if (panel == null || !GodotObject.IsInstanceValid(panel))
                {
                    GD.Print($"[HudLayoutService] Skipping invalid panel: {panelId}");
                    continue;
                }

                // Save position
                config.SetValue(panelId, "position_x", panel.GlobalPosition.X);
                config.SetValue(panelId, "position_y", panel.GlobalPosition.Y);

                // Save size
                config.SetValue(panelId, "size_x", panel.Size.X);
                config.SetValue(panelId, "size_y", panel.Size.Y);
                
                savedCount++;
            }

            var err = config.Save(ConfigPath);
            if (err != Error.Ok)
            {
                GD.PrintErr($"[HudLayoutService] Failed to save layout: {err}");
            }
            else
            {
                // Print absolute path so user can verify file location
                var absolutePath = ProjectSettings.GlobalizePath(ConfigPath);
                GD.Print($"[HudLayoutService] Saved {savedCount} panel layouts to: {absolutePath}");
            }
        }

        /// <summary>
        /// Load saved panel layout from disk and apply to panels.
        /// Returns a set of panel IDs that had saved data loaded.
        /// </summary>
        /// <param name="panels">Dictionary mapping panel identifiers to panel instances.</param>
        public static HashSet<string> LoadLayout(Dictionary<string, HudPanel> panels)
        {
            var loadedPanelIds = new HashSet<string>();

            if (panels == null || panels.Count == 0)
            {
                GD.PrintErr("[HudLayoutService] No panels to load into.");
                return loadedPanelIds;
            }

            var config = new ConfigFile();
            var err = config.Load(ConfigPath);

            if (err != Error.Ok)
            {
                // No saved layout yet (first launch) — not an error
                if (err == Error.FileNotFound)
                {
                    var absolutePath = ProjectSettings.GlobalizePath(ConfigPath);
                    GD.Print($"[HudLayoutService] No saved layout found at: {absolutePath}");
                }
                else
                {
                    GD.PrintErr($"[HudLayoutService] Failed to load layout: {err}");
                }
                return loadedPanelIds;
            }

            foreach (var kvp in panels)
            {
                string panelId = kvp.Key;
                var panel = kvp.Value;

                if (panel == null || !GodotObject.IsInstanceValid(panel))
                {
                    GD.Print($"[HudLayoutService] Skipping invalid panel: {panelId}");
                    continue;
                }

                if (!config.HasSection(panelId))
                {
                    GD.Print($"[HudLayoutService] No saved data for panel: {panelId}");
                    continue;
                }

                bool loadedAny = false;

                // Load position
                if (config.HasSectionKey(panelId, "position_x") && config.HasSectionKey(panelId, "position_y"))
                {
                    float x = (float)config.GetValue(panelId, "position_x");
                    float y = (float)config.GetValue(panelId, "position_y");
                    panel.SetScreenPosition(new Vector2(x, y));
                    GD.Print($"[HudLayoutService] Loaded position for {panelId}: ({x}, {y})");
                    loadedAny = true;
                }

                // Load size
                if (config.HasSectionKey(panelId, "size_x") && config.HasSectionKey(panelId, "size_y"))
                {
                    float w = (float)config.GetValue(panelId, "size_x");
                    float h = (float)config.GetValue(panelId, "size_y");
                    panel.Size = new Vector2(w, h);
                    GD.Print($"[HudLayoutService] Loaded size for {panelId}: ({w}, {h})");
                    loadedAny = true;
                }

                if (loadedAny)
                {
                    loadedPanelIds.Add(panelId);
                }
            }

            if (loadedPanelIds.Count > 0)
            {
                GD.Print($"[HudLayoutService] Successfully loaded {loadedPanelIds.Count} panel layouts.");
            }

            return loadedPanelIds;
        }

        /// <summary>
        /// Delete the saved layout file (reset to defaults).
        /// </summary>
        public static void ClearSavedLayout()
        {
            if (FileAccess.FileExists(ConfigPath))
            {
                DirAccess.RemoveAbsolute(ConfigPath);
                GD.Print("[HudLayoutService] Saved layout cleared.");
            }
        }
    }
}
