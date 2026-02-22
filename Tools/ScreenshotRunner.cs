using Godot;
using System;
using System.IO;
using System.Linq;

namespace QDND.Tools
{
    /// <summary>
    /// Screenshot runner for visual regression testing.
    /// Loads a target scene, waits for it to stabilize, and captures screenshots.
    /// 
    /// Usage:
    ///   xvfb-run -a godot --path . res://Tools/ScreenshotRunner.tscn -- \
    ///     --scene res://Combat/Arena/CombatArena.tscn \
    ///     --screenshot-out artifacts/screens \
    ///     --w 1920 --h 1080
    /// </summary>
    public partial class ScreenshotRunner : Node
    {
        [Export] public string DefaultScene = "res://Combat/Arena/CombatArena.tscn";
        [Export] public string DefaultOutputDir = "artifacts/screens";
        [Export] public int DefaultWidth = 1920;
        [Export] public int DefaultHeight = 1080;
        [Export] public int SettleFrames = 60; // Frames to wait for UI to settle (increased for richer screenshots)
        [Export] public int MaxWaitFrames = 300; // Max frames before timeout

        private string _targetScene;
        private string _outputDir;
        private int _width;
        private int _height;
        private int _frameCount = 0;
        private bool _sceneLoaded = false;
        private bool _screenshotTaken = false;
        private Node _loadedScene = null;
        private bool _openInventory = false;
        private bool _inventoryOpened = false;
        private int _inventoryWaitFrames = 30;

        public override void _Ready()
        {
            GD.Print("=== SCREENSHOT RUNNER ===");
            GD.Print($"Timestamp: {DateTime.UtcNow:O}");

            ParseArguments();

            // Set window size for deterministic rendering
            DisplayServer.WindowSetSize(new Vector2I(_width, _height));
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);

            GD.Print($"Target scene: {_targetScene}");
            GD.Print($"Output dir: {_outputDir}");
            GD.Print($"Resolution: {_width}x{_height}");

            // Ensure output directory exists
            if (!EnsureOutputDirectory())
            {
                ExitWithError("Failed to create output directory");
                return;
            }

            // Load target scene
            LoadTargetScene();
        }

        public override void _Process(double delta)
        {
            _frameCount++;

            if (!_sceneLoaded)
            {
                if (_frameCount > MaxWaitFrames)
                {
                    ExitWithError("Timeout waiting for scene to load");
                }
                return;
            }

            if (_screenshotTaken)
            {
                return;
            }

            // Wait for UI to settle
            if (_frameCount >= SettleFrames)
            {
                if (_openInventory && !_inventoryOpened)
                {
                    var hudController = FindHudController();
                    if (hudController != null)
                    {
                        GD.Print("Opening inventory for screenshot...");
                        hudController.ToggleInventory();
                    }
                    else
                    {
                        GD.PrintErr("WARNING: --open-inventory specified but HudController not found");
                    }
                    _inventoryOpened = true;
                    _frameCount = 0;
                    SettleFrames = _inventoryWaitFrames;
                    return;
                }

                TakeScreenshot();
            }
        }

        private void ParseArguments()
        {
            var userArgs = OS.GetCmdlineUserArgs();

            _targetScene = DefaultScene;
            _outputDir = DefaultOutputDir;
            _width = DefaultWidth;
            _height = DefaultHeight;

            for (int i = 0; i < userArgs.Length; i++)
            {
                string arg = userArgs[i];

                switch (arg)
                {
                    case "--scene":
                        if (i + 1 < userArgs.Length)
                            _targetScene = userArgs[++i];
                        break;
                    case "--screenshot-out":
                        if (i + 1 < userArgs.Length)
                            _outputDir = userArgs[++i];
                        break;
                    case "--w":
                        if (i + 1 < userArgs.Length && int.TryParse(userArgs[++i], out int w))
                            _width = w;
                        break;
                    case "--h":
                        if (i + 1 < userArgs.Length && int.TryParse(userArgs[++i], out int h))
                            _height = h;
                        break;
                    case "--open-inventory":
                        _openInventory = true;
                        break;
                }
            }
        }

        private bool EnsureOutputDirectory()
        {
            try
            {
                string globalPath = ProjectSettings.GlobalizePath(_outputDir);
                if (!Directory.Exists(globalPath))
                {
                    Directory.CreateDirectory(globalPath);
                    GD.Print($"Created output directory: {globalPath}");
                }
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to create directory: {ex.Message}");
                return false;
            }
        }

        private void LoadTargetScene()
        {
            GD.Print($"Loading scene: {_targetScene}");

            if (!ResourceLoader.Exists(_targetScene))
            {
                ExitWithError($"Scene not found: {_targetScene}");
                return;
            }

            // Enable full-fidelity mode for screenshots so HUD renders fully
            DebugFlags.IsFullFidelity = true;

            try
            {
                var packedScene = ResourceLoader.Load<PackedScene>(_targetScene);
                if (packedScene == null)
                {
                    ExitWithError($"Failed to load scene: {_targetScene}");
                    return;
                }

                _loadedScene = packedScene.Instantiate();
                AddChild(_loadedScene);
                _sceneLoaded = true;
                _frameCount = 0; // Reset frame counter after scene loads

                GD.Print("Scene loaded successfully");
            }
            catch (Exception ex)
            {
                ExitWithError($"Exception loading scene: {ex.Message}");
            }
        }

        private void TakeScreenshot()
        {
            _screenshotTaken = true;
            GD.Print($"Taking screenshot at frame {_frameCount}...");

            try
            {
                // Get the viewport texture
                var viewport = GetViewport();
                var img = viewport.GetTexture().GetImage();

                if (img == null)
                {
                    ExitWithError("Failed to get viewport image");
                    return;
                }

                // Generate filename based on scene name
                string sceneName = _targetScene.GetFile().GetBaseName();
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string filename = $"{sceneName}_{timestamp}.png";
                string globalPath = ProjectSettings.GlobalizePath(_outputDir);
                string fullPath = Path.Combine(globalPath, filename);

                // Save the image
                var error = img.SavePng(fullPath);
                if (error != Error.Ok)
                {
                    ExitWithError($"Failed to save screenshot: {error}");
                    return;
                }

                GD.Print($"Screenshot saved: {fullPath}");
                GD.Print($"Image size: {img.GetWidth()}x{img.GetHeight()}");

                // Also save a "latest" copy for easy comparison
                string latestPath = Path.Combine(globalPath, $"{sceneName}_latest.png");
                img.SavePng(latestPath);
                GD.Print($"Latest copy saved: {latestPath}");

                ExitWithSuccess();
            }
            catch (Exception ex)
            {
                ExitWithError($"Exception taking screenshot: {ex.Message}");
            }
        }

        private QDND.Combat.UI.HudController FindHudController()
        {
            return FindNodeOfType<QDND.Combat.UI.HudController>(GetTree().Root);
        }

        private T FindNodeOfType<T>(Node root) where T : class
        {
            if (root is T found) return found;
            foreach (var child in root.GetChildren())
            {
                var result = FindNodeOfType<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void ExitWithSuccess()
        {
            GD.Print("=== SCREENSHOT COMPLETE ===");
            GD.Print("OK");
            GetTree().Quit(0);
        }

        private void ExitWithError(string message)
        {
            GD.PrintErr($"ERROR: {message}");
            GD.Print("FAILED");
            GetTree().Quit(1);
        }
    }
}
