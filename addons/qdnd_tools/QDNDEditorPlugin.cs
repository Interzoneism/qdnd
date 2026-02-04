#if TOOLS
#nullable enable
using Godot;

namespace QDND.Editor;

/// <summary>
/// Main editor plugin for QDND tactical combat tools.
/// Adds custom docks and menu items for content authoring.
/// </summary>
[Tool]
public partial class QDNDEditorPlugin : EditorPlugin
{
    // Dock references
    private Control? _dataInspectorDock;
    private Control? _scenarioEditorDock;

    // Menu constants
    private const string MENU_NAME = "QDND";

    public override void _EnterTree()
    {
        GD.Print("[QDND] Editor plugin loading...");

        try
        {
            // Add custom menu
            AddToolMenuItem(MENU_NAME, Callable.From(OnMenuPressed));

            // Initialize docks (will be created in later phases)
            // _dataInspectorDock = CreateDataInspectorDock();
            // AddControlToDock(DockSlot.RightUl, _dataInspectorDock);

            GD.Print("[QDND] Editor plugin loaded successfully.");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[QDND] Failed to load plugin: {ex.Message}");
        }
    }

    public override void _ExitTree()
    {
        GD.Print("[QDND] Editor plugin unloading...");

        try
        {
            // Remove menu
            RemoveToolMenuItem(MENU_NAME);

            // Remove docks
            if (_dataInspectorDock != null)
            {
                RemoveControlFromDocks(_dataInspectorDock);
                _dataInspectorDock.QueueFree();
                _dataInspectorDock = null;
            }

            if (_scenarioEditorDock != null)
            {
                RemoveControlFromDocks(_scenarioEditorDock);
                _scenarioEditorDock.QueueFree();
                _scenarioEditorDock = null;
            }

            GD.Print("[QDND] Editor plugin unloaded.");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[QDND] Error during plugin unload: {ex.Message}");
        }
    }

    public override string _GetPluginName()
    {
        return "QDND Tools";
    }

    public override Texture2D? _GetPluginIcon()
    {
        // Return a custom icon if available
        return null;
    }

    private void OnMenuPressed()
    {
        GD.Print("[QDND] Menu pressed - opening tools panel");
        ShowToolsDialog();
    }

    private void ShowToolsDialog()
    {
        var dialog = new AcceptDialog();
        dialog.Title = "QDND Tools";
        dialog.DialogText = "QDND Editor Tools\n\n" +
            "• Data Inspector: View/edit ability and status definitions\n" +
            "• Scenario Editor: Visual scenario editing\n" +
            "• Debug Console: Runtime debugging commands\n\n" +
            "Enable individual docks from the View menu.";
        dialog.Size = new Vector2I(400, 200);

        EditorInterface.Singleton.GetBaseControl().AddChild(dialog);
        dialog.PopupCentered();

        dialog.Connect("confirmed", Callable.From(() => dialog.QueueFree()));
    }

    /// <summary>
    /// Get the path to the addon directory.
    /// </summary>
    public static string GetAddonPath()
    {
        return "res://addons/qdnd_tools/";
    }

    /// <summary>
    /// Get the path to the data directory.
    /// </summary>
    public static string GetDataPath()
    {
        return "res://Data/";
    }
}
#endif
