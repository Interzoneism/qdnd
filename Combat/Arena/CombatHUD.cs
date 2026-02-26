using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Entities;
using QDND.Combat.UI;
using QDND.Combat.Actions;
using QDND.Combat.Statuses;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Combat HUD controller. Manages turn tracker, action bar, and unit info displays.
    /// </summary>
    public partial class CombatHUD : Control
    {
        [Export] public CombatArena Arena;
        [Export] public bool DebugUI = true;

        // UI Elements
        private HBoxContainer _turnTracker;
        private VBoxContainer _actionBarGrid;
        private HBoxContainer _actionBar; // kept for compat - first row
        private VBoxContainer _unitInfoPanel;
        private PanelContainer _bottomBar;
        private Button _endTurnButton;
        private Label _combatStateLabel;
        private Label _turnInfoLabel;
        private Label _roundLabel;

        // State
        private Dictionary<string, Control> _turnPortraits = new();
        private List<Button> _abilityButtons = new();
        
        // Variant selection
        private PopupMenu _variantPopup;
        private List<ActionVariant> _pendingVariants;
        private int _pendingVariantAbilityIndex = -1;

        // Tooltip panel
        private PanelContainer _tooltipPanel;
        private VBoxContainer _tooltipContent;
        private TextureRect _tooltipIcon;
        private Label _tooltipName;
        private Label _tooltipCost;
        private RichTextLabel _tooltipDesc;
        private string _hoveredActionId;

        // Combat Log
        private PanelContainer _logPanel;
        private ScrollContainer _logScroll;
        private VBoxContainer _logContainer;
        private RichTextLabel _logText;
        private readonly Queue<string> _logLines = new();
        private const int MaxLogEntries = 80;

        // Resource display
        private HBoxContainer _resourceBar;
        private Dictionary<string, ProgressBar> _resourceBars = new();
        private Dictionary<string, Label> _resourceLabels = new();

        // Inspect panel (left side - hidden by default)
        private PanelContainer _inspectPanel;
        private VBoxContainer _inspectContent;
        private Label _inspectName;
        private Label _inspectFaction;
        private ProgressBar _inspectHpBar;
        private Label _inspectHpText;
        private VBoxContainer _inspectStatusList;
        private Label _inspectInitiative;

        // Character Portrait (bottom-left, per layout spec) - Enhanced BG3-style character sheet
        private PanelContainer _portraitPanel;
        private ScrollContainer _portraitScroll;
        private VBoxContainer _portraitContent;
        private Label _portraitName;
        private Label _portraitRaceClass;
        private Label _portraitAC;
        private ProgressBar _portraitHpBar;
        private Label _portraitHpText;
        private VBoxContainer _portraitAbilityScores;
        private VBoxContainer _portraitCombatStats;
        private VBoxContainer _portraitSavingThrows;
        private VBoxContainer _portraitSkills;
        private VBoxContainer _portraitFeatures;
        private VBoxContainer _portraitResources;
        private StyleBoxFlat _portraitPanelStyle;
        private StyleBoxFlat _portraitHpFillStyle;
        private string _portraitCombatantId;
        private int _portraitCurrentHp = int.MinValue;
        private int _portraitMaxHp = int.MinValue;
        private int _portraitInitiative = int.MinValue;
        private Color _portraitBorderColor = new Color(-1f, -1f, -1f, -1f);

        // Cleanup flag to prevent zombie callbacks
        private bool _disposed = false;
        private bool _logScrollQueued = false;

        private readonly Color _actionCostColor = new Color(0.318f, 0.812f, 0.4f);
        private readonly Color _bonusCostColor = new Color(1.0f, 0.663f, 0.302f);
        private readonly Color _moveCostColor = new Color(1.0f, 0.831f, 0.231f);
        private readonly Color _reactionCostColor = new Color(0.8f, 0.365f, 0.91f);

        // Store service references for cleanup
        private CombatStateMachine _stateMachine;
        private TurnQueueService _turnQueue;
        private CombatLog _combatLog;

        public override void _Ready()
        {
            CallDeferred(nameof(DeferredInit));
        }

        public override void _ExitTree()
        {
            _disposed = true;
            UnsubscribeFromEvents();
            base._ExitTree();
        }

        /// <summary>
        /// Public cleanup method for graceful shutdown. Call before freeing.
        /// </summary>
        public void Cleanup()
        {
            GD.Print($"[CombatHUD] Cleanup called - already disposed={_disposed}");
            _disposed = true;
            UnsubscribeFromEvents();
            GD.Print("[CombatHUD] Cleanup complete");
        }

        private void UnsubscribeFromEvents()
        {
            // Unsubscribe from combat service events
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= OnStateChanged;
            
            if (_turnQueue != null)
                _turnQueue.OnTurnChanged -= OnTurnChanged;
            
            if (_combatLog != null)
                _combatLog.OnEntryAdded -= OnLogEntryAdded;

            // Unsubscribe from UI model events
            if (Arena != null)
            {
                if (Arena.TurnTrackerModel != null)
                {
                    Arena.TurnTrackerModel.TurnOrderChanged -= OnTurnOrderChanged;
                    Arena.TurnTrackerModel.ActiveCombatantChanged -= OnActiveCombatantChanged;
                    Arena.TurnTrackerModel.EntryUpdated -= OnTurnEntryUpdated;
                }

                if (Arena.ResourceBarModel != null)
                {
                    Arena.ResourceBarModel.ResourceChanged -= OnResourceChanged;
                    Arena.ResourceBarModel.HealthChanged -= OnHealthChanged;
                }

                if (Arena.ActionBarModel != null)
                {
                    Arena.ActionBarModel.ActionsChanged -= OnActionsChanged;
                    Arena.ActionBarModel.ActionUpdated -= OnActionUpdated;
                }
            }

            // Unsubscribe from button events
            if (_endTurnButton != null)
                _endTurnButton.Pressed -= OnEndTurnPressed;
        }

        private void DeferredInit()
        {
            GD.Print($"[CombatHUD] DeferredInit called - _disposed={_disposed}, IsValid={IsInstanceValid(this)}, InTree={IsInsideTree()}");
            
            // Guard against running after cleanup/disposal (can happen due to deferred call timing)
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
            {
                GD.Print("[CombatHUD] DeferredInit skipped - already disposed or removed from tree");
                return;
            }

            if (Arena == null)
            {
                Arena = GetTree().Root.FindChild("CombatArena", true, false) as CombatArena;
            }

            // Skip HUD setup in auto-battle mode UNLESS full-fidelity mode is enabled
            if (Arena != null && Arena.IsAutoBattleMode && !QDND.Tools.DebugFlags.IsFullFidelity)
            {
                GD.Print("[CombatHUD] Auto-battle mode detected (fast mode) - disabling HUD");
                _disposed = true;  // Mark as disposed so event handlers skip their work
                return;
            }

            GD.Print("[CombatHUD] DeferredInit proceeding with setup");

            SetupUI();

            if (Arena != null)
            {
                // Subscribe to combat events via the context
                var context = Arena.Context;
                if (context != null)
                {
                    _stateMachine = context.GetService<CombatStateMachine>();
                    _turnQueue = context.GetService<TurnQueueService>();

                    if (_stateMachine != null)
                        _stateMachine.OnStateChanged += OnStateChanged;
                    if (_turnQueue != null)
                        _turnQueue.OnTurnChanged += OnTurnChanged;

                    _combatLog = context.GetService<CombatLog>();
                    if (_combatLog != null)
                    {
                        _combatLog.OnEntryAdded += OnLogEntryAdded;

                        // Also show existing entries
                        foreach (var entry in _combatLog.GetRecentEntries(MaxLogEntries))
                        {
                            AddLogEntry(entry);
                        }
                    }
                }

                // Initial population of turn tracker
                InitializeTurnTracker();

                // Subscribe to UI models if available
                if (Arena.TurnTrackerModel != null)
                {
                    Arena.TurnTrackerModel.TurnOrderChanged += OnTurnOrderChanged;
                    Arena.TurnTrackerModel.ActiveCombatantChanged += OnActiveCombatantChanged;
                    Arena.TurnTrackerModel.EntryUpdated += OnTurnEntryUpdated;
                }

                if (Arena.ResourceBarModel != null)
                {
                    Arena.ResourceBarModel.ResourceChanged += OnResourceChanged;
                    Arena.ResourceBarModel.HealthChanged += OnHealthChanged;
                }

                if (Arena.ActionBarModel != null)
                {
                    Arena.ActionBarModel.ActionsChanged += OnActionsChanged;
                    Arena.ActionBarModel.ActionUpdated += OnActionUpdated;
                }

                // Ensure the HUD is populated even if initial model events fired before subscription.
                SyncHudFromModels();
            }
        }

        private void SyncHudFromModels()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree() || Arena == null)
                return;

            if (Arena.ActionBarModel != null)
            {
                if (Arena.ActionBarModel.Actions.Count > 0)
                {
                    OnActionsChanged();
                }
                else if (!string.IsNullOrEmpty(Arena.ActiveCombatantId))
                {
                    UpdateAbilityButtons(Arena.ActiveCombatantId);
                }
            }

            if (Arena.ResourceBarModel != null)
            {
                foreach (string resourceId in new[] { "action", "bonus_action", "move", "reaction" })
                {
                    var resource = Arena.ResourceBarModel.GetResource(resourceId);
                    if (resource != null)
                    {
                        UpdateResourceBar(resourceId, resource.Current, resource.Maximum);
                    }
                }
            }
        }

        private void InitializeTurnTracker()
        {
            if (Arena == null) return;

            var combatants = Arena.GetCombatants();
            var currentId = Arena.Context?.GetService<TurnQueueService>()?.CurrentCombatant?.Id;
            RefreshTurnTracker(combatants, currentId);
        }

        private void SetupUI()
        {
            if (DebugUI)
                GD.Print("[CombatHUD] === NEW BG3-STYLE HUD SETUP ===");

            // Main container
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore; // Allow clicks to pass through to 3D

            if (DebugUI)
                GD.Print($"[CombatHUD] MouseFilter set to: {MouseFilter}");

            SetupTurnTracker();

            // Hotbar Panel - BG3 dark fantasy style, dynamic grid
            _bottomBar = new PanelContainer();
            _bottomBar.AnchorLeft = 0.5f;  // Center-anchored
            _bottomBar.AnchorRight = 0.5f;
            _bottomBar.AnchorTop = 1.0f;
            _bottomBar.AnchorBottom = 1.0f;
            _bottomBar.OffsetLeft = -400;   // Wider to fit grid layout
            _bottomBar.OffsetRight = 400;
            _bottomBar.OffsetTop = -180;    // Taller for multiple rows
            _bottomBar.OffsetBottom = -44;  // Still ends above End Turn button
            _bottomBar.CustomMinimumSize = new Vector2(800, 136);
            _bottomBar.MouseFilter = MouseFilterEnum.Stop;
            _bottomBar.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            
            var style = new StyleBoxFlat();
            style.BgColor = new Color(12f/255f, 10f/255f, 18f/255f, 0.94f);  // Primary dark
            style.SetCornerRadiusAll(10);
            style.SetBorderWidthAll(1);
            style.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.3f);  // Gold border
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 6;
            style.ContentMarginBottom = 6;
            _bottomBar.AddThemeStyleboxOverride("panel", style);
            
            AddChild(_bottomBar);

            if (DebugUI)
                GD.Print($"[CombatHUD] Hotbar panel created (dynamic grid)");

            // Grid layout: VBox with rows of abilities
            _actionBarGrid = new VBoxContainer();
            _actionBarGrid.AddThemeConstantOverride("separation", 4);
            _actionBarGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _bottomBar.AddChild(_actionBarGrid);

            // Row 1: Class abilities / spells
            _actionBar = new HBoxContainer();
            _actionBar.AddThemeConstantOverride("separation", 3);
            _actionBar.Alignment = BoxContainer.AlignmentMode.Center;
            _actionBarGrid.AddChild(_actionBar);

            // Row 2: Common actions (populated dynamically in OnActionsChanged)
            // Rows are populated entirely from OnActionsChanged

            // End Turn Button - separate panel, absolute positioned
            // Position per spec: X: 1716px, Y: 956px, Size: 180x84
            SetupEndTurnButton();

            // Bottom bar starts visible during player turn
            _bottomBar.Visible = true;

            SetupCombatLog();
            SetupResourceBar();
            SetupInspectPanel();
            SetupCharacterPortrait();
            SetupVariantPopup();
            SetupTooltipPanel();
        }

        private void SetupTooltipPanel()
        {
            _tooltipPanel = new PanelContainer();
            _tooltipPanel.Visible = false;
            _tooltipPanel.MouseFilter = MouseFilterEnum.Ignore;
            _tooltipPanel.CustomMinimumSize = new Vector2(280, 100);
            _tooltipPanel.AnchorLeft = 0.5f;
            _tooltipPanel.AnchorRight = 0.5f;
            _tooltipPanel.AnchorTop = 1.0f;
            _tooltipPanel.AnchorBottom = 1.0f;
            _tooltipPanel.OffsetLeft = -140;
            _tooltipPanel.OffsetRight = 140;
            _tooltipPanel.OffsetTop = -340; // Above the hotbar
            _tooltipPanel.OffsetBottom = -190;

            var tooltipStyle = new StyleBoxFlat();
            tooltipStyle.BgColor = new Color(18f/255f, 14f/255f, 26f/255f, 0.96f);
            tooltipStyle.SetCornerRadiusAll(8);
            tooltipStyle.SetBorderWidthAll(2);
            tooltipStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.6f);
            tooltipStyle.ContentMarginLeft = 12;
            tooltipStyle.ContentMarginRight = 12;
            tooltipStyle.ContentMarginTop = 10;
            tooltipStyle.ContentMarginBottom = 10;
            _tooltipPanel.AddThemeStyleboxOverride("panel", tooltipStyle);
            AddChild(_tooltipPanel);

            _tooltipContent = new VBoxContainer();
            _tooltipContent.AddThemeConstantOverride("separation", 6);
            _tooltipContent.MouseFilter = MouseFilterEnum.Ignore;
            _tooltipPanel.AddChild(_tooltipContent);

            // Header row: icon + name
            var headerRow = new HBoxContainer();
            headerRow.AddThemeConstantOverride("separation", 8);
            headerRow.MouseFilter = MouseFilterEnum.Ignore;
            _tooltipContent.AddChild(headerRow);

            _tooltipIcon = new TextureRect();
            _tooltipIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _tooltipIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _tooltipIcon.CustomMinimumSize = new Vector2(32, 32);
            _tooltipIcon.MouseFilter = MouseFilterEnum.Ignore;
            headerRow.AddChild(_tooltipIcon);

            var nameCol = new VBoxContainer();
            nameCol.AddThemeConstantOverride("separation", 0);
            nameCol.MouseFilter = MouseFilterEnum.Ignore;
            headerRow.AddChild(nameCol);

            _tooltipName = new Label();
            _tooltipName.AddThemeFontSizeOverride("font_size", 14);
            _tooltipName.AddThemeColorOverride("font_color", new Color(200f/255f, 168f/255f, 78f/255f)); // Gold
            _tooltipName.MouseFilter = MouseFilterEnum.Ignore;
            nameCol.AddChild(_tooltipName);

            _tooltipCost = new Label();
            _tooltipCost.AddThemeFontSizeOverride("font_size", 10);
            _tooltipCost.AddThemeColorOverride("font_color", new Color(180f/255f, 180f/255f, 180f/255f));
            _tooltipCost.MouseFilter = MouseFilterEnum.Ignore;
            nameCol.AddChild(_tooltipCost);

            // Separator
            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", CreateSeparatorStyle());
            _tooltipContent.AddChild(sep);

            // Description
            _tooltipDesc = new RichTextLabel();
            _tooltipDesc.BbcodeEnabled = true;
            _tooltipDesc.FitContent = true;
            _tooltipDesc.ScrollActive = false;
            _tooltipDesc.CustomMinimumSize = new Vector2(256, 30);
            _tooltipDesc.AddThemeFontSizeOverride("normal_font_size", 11);
            _tooltipDesc.AddThemeColorOverride("default_color", new Color(232f/255f, 224f/255f, 208f/255f));
            _tooltipDesc.MouseFilter = MouseFilterEnum.Ignore;
            _tooltipContent.AddChild(_tooltipDesc);
        }

        private void OnAbilityHovered(int index)
        {
            if (_disposed || Arena?.ActionBarModel == null) return;

            var actions = Arena.ActionBarModel.Actions.ToList();
            if (index < 0 || index >= actions.Count) return;

            var action = actions[index];
            _hoveredActionId = action.ActionId;

            // Populate tooltip
            _tooltipName.Text = action.DisplayName ?? "Unknown";

            // Build cost string
            var costParts = new List<string>();
            if (action.ActionPointCost > 0)
                costParts.Add("Action");
            if (action.BonusActionCost > 0)
                costParts.Add("Bonus Action");
            if (action.MovementCost > 0)
                costParts.Add($"{action.MovementCost}m Movement");
            _tooltipCost.Text = costParts.Count > 0 ? string.Join(" · ", costParts) : "Free";

            // Description
            _tooltipDesc.Text = "";
            _tooltipDesc.AppendText(action.Description ?? "No description available.");

            // Icon
            if (!string.IsNullOrEmpty(action.IconPath) && action.IconPath.StartsWith("res://"))
            {
                var tex = ResourceLoader.Exists(action.IconPath)
                    ? ResourceLoader.Load<Texture2D>(action.IconPath)
                    : null;
                _tooltipIcon.Texture = tex;
                _tooltipIcon.Visible = tex != null;
            }
            else
            {
                _tooltipIcon.Visible = false;
            }

            _tooltipPanel.Visible = true;
        }

        private void OnAbilityHoverExit()
        {
            _hoveredActionId = null;
            if (_tooltipPanel != null && IsInstanceValid(_tooltipPanel))
            {
                _tooltipPanel.Visible = false;
            }
        }

        private void SetupVariantPopup()
        {
            _variantPopup = new PopupMenu();
            _variantPopup.IdPressed += OnVariantSelected;
            AddChild(_variantPopup);
        }

        private void SetupTurnTracker()
        {
            // Turn Tracker - sleek floating bar, centered at top
            var topBar = new PanelContainer();
            topBar.AnchorLeft = 0.5f;
            topBar.AnchorRight = 0.5f;
            topBar.AnchorTop = 0.0f;
            topBar.AnchorBottom = 0.0f;
            topBar.OffsetLeft = -400;  // Center 800px width
            topBar.OffsetRight = 400;
            topBar.OffsetTop = 12;
            topBar.OffsetBottom = 92;  // 80px height
            topBar.CustomMinimumSize = new Vector2(800, 80);
            topBar.MouseFilter = MouseFilterEnum.Stop;
            
            var style = new StyleBoxFlat();
            style.BgColor = new Color(12f/255f, 10f/255f, 18f/255f, 0.92f);  // Primary background
            style.SetCornerRadiusAll(8);  // Medium radius for panels
            style.SetBorderWidthAll(1);
            style.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.25f);  // Gold border
            // Add padding via content margins
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            topBar.AddThemeStyleboxOverride("panel", style);
            
            AddChild(topBar);

            if (DebugUI)
                GD.Print($"[CombatHUD] Turn tracker floating bar created");

            _turnTracker = new HBoxContainer();
            _turnTracker.Alignment = BoxContainer.AlignmentMode.Center;
            _turnTracker.AddThemeConstantOverride("separation", 8);
            topBar.AddChild(_turnTracker);

            // Round counter label - placed before the portraits
            _roundLabel = new Label();
            _roundLabel.Text = "R1";
            _roundLabel.AddThemeFontSizeOverride("font_size", 18);
            _roundLabel.AddThemeColorOverride("font_color", new Color(200f/255f, 168f/255f, 78f/255f)); // Gold
            _roundLabel.VerticalAlignment = VerticalAlignment.Center;
            _roundLabel.CustomMinimumSize = new Vector2(40, 0);
            _turnTracker.AddChild(_roundLabel);
            _turnTracker.MoveChild(_roundLabel, 0); // Ensure it's first
        }

        private void SetupResourceBar()
        {
            // Resource bar - compact, ABOVE hotbar, centered
            var resourcePanel = new PanelContainer();
            resourcePanel.AnchorLeft = 0.5f;
            resourcePanel.AnchorRight = 0.5f;
            resourcePanel.AnchorTop = 1.0f;
            resourcePanel.AnchorBottom = 1.0f;
            resourcePanel.OffsetLeft = -140;     // Center 280px width
            resourcePanel.OffsetRight = 140;
            resourcePanel.OffsetTop = -156;      // Above hotbar (hotbar starts at -116)
            resourcePanel.OffsetBottom = -120;   // 36px tall, 4px gap above hotbar
            resourcePanel.CustomMinimumSize = new Vector2(280, 36);

            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(12f/255f, 10f/255f, 18f/255f, 0.85f);  // Primary dark
            panelStyle.SetCornerRadiusAll(6);  // Small radius
            panelStyle.SetBorderWidthAll(0);   // No border for clean look
            resourcePanel.AddThemeStyleboxOverride("panel", panelStyle);
            AddChild(resourcePanel);

            _resourceBar = new HBoxContainer();
            _resourceBar.Alignment = BoxContainer.AlignmentMode.Center;
            _resourceBar.AddThemeConstantOverride("separation", 6);
            resourcePanel.AddChild(_resourceBar);

            // Initialize with default resource displays
            InitializeResourceDisplays();
        }

        private void InitializeResourceDisplays()
        {
            // Clear existing
            foreach (var child in _resourceBar.GetChildren())
                child.QueueFree();
            _resourceBars.Clear();
            _resourceLabels.Clear();

            // Resource colors per layout spec
            // ACT (Action): #51CF66 (green)
            // BNS (Bonus Action): #FFA94D (orange)
            // MOV (Movement): #FFD43B (yellow)
            // RXN (Reaction): #CC5DE8 (purple)
            CreateResourceDisplay("action", "ACT", new Color(0.318f, 0.812f, 0.4f), 1, 1);      // Green #51CF66
            CreateResourceDisplay("bonus_action", "BNS", new Color(1.0f, 0.663f, 0.302f), 1, 1); // Orange #FFA94D
            int defaultMove = GetDefaultMovePoints();
            CreateResourceDisplay("move", "MOV", new Color(1.0f, 0.831f, 0.231f), defaultMove, defaultMove); // Yellow #FFD43B
            CreateResourceDisplay("reaction", "RXN", new Color(0.8f, 0.365f, 0.91f), 1, 1);     // Purple #CC5DE8
        }

        private void SetupInspectPanel()
        {
            // Inspect panel - left side, polished dark fantasy style
            _inspectPanel = new PanelContainer();
            _inspectPanel.AnchorLeft = 0.0f;
            _inspectPanel.AnchorRight = 0.0f;
            _inspectPanel.AnchorTop = 0.15f;
            _inspectPanel.AnchorBottom = 0.6f;
            _inspectPanel.OffsetLeft = 10;
            _inspectPanel.OffsetRight = 230;
            _inspectPanel.OffsetTop = 0;
            _inspectPanel.OffsetBottom = 0;
            _inspectPanel.CustomMinimumSize = new Vector2(210, 180);
            _inspectPanel.Visible = false; // Hidden by default

            var style = new StyleBoxFlat();
            style.BgColor = new Color(12f/255f, 10f/255f, 18f/255f, 0.92f);  // Primary dark
            style.SetCornerRadiusAll(8);
            style.SetBorderWidthAll(1);
            style.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.3f);  // Gold border
            _inspectPanel.AddThemeStyleboxOverride("panel", style);
            AddChild(_inspectPanel);

            _inspectContent = new VBoxContainer();
            _inspectContent.AddThemeConstantOverride("separation", 8);
            _inspectPanel.AddChild(_inspectContent);

            // Name - gold color
            _inspectName = new Label();
            _inspectName.AddThemeFontSizeOverride("font_size", 16);
            _inspectName.AddThemeColorOverride("font_color", new Color(200f/255f, 168f/255f, 78f/255f));
            _inspectName.HorizontalAlignment = HorizontalAlignment.Center;
            _inspectContent.AddChild(_inspectName);

            // Faction
            _inspectFaction = new Label();
            _inspectFaction.AddThemeFontSizeOverride("font_size", 12);
            _inspectFaction.HorizontalAlignment = HorizontalAlignment.Center;
            _inspectContent.AddChild(_inspectFaction);

            // Separator
            _inspectContent.AddChild(new HSeparator());

            // HP Bar
            var hpContainer = new VBoxContainer();
            _inspectContent.AddChild(hpContainer);

            var hpLabel = new Label();
            hpLabel.Text = "Health";
            hpLabel.AddThemeFontSizeOverride("font_size", 12);
            hpContainer.AddChild(hpLabel);

            _inspectHpBar = new ProgressBar();
            _inspectHpBar.CustomMinimumSize = new Vector2(200, 20);
            _inspectHpBar.ShowPercentage = false;

            var hpBgStyle = new StyleBoxFlat();
            hpBgStyle.BgColor = new Color(0.3f, 0.1f, 0.1f);
            _inspectHpBar.AddThemeStyleboxOverride("background", hpBgStyle);

            var hpFillStyle = new StyleBoxFlat();
            hpFillStyle.BgColor = new Color(0.2f, 0.8f, 0.2f);
            _inspectHpBar.AddThemeStyleboxOverride("fill", hpFillStyle);
            hpContainer.AddChild(_inspectHpBar);

            _inspectHpText = new Label();
            _inspectHpText.AddThemeFontSizeOverride("font_size", 12);
            _inspectHpText.HorizontalAlignment = HorizontalAlignment.Center;
            hpContainer.AddChild(_inspectHpText);

            // Initiative
            _inspectInitiative = new Label();
            _inspectInitiative.AddThemeFontSizeOverride("font_size", 12);
            _inspectContent.AddChild(_inspectInitiative);

            // Statuses section
            _inspectContent.AddChild(new HSeparator());

            var statusHeader = new Label();
            statusHeader.Text = "Active Effects";
            statusHeader.AddThemeFontSizeOverride("font_size", 14);
            _inspectContent.AddChild(statusHeader);

            _inspectStatusList = new VBoxContainer();
            _inspectStatusList.AddThemeConstantOverride("separation", 2);
            _inspectContent.AddChild(_inspectStatusList);
        }

        private void SetupCharacterPortrait()
        {
            // Character Portrait - BG3-style character sheet, bottom-left area
            // DOUBLED SIZE: 360x220 (from 180x110)
            _portraitPanel = new PanelContainer();
            _portraitPanel.AnchorLeft = 0.0f;
            _portraitPanel.AnchorRight = 0.0f;
            _portraitPanel.AnchorTop = 1.0f;
            _portraitPanel.AnchorBottom = 1.0f;
            // Left side, extends higher to accommodate more content
            _portraitPanel.OffsetLeft = 20;
            _portraitPanel.OffsetRight = 380;   // 360px wide (doubled from 180)
            _portraitPanel.OffsetTop = -240;   // 220px tall (doubled from 110)
            _portraitPanel.OffsetBottom = -20;
            _portraitPanel.CustomMinimumSize = new Vector2(360, 220);

            _portraitPanelStyle = new StyleBoxFlat();
            _portraitPanelStyle.BgColor = new Color(12f/255f, 10f/255f, 18f/255f, 0.92f);  // Primary dark
            _portraitPanelStyle.SetCornerRadiusAll(8);
            _portraitPanelStyle.SetBorderWidthAll(2);
            _portraitPanelStyle.BorderColor = new Color(0.318f, 0.8f, 0.318f);  // Health-based (updated in code)
            _portraitPanel.AddThemeStyleboxOverride("panel", _portraitPanelStyle);
            AddChild(_portraitPanel);

            // Scrollable container for character stat content
            _portraitScroll = new ScrollContainer();
            _portraitScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _portraitScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            _portraitScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            _portraitScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
            _portraitPanel.AddChild(_portraitScroll);

            _portraitContent = new VBoxContainer();
            _portraitContent.AddThemeConstantOverride("separation", 6);
            _portraitScroll.AddChild(_portraitContent);

            // === HEADER SECTION ===
            _portraitName = new Label();
            _portraitName.Text = "Click a Character";
            _portraitName.AddThemeFontSizeOverride("font_size", 16);
            _portraitName.AddThemeColorOverride("font_color", new Color(232f/255f, 224f/255f, 208f/255f));  // Warm white
            _portraitName.HorizontalAlignment = HorizontalAlignment.Center;
            _portraitContent.AddChild(_portraitName);

            _portraitRaceClass = new Label();
            _portraitRaceClass.Text = "";
            _portraitRaceClass.AddThemeFontSizeOverride("font_size", 11);
            _portraitRaceClass.AddThemeColorOverride("font_color", new Color(170f/255f, 160f/255f, 140f/255f));  // Muted beige
            _portraitRaceClass.HorizontalAlignment = HorizontalAlignment.Center;
            _portraitContent.AddChild(_portraitRaceClass);

            // HP Bar
            _portraitHpBar = new ProgressBar();
            _portraitHpBar.CustomMinimumSize = new Vector2(330, 12);
            _portraitHpBar.ShowPercentage = false;
            _portraitHpBar.Value = 100;

            var hpBgStyle = new StyleBoxFlat();
            hpBgStyle.BgColor = new Color(0.2f, 0.1f, 0.1f);
            hpBgStyle.SetCornerRadiusAll(3);
            _portraitHpBar.AddThemeStyleboxOverride("background", hpBgStyle);

            _portraitHpFillStyle = new StyleBoxFlat();
            _portraitHpFillStyle.BgColor = new Color(0.318f, 0.8f, 0.318f);
            _portraitHpFillStyle.SetCornerRadiusAll(3);
            _portraitHpBar.AddThemeStyleboxOverride("fill", _portraitHpFillStyle);
            _portraitContent.AddChild(_portraitHpBar);

            _portraitHpText = new Label();
            _portraitHpText.Text = "HP: --/--";
            _portraitHpText.AddThemeFontSizeOverride("font_size", 10);
            _portraitHpText.AddThemeColorOverride("font_color", new Color(232f/255f, 224f/255f, 208f/255f));
            _portraitHpText.HorizontalAlignment = HorizontalAlignment.Center;
            _portraitContent.AddChild(_portraitHpText);

            AddSectionSeparator(_portraitContent);

            // === ABILITY SCORES SECTION ===
            _portraitAbilityScores = CreateStatSection(_portraitContent, "ABILITY SCORES");

            AddSectionSeparator(_portraitContent);

            // === COMBAT STATS SECTION ===
            _portraitCombatStats = CreateStatSection(_portraitContent, "COMBAT STATS");

            AddSectionSeparator(_portraitContent);

            // === SAVING THROWS SECTION ===
            _portraitSavingThrows = CreateStatSection(_portraitContent, "SAVING THROWS");

            AddSectionSeparator(_portraitContent);

            // === SKILLS SECTION ===
            _portraitSkills = CreateStatSection(_portraitContent, "PROFICIENT SKILLS");

            AddSectionSeparator(_portraitContent);

            // === FEATURES SECTION ===
            _portraitFeatures = CreateStatSection(_portraitContent, "FEATURES");

            AddSectionSeparator(_portraitContent);

            // === RESOURCES SECTION ===
            _portraitResources = CreateStatSection(_portraitContent, "RESOURCES");

            if (DebugUI)
                GD.Print("[CombatHUD] BG3-style character sheet panel created (360x220)");
        }

        private VBoxContainer CreateStatSection(Control parent, string title)
        {
            var header = new Label();
            header.Text = title;
            header.AddThemeFontSizeOverride("font_size", 10);
            header.AddThemeColorOverride("font_color", new Color(200f/255f, 168f/255f, 78f/255f));  // Gold header
            header.HorizontalAlignment = HorizontalAlignment.Left;
            parent.AddChild(header);

            var container = new VBoxContainer();
            container.AddThemeConstantOverride("separation", 2);
            parent.AddChild(container);

            return container;
        }

        private void AddSectionSeparator(Control parent)
        {
            var separator = new HSeparator();
            separator.CustomMinimumSize = new Vector2(0, 1);
            
            var sepStyle = new StyleBoxFlat();
            sepStyle.BgColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.2f);  // Subtle gold line
            separator.AddThemeStyleboxOverride("separator", sepStyle);
            
            parent.AddChild(separator);
        }

        /// <summary>
        /// Public method to show character sheet for any combatant (called when clicking a character).
        /// </summary>
        public void ShowCharacterSheet(Combatant combatant)
        {
            if (combatant == null) return;
            UpdateCharacterPortrait(combatant);
        }

        private void UpdateCharacterPortrait(Combatant combatant)
        {
            if (_portraitPanel == null || combatant == null) return;

            float hpPercent = combatant.Resources.MaxHP > 0
                ? (float)combatant.Resources.CurrentHP / combatant.Resources.MaxHP * 100
                : 0;
            var borderColor = GetHealthColor(hpPercent);

            bool unchanged =
                _portraitCombatantId == combatant.Id &&
                _portraitCurrentHp == combatant.Resources.CurrentHP &&
                _portraitMaxHp == combatant.Resources.MaxHP &&
                _portraitInitiative == combatant.Initiative &&
                _portraitBorderColor == borderColor;

            if (unchanged)
            {
                return;
            }

            _portraitCombatantId = combatant.Id;
            _portraitCurrentHp = combatant.Resources.CurrentHP;
            _portraitMaxHp = combatant.Resources.MaxHP;
            _portraitInitiative = combatant.Initiative;
            _portraitBorderColor = borderColor;

            // === UPDATE HEADER ===
            _portraitName.Text = combatant.Name;

            // Race + Class (for ResolvedCharacter)
            if (combatant.ResolvedCharacter != null && combatant.ResolvedCharacter.Sheet != null)
            {
                var sheet = combatant.ResolvedCharacter.Sheet;
                var raceStr = sheet.RaceId ?? "Unknown";
                var classStr = sheet.ClassLevels != null && sheet.ClassLevels.Count > 0
                    ? string.Join(", ", sheet.ClassLevels.GroupBy(cl => cl.ClassId).Select(g => $"{g.Key} {g.Count()}"))
                    : "";
                _portraitRaceClass.Text = $"{raceStr} {classStr}".Trim();
            }
            else
            {
                _portraitRaceClass.Text = "Legacy Unit";
            }

            // HP Bar
            _portraitHpBar.Value = hpPercent;
            _portraitHpText.Text = $"HP: {combatant.Resources.CurrentHP}/{combatant.Resources.MaxHP}";

            if (_portraitHpFillStyle != null)
            {
                _portraitHpFillStyle.BgColor = borderColor;
            }

            if (_portraitPanelStyle != null)
            {
                _portraitPanelStyle.BorderColor = borderColor;
            }

            // === UPDATE SECTIONS ===
            if (combatant.ResolvedCharacter != null)
            {
                UpdateAbilityScores(combatant);
                UpdateCombatStats(combatant);
                UpdateSavingThrows(combatant);
                UpdateSkills(combatant);
                UpdateFeatures(combatant);
                UpdateResources(combatant);
            }
            else
            {
                // Legacy unit - show minimal info
                ClearStatSection(_portraitAbilityScores);
                ClearStatSection(_portraitCombatStats);
                AddStatLabel(_portraitCombatStats, $"AC: {10 + combatant.Initiative / 2}", Colors.White);
                AddStatLabel(_portraitCombatStats, $"Initiative: +{combatant.Initiative}", Colors.White);
                ClearStatSection(_portraitSavingThrows);
                ClearStatSection(_portraitSkills);
                ClearStatSection(_portraitFeatures);
                ClearStatSection(_portraitResources);
            }
        }

        private void UpdateAbilityScores(Combatant combatant)
        {
            ClearStatSection(_portraitAbilityScores);
            var rc = combatant.ResolvedCharacter;
            if (rc == null) return;

            var abilities = new[] {
                ("STR", QDND.Data.CharacterModel.AbilityType.Strength),
                ("DEX", QDND.Data.CharacterModel.AbilityType.Dexterity),
                ("CON", QDND.Data.CharacterModel.AbilityType.Constitution),
                ("INT", QDND.Data.CharacterModel.AbilityType.Intelligence),
                ("WIS", QDND.Data.CharacterModel.AbilityType.Wisdom),
                ("CHA", QDND.Data.CharacterModel.AbilityType.Charisma)
            };

            foreach (var (abbr, type) in abilities)
            {
                int score = rc.AbilityScores[type];
                int modifier = rc.GetModifier(type);
                string modStr = modifier >= 0 ? $"+{modifier}" : $"{modifier}";
                AddStatLabel(_portraitAbilityScores, $"{abbr} {score} ({modStr})", Colors.White);
            }
        }

        private void UpdateCombatStats(Combatant combatant)
        {
            ClearStatSection(_portraitCombatStats);
            var rc = combatant.ResolvedCharacter;
            if (rc == null) return;

            AddStatLabel(_portraitCombatStats, $"AC: {rc.BaseAC}", new Color(200f/255f, 168f/255f, 78f/255f));
            AddStatLabel(_portraitCombatStats, $"Initiative: +{combatant.Initiative}", Colors.White);
            AddStatLabel(_portraitCombatStats, $"Speed: {rc.Speed} ft", Colors.White);
            AddStatLabel(_portraitCombatStats, $"Prof. Bonus: +{combatant.ProficiencyBonus}", Colors.White);
        }

        private void UpdateSavingThrows(Combatant combatant)
        {
            ClearStatSection(_portraitSavingThrows);
            var rc = combatant.ResolvedCharacter;
            if (rc == null) return;

            var saves = new[] {
                ("STR", QDND.Data.CharacterModel.AbilityType.Strength),
                ("DEX", QDND.Data.CharacterModel.AbilityType.Dexterity),
                ("CON", QDND.Data.CharacterModel.AbilityType.Constitution),
                ("INT", QDND.Data.CharacterModel.AbilityType.Intelligence),
                ("WIS", QDND.Data.CharacterModel.AbilityType.Wisdom),
                ("CHA", QDND.Data.CharacterModel.AbilityType.Charisma)
            };

            foreach (var (abbr, type) in saves)
            {
                int bonus = rc.GetSavingThrowBonus(type, combatant.ProficiencyBonus);
                string bonusStr = bonus >= 0 ? $"+{bonus}" : $"{bonus}";
                bool isProficient = rc.Proficiencies.IsProficientInSave(type);
                string profMarker = isProficient ? " (Prof)" : "";
                var color = isProficient ? new Color(200f/255f, 168f/255f, 78f/255f) : Colors.White;
                AddStatLabel(_portraitSavingThrows, $"{abbr} {bonusStr}{profMarker}", color);
            }
        }

        private void UpdateSkills(Combatant combatant)
        {
            ClearStatSection(_portraitSkills);
            var rc = combatant.ResolvedCharacter;
            if (rc == null) return;

            var proficientSkills = rc.Proficiencies.Skills.ToList();
            if (proficientSkills.Count == 0)
            {
                AddStatLabel(_portraitSkills, "None", new Color(0.5f, 0.5f, 0.5f));
                return;
            }

            // Only show proficient skills (limit to first 10 for space)
            foreach (var skill in proficientSkills.Take(10))
            {
                int bonus = rc.GetSkillBonus(skill, combatant.ProficiencyBonus);
                string bonusStr = bonus >= 0 ? $"+{bonus}" : $"{bonus}";
                bool hasExpertise = rc.Proficiencies.HasExpertise(skill);
                string expertMarker = hasExpertise ? " (Exp)" : "";
                var color = hasExpertise ? new Color(1.0f, 0.84f, 0.0f) : new Color(200f/255f, 168f/255f, 78f/255f);
                AddStatLabel(_portraitSkills, $"{FormatSkillName(skill)} {bonusStr}{expertMarker}", color);
            }
        }

        private void UpdateFeatures(Combatant combatant)
        {
            ClearStatSection(_portraitFeatures);
            var rc = combatant.ResolvedCharacter;
            if (rc == null || rc.Features == null) return;

            var features = rc.Features.Take(10).ToList();
            if (features.Count == 0)
            {
                AddStatLabel(_portraitFeatures, "None", new Color(0.5f, 0.5f, 0.5f));
                return;
            }

            foreach (var feature in features)
            {
                AddStatLabel(_portraitFeatures, feature.Name ?? "Unnamed Feature", Colors.White);
            }

            if (rc.Features.Count > 10)
            {
                AddStatLabel(_portraitFeatures, $"... and {rc.Features.Count - 10} more", new Color(0.6f, 0.6f, 0.6f));
            }
        }

        private void UpdateResources(Combatant combatant)
        {
            ClearStatSection(_portraitResources);
            var rc = combatant.ResolvedCharacter;
            if (rc == null || rc.Resources == null) return;

            if (rc.Resources.Count == 0)
            {
                AddStatLabel(_portraitResources, "None", new Color(0.5f, 0.5f, 0.5f));
                return;
            }

            foreach (var (resourceName, maxValue) in rc.Resources)
            {
                // Show max value (current tracking would require additional state)
                AddStatLabel(_portraitResources, $"{resourceName}: {maxValue} max", Colors.White);
            }
        }

        private void ClearStatSection(VBoxContainer container)
        {
            if (container == null) return;
            foreach (var child in container.GetChildren())
            {
                child.QueueFree();
            }
        }

        private void AddStatLabel(VBoxContainer parent, string text, Color color)
        {
            if (parent == null) return;
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", color);
            label.HorizontalAlignment = HorizontalAlignment.Left;
            parent.AddChild(label);
        }

        private string FormatSkillName(QDND.Data.CharacterModel.Skill skill)
        {
            // Convert enum to readable name (e.g., SleightOfHand -> "Sleight of Hand")
            string name = skill.ToString();
            return System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
        }

        private void SetupEndTurnButton()
        {
            // End Turn Button - elegant gold-accented button below hotbar
            _endTurnButton = new Button();
            _endTurnButton.AnchorLeft = 0.5f;
            _endTurnButton.AnchorRight = 0.5f;
            _endTurnButton.AnchorTop = 1.0f;
            _endTurnButton.AnchorBottom = 1.0f;
            // Center below hotbar (hotbar ends at -44 from bottom)
            _endTurnButton.OffsetLeft = -70;   // Center 140px width
            _endTurnButton.OffsetRight = 70;
            _endTurnButton.OffsetTop = -38;    // 6px below hotbar bottom edge
            _endTurnButton.OffsetBottom = 6;    // Extends slightly past bottom (clipped) - or sits at bottom
            _endTurnButton.CustomMinimumSize = new Vector2(140, 48);
            _endTurnButton.Text = "END TURN";
            _endTurnButton.MouseFilter = MouseFilterEnum.Stop;
            
            // Gold-accented button style
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(160f/255f, 130f/255f, 60f/255f, 0.85f);  // Muted gold background
            normalStyle.SetCornerRadiusAll(6);
            normalStyle.SetBorderWidthAll(2);
            normalStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f);  // Gold border
            _endTurnButton.AddThemeStyleboxOverride("normal", normalStyle);
            _endTurnButton.AddThemeColorOverride("font_color", new Color(232f/255f, 224f/255f, 208f/255f));  // Warm white
            _endTurnButton.AddThemeFontSizeOverride("font_size", 14);

            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(180f/255f, 150f/255f, 70f/255f, 0.95f);  // Brighter gold
            hoverStyle.SetCornerRadiusAll(6);
            hoverStyle.SetBorderWidthAll(2);
            hoverStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f);
            _endTurnButton.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = new StyleBoxFlat();
            pressedStyle.BgColor = new Color(120f/255f, 100f/255f, 45f/255f, 0.9f);  // Darker gold
            pressedStyle.SetCornerRadiusAll(6);
            pressedStyle.SetBorderWidthAll(2);
            pressedStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f);
            _endTurnButton.AddThemeStyleboxOverride("pressed", pressedStyle);

            var disabledStyle = new StyleBoxFlat();
            disabledStyle.BgColor = new Color(40f/255f, 38f/255f, 45f/255f, 0.7f);  // Dark gray
            disabledStyle.SetCornerRadiusAll(6);
            disabledStyle.SetBorderWidthAll(1);
            disabledStyle.BorderColor = new Color(100f/255f, 100f/255f, 100f/255f, 0.3f);
            _endTurnButton.AddThemeStyleboxOverride("disabled", disabledStyle);
            _endTurnButton.AddThemeColorOverride("font_disabled_color", new Color(0.5f, 0.5f, 0.5f));

            _endTurnButton.Pressed += OnEndTurnPressed;
            AddChild(_endTurnButton);

            if (DebugUI)
            {
                GD.Print("[CombatHUD] End Turn button created");
                CallDeferred(nameof(DebugEndTurnButton));
            }
        }

        private void DebugEndTurnButton()
        {
            // Guard against running after cleanup/disposal
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (_endTurnButton != null && DebugUI)
            {
                GD.Print($"[CombatHUD] END TURN Debug:");
                GD.Print($"  Position: {_endTurnButton.Position}");
                GD.Print($"  Size: {_endTurnButton.Size}");  
                GD.Print($"  GlobalPosition: {_endTurnButton.GlobalPosition}");
                GD.Print($"  Visible: {_endTurnButton.Visible}");
                GD.Print($"  IsInsideTree: {_endTurnButton.IsInsideTree()}");
                GD.Print($"  Anchors: L={_endTurnButton.AnchorLeft}, R={_endTurnButton.AnchorRight}, T={_endTurnButton.AnchorTop}, B={_endTurnButton.AnchorBottom}");
                GD.Print($"  Offsets: L={_endTurnButton.OffsetLeft}, R={_endTurnButton.OffsetRight}, T={_endTurnButton.OffsetTop}, B={_endTurnButton.OffsetBottom}");
            }
        }

        private Color GetHealthColor(float hpPercent)
        {
            if (hpPercent > 50)
                return new Color(0.318f, 0.8f, 0.318f);  // Green #51CF66
            else if (hpPercent > 25)
                return new Color(0.902f, 0.831f, 0.263f); // Yellow #FF943B/E6D443
            else
                return new Color(0.902f, 0.263f, 0.263f); // Red #FF6B6B
        }

        private void CreateResourceDisplay(string id, string label, Color color, int current, int max)
        {
            // Compact resource slots
            var container = new VBoxContainer();
            container.CustomMinimumSize = new Vector2(52, 32);
            container.AddThemeConstantOverride("separation", 1);
            _resourceBar.AddChild(container);

            // Label at top - muted gold
            var labelNode = new Label();
            labelNode.Text = label;
            labelNode.HorizontalAlignment = HorizontalAlignment.Center;
            labelNode.AddThemeFontSizeOverride("font_size", 9);
            labelNode.AddThemeColorOverride("font_color", new Color(160f/255f, 136f/255f, 72f/255f));  // Muted gold
            container.AddChild(labelNode);

            // Smaller progress bar
            var bar = new ProgressBar();
            bar.CustomMinimumSize = new Vector2(48, 12);
            bar.Value = max > 0 ? (float)current / max * 100 : 0;
            bar.ShowPercentage = false;

            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);  // Subtle dark background
            bgStyle.SetCornerRadiusAll(2);
            bar.AddThemeStyleboxOverride("background", bgStyle);

            var fillStyle = new StyleBoxFlat();
            fillStyle.BgColor = color;
            fillStyle.SetCornerRadiusAll(2);
            bar.AddThemeStyleboxOverride("fill", fillStyle);

            container.AddChild(bar);
            _resourceBars[id] = bar;

            // Value label - warm white, smaller
            var valueLabel = new Label();
            valueLabel.Text = $"{current}/{max}";
            valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
            valueLabel.AddThemeFontSizeOverride("font_size", 11);
            valueLabel.AddThemeColorOverride("font_color", new Color(232f/255f, 224f/255f, 208f/255f));
            container.AddChild(valueLabel);
            _resourceLabels[id] = valueLabel;
        }

        private void SetupCombatLog()
        {
            _logLines.Clear();

            // Combat log - narrower, elegant scroll panel
            _logPanel = new PanelContainer();
            _logPanel.AnchorLeft = 1.0f;
            _logPanel.AnchorRight = 1.0f;
            _logPanel.AnchorTop = 0.0f;
            _logPanel.AnchorBottom = 0.0f;
            // Narrower: 280px instead of 312px
            _logPanel.OffsetLeft = -300;   // 20px margin from right
            _logPanel.OffsetRight = -20;
            _logPanel.OffsetTop = 92;      // Below turn tracker
            _logPanel.OffsetBottom = 920;
            _logPanel.CustomMinimumSize = new Vector2(280, 828);

            var logStyle = new StyleBoxFlat();
            logStyle.BgColor = new Color(8f/255f, 6f/255f, 14f/255f, 0.88f);  // Very dark background
            logStyle.SetCornerRadiusAll(10);
            logStyle.SetBorderWidthAll(1);
            logStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.15f);  // Subtle gold border
            _logPanel.AddThemeStyleboxOverride("panel", logStyle);
            AddChild(_logPanel);

            var logVBox = new VBoxContainer();
            logVBox.AddThemeConstantOverride("separation", 0);
            _logPanel.AddChild(logVBox);

            // Header with gold accent
            var headerPanel = new PanelContainer();
            headerPanel.CustomMinimumSize = new Vector2(0, 36);
            var headerStyle = new StyleBoxFlat();
            headerStyle.BgColor = new Color(18f/255f, 15f/255f, 25f/255f, 0.95f);  // Slightly lighter
            headerStyle.SetCornerRadiusAll(0);
            headerStyle.SetBorderWidthAll(0);
            headerStyle.BorderBlend = true;
            // Bottom border for header separation
            headerStyle.BorderWidthBottom = 1;
            headerStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.2f);
            headerPanel.AddThemeStyleboxOverride("panel", headerStyle);
            logVBox.AddChild(headerPanel);

            var logHeader = new Label();
            logHeader.Text = "COMBAT LOG";
            logHeader.HorizontalAlignment = HorizontalAlignment.Left;
            logHeader.VerticalAlignment = VerticalAlignment.Center;
            logHeader.AddThemeFontSizeOverride("font_size", 13);
            logHeader.AddThemeColorOverride("font_color", new Color(200f/255f, 168f/255f, 78f/255f));  // Gold text
            logHeader.CustomMinimumSize = new Vector2(0, 36);
            // Add left padding via margin
            var headerMargin = new MarginContainer();
            headerMargin.AddThemeConstantOverride("margin_left", 12);
            headerMargin.AddChild(logHeader);
            headerPanel.AddChild(headerMargin);

            // Scrollable log entries
            _logScroll = new ScrollContainer();
            _logScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _logScroll.Set("horizontal_scroll_mode", 0); // Disabled
            logVBox.AddChild(_logScroll);

            _logContainer = new VBoxContainer();
            _logContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _logContainer.AddThemeConstantOverride("separation", 4);
            _logScroll.AddChild(_logContainer);

            _logText = new RichTextLabel();
            _logText.BbcodeEnabled = true;
            _logText.ScrollActive = false;
            _logText.SelectionEnabled = false;
            _logText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _logText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _logText.SizeFlagsVertical = SizeFlags.ExpandFill;
            _logText.FitContent = true;
            _logText.AddThemeFontSizeOverride("normal_font_size", 11);  // Smaller font
            _logText.AddThemeFontSizeOverride("bold_font_size", 12);
            _logContainer.AddChild(_logText);

            if (DebugUI)
                GD.Print("[CombatHUD] Combat log panel created");
        }

        private Button CreateAbilityButton(int index)
        {
            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(52, 52);
            btn.Text = "";
            btn.TooltipText = "Empty slot";
            btn.Disabled = true;
            btn.MouseFilter = MouseFilterEnum.Stop;
            btn.ClipText = true;

            // Normal style - dark with subtle gold border
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(28f/255f, 24f/255f, 36f/255f, 0.9f);
            normalStyle.SetCornerRadiusAll(6);
            normalStyle.SetBorderWidthAll(1);
            normalStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.2f);
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeColorOverride("font_color", new Color(232f/255f, 224f/255f, 208f/255f));
            btn.AddThemeFontSizeOverride("font_size", 9);

            // Disabled style
            var disabledStyle = new StyleBoxFlat();
            disabledStyle.BgColor = new Color(15f/255f, 12f/255f, 20f/255f, 0.6f);
            disabledStyle.SetCornerRadiusAll(6);
            disabledStyle.SetBorderWidthAll(1);
            disabledStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.1f);
            btn.AddThemeStyleboxOverride("disabled", disabledStyle);
            btn.AddThemeColorOverride("font_disabled_color", new Color(112f/255f, 104f/255f, 88f/255f));

            // Hover style
            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(32f/255f, 28f/255f, 40f/255f, 0.95f);
            hoverStyle.SetCornerRadiusAll(6);
            hoverStyle.SetBorderWidthAll(2);
            hoverStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            // Pressed style
            var pressedStyle = new StyleBoxFlat();
            pressedStyle.BgColor = new Color(48f/255f, 42f/255f, 30f/255f, 0.9f);
            pressedStyle.SetCornerRadiusAll(6);
            pressedStyle.SetBorderWidthAll(2);
            pressedStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f);
            btn.AddThemeStyleboxOverride("pressed", pressedStyle);

            // Icon texture centered in button
            var iconRect = new TextureRect();
            iconRect.Name = "IconRect";
            iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconRect.CustomMinimumSize = new Vector2(36, 36);
            iconRect.AnchorLeft = 0.5f;
            iconRect.AnchorRight = 0.5f;
            iconRect.AnchorTop = 0.0f;
            iconRect.AnchorBottom = 0.0f;
            iconRect.OffsetLeft = -18;
            iconRect.OffsetRight = 18;
            iconRect.OffsetTop = 2;
            iconRect.OffsetBottom = 38;
            iconRect.MouseFilter = MouseFilterEnum.Ignore;
            btn.AddChild(iconRect);

            // Hotkey label at top-left corner
            var hotkeyLabel = new Label();
            hotkeyLabel.Name = "HotkeyLabel";
            hotkeyLabel.Text = "";
            hotkeyLabel.AddThemeFontSizeOverride("font_size", 8);
            hotkeyLabel.AddThemeColorOverride("font_color", new Color(200f/255f, 168f/255f, 78f/255f, 0.7f));
            hotkeyLabel.AnchorLeft = 0;
            hotkeyLabel.AnchorTop = 0;
            hotkeyLabel.OffsetLeft = 3;
            hotkeyLabel.OffsetTop = 1;
            hotkeyLabel.MouseFilter = MouseFilterEnum.Ignore;
            btn.AddChild(hotkeyLabel);

            // Short name label at bottom
            var nameLabel = new Label();
            nameLabel.Name = "NameLabel";
            nameLabel.Text = "";
            nameLabel.AddThemeFontSizeOverride("font_size", 7);
            nameLabel.AddThemeColorOverride("font_color", new Color(232f/255f, 224f/255f, 208f/255f, 0.9f));
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AnchorLeft = 0;
            nameLabel.AnchorRight = 1;
            nameLabel.AnchorTop = 1;
            nameLabel.AnchorBottom = 1;
            nameLabel.OffsetLeft = 0;
            nameLabel.OffsetRight = 0;
            nameLabel.OffsetTop = -14;
            nameLabel.OffsetBottom = -1;
            nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            nameLabel.ClipText = true;
            btn.AddChild(nameLabel);

            int capturedIndex = index;
            btn.Pressed += () => OnAbilityPressed(capturedIndex);
            btn.MouseEntered += () => OnAbilityHovered(capturedIndex);
            btn.MouseExited += () => OnAbilityHoverExit();
            AddActionCostContainer(btn);

            return btn;
        }

        private void OnStateChanged(StateTransitionEvent evt)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            // Show/hide action bar based on state
            if (_bottomBar != null && IsInstanceValid(_bottomBar))
            {
                _bottomBar.Visible = evt.ToState == CombatState.PlayerDecision;
            }

            if (_endTurnButton != null && IsInstanceValid(_endTurnButton))
            {
                _endTurnButton.Disabled = evt.ToState != CombatState.PlayerDecision;
            }

            // Show/hide resource bar based on state
            if (_resourceBar != null && IsInstanceValid(_resourceBar))
            {
                _resourceBar.Visible = evt.ToState == CombatState.PlayerDecision;
            }
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            // Refresh the turn tracker to update active highlighting
            if (Arena != null)
            {
                var combatants = Arena.GetCombatants();
                RefreshTurnTracker(combatants, evt.CurrentCombatant?.Id);
            }

            // Update abilities for current combatant
            if (evt.CurrentCombatant != null && evt.CurrentCombatant.IsPlayerControlled)
            {
                UpdateAbilityButtons(evt.CurrentCombatant.Id);

                // Default full resources at turn start
                int maxMove = Mathf.RoundToInt(evt.CurrentCombatant.ActionBudget?.MaxMovement ?? GetDefaultMovePoints());
                int remainingMove = Mathf.RoundToInt(evt.CurrentCombatant.ActionBudget?.RemainingMovement ?? GetDefaultMovePoints());
                UpdateResources(1, 1, 1, 1, remainingMove, maxMove, 1, 1);
            }
        }

        private int GetDefaultMovePoints()
        {
            return Mathf.RoundToInt(Arena?.DefaultMovePoints ?? 10f);
        }

        public void UpdateResources(int action, int maxAction, int bonus, int maxBonus, int move, int maxMove, int reaction, int maxReaction)
        {
            UpdateResourceBar("action", action, maxAction);
            UpdateResourceBar("bonus_action", bonus, maxBonus);
            UpdateResourceBar("move", move, maxMove);
            UpdateResourceBar("reaction", reaction, maxReaction);
        }

        private void UpdateResourceBar(string id, int current, int max)
        {
            if (_disposed || !IsInstanceValid(this)) return;

            if (_resourceBars.TryGetValue(id, out var bar) && IsInstanceValid(bar))
            {
                bar.Value = max > 0 ? (float)current / max * 100 : 0;

                // Color based on depleted state
                var style = bar.GetThemeStylebox("fill") as StyleBoxFlat;
                if (style != null && current <= 0)
                {
                    // Create new style for depleted state
                    var depletedStyle = new StyleBoxFlat();
                    depletedStyle.BgColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                    bar.AddThemeStyleboxOverride("fill", depletedStyle);
                }
            }

            if (_resourceLabels.TryGetValue(id, out var label) && IsInstanceValid(label))
            {
                label.Text = $"{current}/{max}";
                label.Modulate = current <= 0 ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
            }
        }

        private void OnTurnOrderChanged()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (Arena?.TurnTrackerModel == null) return;

            // Rebuild turn tracker from model
            var entries = Arena.TurnTrackerModel.Entries;
            RefreshTurnTrackerFromModel(entries, Arena.TurnTrackerModel.ActiveCombatantId);
        }

        private void OnActiveCombatantChanged(string combatantId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            // Update highlighting in turn tracker
            foreach (var kvp in _turnPortraits)
            {
                bool isActive = kvp.Key == combatantId;
                var panel = kvp.Value as PanelContainer;
                if (panel != null && IsInstanceValid(panel))
                {
                    var styleBox = new StyleBoxFlat();
                    styleBox.BgColor = isActive ? new Color(0.2f, 0.8f, 0.3f, 0.8f) : new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    styleBox.SetCornerRadiusAll(5);
                    panel.AddThemeStyleboxOverride("panel", styleBox);
                }
            }
        }

        private void OnTurnEntryUpdated(string combatantId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            var entry = Arena?.TurnTrackerModel?.GetEntry(combatantId);
            if (entry == null || !_turnPortraits.TryGetValue(combatantId, out var portrait)) return;

            if (!IsInstanceValid(portrait)) return;

            // Update HP display in portrait
            // Find HP label in portrait
            var vbox = portrait.GetChild(0) as VBoxContainer;
            if (vbox != null && IsInstanceValid(vbox) && vbox.GetChildCount() >= 3)
            {
                var hpLabel = vbox.GetChild(2) as Label;
                if (hpLabel != null && IsInstanceValid(hpLabel))
                {
                    int hp = (int)(entry.HpPercent * 100);
                    hpLabel.Text = $"{hp}%";
                    hpLabel.Modulate = entry.IsDead ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
                }
            }
        }

        private void OnResourceChanged(string resourceId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            var resource = Arena?.ResourceBarModel?.GetResource(resourceId);
            if (resource == null) return;

            UpdateResourceBar(resourceId, resource.Current, resource.Maximum);
        }

        private void OnHealthChanged(int current, int max, int temp)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            UpdateResourceBar("health", current, max);
        }

        /// <summary>
        /// Common action IDs (BG3 common actions) - used for categorization in the grid.
        /// </summary>
        private static readonly HashSet<string> CommonActionIdSet = new()
        {
            // BG3 prefixed IDs (canonical)
            "Target_MainHandAttack", "Projectile_MainHandAttack", "Target_UnarmedStrike",
            "Target_OffhandAttack", "Shout_Dash", "Shout_Disengage", "Shout_Dodge",
            "Shout_Hide", "Target_Shove", "Target_Help", "Throw_Throw", "Shout_Jump",
            "Target_Dip",
            // Legacy unprefixed (for actions loaded from JSON files)
            "main_hand_attack", "ranged_attack", "unarmed_strike", "offhand_attack",
            "dash", "disengage", "dodge_action", "hide",
            "shove", "help", "throw", "jump", "dip"
        };

        private void OnActionsChanged()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (Arena?.ActionBarModel == null) return;

            var actions = Arena.ActionBarModel.Actions.ToList();

            // Split actions into class abilities and common actions
            var classActions = new List<ActionBarEntry>();
            var commonActions = new List<ActionBarEntry>();
            foreach (var action in actions)
            {
                if (CommonActionIdSet.Contains(action.ActionId))
                    commonActions.Add(action);
                else
                    classActions.Add(action);
            }

            // Clear existing buttons and rows
            _abilityButtons.Clear();
            if (_actionBarGrid != null && IsInstanceValid(_actionBarGrid))
            {
                foreach (var child in _actionBarGrid.GetChildren())
                {
                    child.QueueFree();
                }
            }

            // Row 1: Class/specific abilities (if any)
            if (classActions.Count > 0)
            {
                var classRow = new HBoxContainer();
                classRow.AddThemeConstantOverride("separation", 3);
                classRow.Alignment = BoxContainer.AlignmentMode.Center;
                _actionBarGrid.AddChild(classRow);

                for (int i = 0; i < classActions.Count; i++)
                {
                    int globalIdx = i;
                    var btn = CreateAbilityButton(globalIdx);
                    classRow.AddChild(btn);
                    _abilityButtons.Add(btn);
                    PopulateAbilityButton(btn, classActions[i], globalIdx);
                }

                // Add separator line
                var sep = new HSeparator();
                sep.AddThemeConstantOverride("separation", 2);
                sep.AddThemeStyleboxOverride("separator", CreateSeparatorStyle());
                _actionBarGrid.AddChild(sep);
            }

            // Row 2: Common actions
            if (commonActions.Count > 0)
            {
                var commonRow = new HBoxContainer();
                commonRow.AddThemeConstantOverride("separation", 3);
                commonRow.Alignment = BoxContainer.AlignmentMode.Center;
                _actionBarGrid.AddChild(commonRow);

                int startIdx = classActions.Count;
                for (int i = 0; i < commonActions.Count; i++)
                {
                    int globalIdx = startIdx + i;
                    var btn = CreateAbilityButton(globalIdx);
                    commonRow.AddChild(btn);
                    _abilityButtons.Add(btn);
                    PopulateAbilityButton(btn, commonActions[i], globalIdx);
                }
            }

            // If no actions at all, show empty message
            if (actions.Count == 0)
            {
                _actionBar = new HBoxContainer();
                _actionBar.Alignment = BoxContainer.AlignmentMode.Center;
                _actionBarGrid.AddChild(_actionBar);
            }
        }

        private StyleBoxFlat CreateSeparatorStyle()
        {
            var sepStyle = new StyleBoxFlat();
            sepStyle.BgColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.15f);
            sepStyle.ContentMarginTop = 1;
            sepStyle.ContentMarginBottom = 1;
            return sepStyle;
        }

        private void PopulateAbilityButton(Button btn, ActionBarEntry action, int index)
        {
            if (btn == null || !IsInstanceValid(btn)) return;

            // Set button text to empty since we use icon + labels
            btn.Text = "";
            btn.TooltipText = ""; // We use custom tooltip panel
            btn.Disabled = !action.IsAvailable;

            // Set icon
            var iconRect = btn.GetNodeOrNull<TextureRect>("IconRect");
            if (iconRect != null && !string.IsNullOrEmpty(action.IconPath) && action.IconPath.StartsWith("res://"))
            {
                var texture = ResourceLoader.Exists(action.IconPath)
                    ? ResourceLoader.Load<Texture2D>(action.IconPath)
                    : null;
                if (texture != null)
                {
                    iconRect.Texture = texture;
                    iconRect.Visible = true;
                }
                else
                {
                    iconRect.Visible = false;
                }
            }
            else
            {
                if (iconRect != null) iconRect.Visible = false;
            }

            // Set hotkey label
            var hotkeyLabel = btn.GetNodeOrNull<Label>("HotkeyLabel");
            if (hotkeyLabel != null)
            {
                hotkeyLabel.Text = (index + 1) <= 12 ? (index + 1).ToString() : "";
            }

            // Set short name
            var nameLabel = btn.GetNodeOrNull<Label>("NameLabel");
            if (nameLabel != null)
            {
                // Show icon fallback text if no icon
                bool hasIcon = iconRect?.Visible == true;
                nameLabel.Text = hasIcon ? "" : TruncateName(action.DisplayName, 7);
            }

            // If no icon available, show name as button text
            if (iconRect?.Visible != true)
            {
                btn.Text = TruncateName(action.DisplayName, 6);
            }

            // Cost badges
            int reactionCost = action.ResourceCosts != null && action.ResourceCosts.TryGetValue("reaction", out var rxnCost)
                ? rxnCost : 0;
            UpdateActionCostBadges(btn, action.ActionPointCost, action.BonusActionCost, action.MovementCost, reactionCost);
        }

        private static string TruncateName(string name, int maxLen)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Length <= maxLen ? name : name.Substring(0, maxLen - 1) + "…";
        }

        private void OnActionUpdated(string actionId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (Arena?.ActionBarModel == null) return;

            var actions = Arena.ActionBarModel.Actions.ToList();
            for (int i = 0; i < actions.Count && i < _abilityButtons.Count; i++)
            {
                if (actions[i].ActionId == actionId)
                {
                    var action = actions[i];
                    var btn = _abilityButtons[i];
                    if (btn != null && IsInstanceValid(btn))
                    {
                        btn.Disabled = !action.IsAvailable;
                        int reactionCost = action.ResourceCosts != null && action.ResourceCosts.TryGetValue("reaction", out var rxnCost)
                            ? rxnCost
                            : 0;
                        UpdateActionCostBadges(btn, action.ActionPointCost, action.BonusActionCost, action.MovementCost, reactionCost);

                        // Update the visual cooldown state
                        if (action.HasCooldown)
                        {
                            btn.Modulate = new Color(0.6f, 0.6f, 0.6f);
                        }
                        else
                        {
                            btn.Modulate = Colors.White;
                        }
                    }
                    break;
                }
            }
        }

        private void RefreshTurnTrackerFromModel(IReadOnlyList<TurnTrackerEntry> entries, string activeId)
        {
            // Clear existing
            foreach (var child in _turnTracker.GetChildren())
            {
                child.QueueFree();
            }
            _turnPortraits.Clear();

            // Create portrait for each entry
            foreach (var entry in entries.OrderByDescending(e => e.Initiative))
            {
                var portrait = CreateTurnPortraitFromEntry(entry, entry.CombatantId == activeId);
                _turnTracker.AddChild(portrait);
                _turnPortraits[entry.CombatantId] = portrait;
            }
        }

        private Control CreateTurnPortraitFromEntry(TurnTrackerEntry entry, bool isActive)
        {
            var panel = new PanelContainer();
            // Active gets larger size for prominence
            panel.CustomMinimumSize = isActive ? new Vector2(80, 80) : new Vector2(72, 72);

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(18f/255f, 15f/255f, 25f/255f, 0.95f);  // Dark background
            styleBox.SetCornerRadiusAll(8);
            
            // Faction colors with BG3 palette
            var factionColor = entry.IsPlayer 
                ? new Color(0.4f, 0.68f, 0.97f)   // Blue tint
                : new Color(0.91f, 0.42f, 0.42f);  // Red tint
            
            // Active: thick gold border with glow effect
            styleBox.SetBorderWidthAll(isActive ? 3 : 1);
            styleBox.BorderColor = isActive 
                ? new Color(200f/255f, 168f/255f, 78f/255f, 0.8f)  // Gold with opacity
                : new Color(200f/255f, 168f/255f, 78f/255f, 0.15f);  // Subtle border

            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);

            // Initiative badge - top-left corner
            var initLabel = new Label();
            initLabel.Text = entry.Initiative.ToString();
            initLabel.HorizontalAlignment = HorizontalAlignment.Center;
            initLabel.VerticalAlignment = VerticalAlignment.Center;
            initLabel.AddThemeFontSizeOverride("font_size", 10);
            initLabel.AddThemeColorOverride("font_color", new Color(200f/255f, 168f/255f, 78f/255f));  // Gold
            vbox.AddChild(initLabel);

            var spacer = new Control();
            spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(spacer);

            // Name - center
            var nameLabel = new Label();
            var displayName = entry.DisplayName.Length > 10 ? entry.DisplayName.Substring(0, 10) : entry.DisplayName;
            nameLabel.Text = displayName;
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", isActive ? 12 : 11);
            nameLabel.AddThemeColorOverride("font_color", entry.IsDead 
                ? new Color(0.5f, 0.5f, 0.5f) 
                : new Color(232f/255f, 224f/255f, 208f/255f));  // Warm white
            vbox.AddChild(nameLabel);

            // HP Bar - full width at bottom, rounded
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(0, 4);
            hpBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hpBar.ShowPercentage = false;
            hpBar.Value = entry.HpPercent * 100;
            
            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.15f, 0.1f, 0.1f, 0.8f);
            bgStyle.SetCornerRadiusAll(2);
            var fillStyle = new StyleBoxFlat();
            // Health-based colors
            fillStyle.BgColor = entry.HpPercent > 0.5f 
                ? new Color(0.318f, 0.812f, 0.4f)   // Green
                : entry.HpPercent > 0.25f 
                    ? new Color(1.0f, 0.831f, 0.231f)  // Yellow
                    : new Color(0.91f, 0.42f, 0.42f);  // Red
            fillStyle.SetCornerRadiusAll(2);
            hpBar.AddThemeStyleboxOverride("background", bgStyle);
            hpBar.AddThemeStyleboxOverride("fill", fillStyle);
            vbox.AddChild(hpBar);

            return panel;
        }

        public void RefreshTurnTracker(IEnumerable<Combatant> combatants, string activeId)
        {
            // Clear existing
            foreach (var child in _turnTracker.GetChildren())
            {
                child.QueueFree();
            }
            _turnPortraits.Clear();

            // Create portrait for each combatant
            foreach (var c in combatants.OrderByDescending(c => c.Initiative))
            {
                var portrait = CreateTurnPortrait(c, c.Id == activeId);
                _turnTracker.AddChild(portrait);
                _turnPortraits[c.Id] = portrait;
            }
        }

        private Control CreateTurnPortrait(Combatant c, bool isActive)
        {
            var panel = new PanelContainer();
            // Active gets larger for emphasis
            panel.CustomMinimumSize = isActive ? new Vector2(80, 80) : new Vector2(72, 72);

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(18f/255f, 15f/255f, 25f/255f, 0.95f);  // Dark background
            styleBox.SetCornerRadiusAll(8);
            
            // Faction edge tint
            var factionColor = c.Faction == Faction.Player 
                ? new Color(0.4f, 0.68f, 0.97f)   // Blue
                : new Color(0.91f, 0.42f, 0.42f);  // Red
            
            styleBox.SetBorderWidthAll(isActive ? 3 : 1);
            styleBox.BorderColor = isActive 
                ? new Color(200f/255f, 168f/255f, 78f/255f, 0.8f)  // Bright gold
                : new Color(200f/255f, 168f/255f, 78f/255f, 0.15f);  // Subtle

            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);

            // Initiative badge - top center
            var initLabel = new Label();
            initLabel.Text = c.Initiative.ToString();
            initLabel.HorizontalAlignment = HorizontalAlignment.Center;
            initLabel.VerticalAlignment = VerticalAlignment.Center;
            initLabel.AddThemeFontSizeOverride("font_size", 10);
            initLabel.AddThemeColorOverride("font_color", new Color(200f/255f, 168f/255f, 78f/255f));  // Gold
            vbox.AddChild(initLabel);

            var spacer = new Control();
            spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(spacer);

            // Name - centered
            var nameLabel = new Label();
            var displayName = c.Name.Length > 10 ? c.Name.Substring(0, 10) : c.Name;
            nameLabel.Text = displayName;
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.AddThemeFontSizeOverride("font_size", isActive ? 12 : 11);
            nameLabel.AddThemeColorOverride("font_color", new Color(232f/255f, 224f/255f, 208f/255f));  // Warm white
            vbox.AddChild(nameLabel);

            // HP Bar - thin, full width at bottom
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(0, 4);
            hpBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hpBar.ShowPercentage = false;
            var hpPercent = c.Resources.MaxHP > 0 
                ? (float)c.Resources.CurrentHP / c.Resources.MaxHP 
                : 0;
            hpBar.Value = hpPercent * 100;
            
            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.15f, 0.1f, 0.1f, 0.8f);
            bgStyle.SetCornerRadiusAll(2);
            var fillStyle = new StyleBoxFlat();
            // Health-based colors
            fillStyle.BgColor = hpPercent > 0.5f 
                ? new Color(0.318f, 0.812f, 0.4f)   // Green
                : hpPercent > 0.25f 
                    ? new Color(1.0f, 0.831f, 0.231f)  // Yellow
                    : new Color(0.91f, 0.42f, 0.42f);  // Red
            fillStyle.SetCornerRadiusAll(2);
            hpBar.AddThemeStyleboxOverride("background", bgStyle);
            hpBar.AddThemeStyleboxOverride("fill", fillStyle);
            vbox.AddChild(hpBar);

            return panel;
        }

        private void UpdateAbilityButtons(string combatantId)
        {
            // Fallback used when the model hasn't populated yet - does nothing,
            // the model-driven path (OnActionsChanged) handles everything now
            if (Arena?.ActionBarModel != null && Arena.ActionBarModel.Actions.Count > 0)
            {
                OnActionsChanged();
            }
        }

        private void AddActionCostContainer(Button button)
        {
            var row = new HBoxContainer
            {
                Name = "CostBadges",
                MouseFilter = MouseFilterEnum.Ignore
            };
            row.AnchorLeft = 0;
            row.AnchorRight = 1;
            row.AnchorTop = 1;
            row.AnchorBottom = 1;
            row.OffsetLeft = 4;
            row.OffsetRight = -4;
            row.OffsetTop = -18;
            row.OffsetBottom = -2;
            row.Alignment = BoxContainer.AlignmentMode.End;
            row.AddThemeConstantOverride("separation", 2);
            button.AddChild(row);
        }

        private void UpdateActionCostBadges(Button button, int actionCost, int bonusCost, int moveCost, int reactionCost)
        {
            if (button == null || !IsInstanceValid(button))
                return;

            var row = button.GetNodeOrNull<HBoxContainer>("CostBadges");
            if (row == null)
                return;

            foreach (var child in row.GetChildren())
            {
                child.QueueFree();
            }

            AddCostBadge(row, _actionCostColor, actionCost);
            AddCostBadge(row, _bonusCostColor, bonusCost);
            AddCostBadge(row, _moveCostColor, moveCost);
            AddCostBadge(row, _reactionCostColor, reactionCost);

            row.Visible = row.GetChildCount() > 0;
        }

        private void AddCostBadge(HBoxContainer row, Color color, int cost)
        {
            if (row == null || cost <= 0)
                return;

            var badge = new PanelContainer
            {
                MouseFilter = MouseFilterEnum.Ignore,
                CustomMinimumSize = new Vector2(14, 14)
            };

            var style = new StyleBoxFlat
            {
                BgColor = color
            };
            style.SetCornerRadiusAll(7);
            style.SetBorderWidthAll(1);
            style.BorderColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            badge.AddThemeStyleboxOverride("panel", style);

            var center = new CenterContainer
            {
                MouseFilter = MouseFilterEnum.Ignore
            };
            center.SetAnchorsPreset(LayoutPreset.FullRect);
            badge.AddChild(center);

            var label = new Label
            {
                Text = cost.ToString(),
                MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", Colors.Black);
            center.AddChild(label);

            row.AddChild(badge);
        }

        private void UpdateUnitInfo(Combatant c)
        {
            // Unit info is now shown in the inspect panel
            // This method is kept for compatibility but does nothing
        }

        private void OnAbilityPressed(int index)
        {
            if (DebugUI)
                GD.Print($"[CombatHUD] OnAbilityPressed({index}) - Arena: {Arena != null}, IsPlayerTurn: {Arena?.IsPlayerTurn}");

            if (Arena == null || !Arena.IsPlayerTurn) return;

            var abilities = Arena.GetActionsForCombatant(Arena.SelectedCombatantId);
            if (DebugUI)
                GD.Print($"[CombatHUD] Abilities count: {abilities?.Count ?? 0}");

            if (index >= 0 && index < abilities.Count)
            {
                var action = abilities[index];
                
                if (DebugUI)
                    GD.Print($"[CombatHUD] Ability: {action.Id}, Variants: {action.Variants?.Count ?? 0}");

                // Check if ability has variants
                if (action.Variants != null && action.Variants.Count > 0)
                {
                    // Show variant selection popup
                    _pendingVariants = action.Variants;
                    _pendingVariantAbilityIndex = index;
                    
                    _variantPopup.Clear();
                    for (int i = 0; i < action.Variants.Count; i++)
                    {
                        var variant = action.Variants[i];
                        _variantPopup.AddItem(variant.DisplayName ?? variant.VariantId, i);
                    }
                    
                    // Position popup near the clicked button
                    var btn = _abilityButtons[index];
                    var popupPos = btn.GlobalPosition + new Vector2(0, btn.Size.Y);
                    _variantPopup.Position = (Vector2I)popupPos;
                    _variantPopup.Popup();
                    
                    if (DebugUI)
                        GD.Print($"[CombatHUD] Showing variant popup for {action.Id} with {action.Variants.Count} variants");
                }
                else
                {
                    // No variants - select ability directly
                    if (DebugUI)
                        GD.Print($"[CombatHUD] Selecting ability: {action.Id}");

                    Arena.SelectAction(action.Id);

                    // Highlight the selected button
                    for (int i = 0; i < _abilityButtons.Count; i++)
                    {
                        var btn = _abilityButtons[i];
                        btn.Modulate = i == index ? new Color(1.2f, 1.2f, 0.5f) : Colors.White;
                    }
                }
            }
        }

        private void OnVariantSelected(long id)
        {
            if (DebugUI)
                GD.Print($"[CombatHUD] OnVariantSelected({id})");
                
            if (_pendingVariantAbilityIndex < 0 || _pendingVariants == null || id >= _pendingVariants.Count)
                return;

            var variant = _pendingVariants[(int)id];
            var abilities = Arena.GetActionsForCombatant(Arena.SelectedCombatantId);
            
            if (_pendingVariantAbilityIndex >= 0 && _pendingVariantAbilityIndex < abilities.Count)
            {
                var action = abilities[_pendingVariantAbilityIndex];
                
                // Create options with the selected variant
                var options = new ActionExecutionOptions
                {
                    VariantId = variant.VariantId,
                    UpcastLevel = 0
                };
                
                if (DebugUI)
                    GD.Print($"[CombatHUD] Selecting ability {action.Id} with variant {variant.VariantId}");
                
                Arena.SelectAction(action.Id, options);
                
                // Highlight the selected button
                for (int i = 0; i < _abilityButtons.Count; i++)
                {
                    var btn = _abilityButtons[i];
                    btn.Modulate = i == _pendingVariantAbilityIndex ? new Color(1.2f, 1.2f, 0.5f) : Colors.White;
                }
            }
            
            // Clear pending state
            _pendingVariants = null;
            _pendingVariantAbilityIndex = -1;
        }

        private void OnEndTurnPressed()
        {
            if (DebugUI)
                GD.Print("[CombatHUD] OnEndTurnPressed");
            Arena?.EndCurrentTurn();
        }

        private void OnPassTurnPressed()
        {
            if (DebugUI)
                GD.Print("[CombatHUD] OnPassTurnPressed");
            // Pass turn is essentially the same as ending turn without taking an action
            Arena?.EndCurrentTurn();
        }

        private void OnDefendPressed()
        {
            if (DebugUI)
                GD.Print("[CombatHUD] OnDefendPressed → Dodge action");

            // BG3 Dodge: Uses your Action to focus on defense. Until the start
            // of your next turn, attack rolls against you have Disadvantage,
            // and you make Dexterity Saving Throws with Advantage.
            var activeCombatantId = Arena?.ActiveCombatantId;
            if (!string.IsNullOrEmpty(activeCombatantId))
            {
                try
                {
                    Arena?.ExecuteAction(activeCombatantId, "Shout_Dodge");
                    GD.Print($"[CombatHUD] {activeCombatantId} takes the Dodge action");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[CombatHUD] Failed to execute Dodge action: {ex.Message}");
                    // Fallback: apply dodge status directly
                    var statusManager = Arena?.Context?.GetService<QDND.Combat.Statuses.StatusManager>();
                    if (statusManager != null)
                    {
                        statusManager.ApplyStatus("dodging", activeCombatantId, activeCombatantId, duration: 1);
                        GD.Print($"[CombatHUD] {activeCombatantId} takes the Dodge action (fallback)");
                    }
                }
            }

            Arena?.EndCurrentTurn();
        }

        private void OnLogEntryAdded(CombatLogEntry entry)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            AddLogEntry(entry);

            // Auto-scroll to bottom
            if (!_logScrollQueued)
            {
                _logScrollQueued = true;
                CallDeferred(nameof(ScrollLogToBottom));
            }
        }

        private void ScrollLogToBottom()
        {
            _logScrollQueued = false;

            // Guard against running after cleanup/disposal
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (_logScroll != null && IsInstanceValid(_logScroll))
            {
                _logScroll.ScrollVertical = (int)_logScroll.GetVScrollBar().MaxValue;
            }
        }

        private void AddLogEntry(CombatLogEntry entry)
        {
            if (_disposed || !IsInstanceValid(this)) return;
            if (_logText == null || !IsInstanceValid(_logText)) return;

            string formatted = FormatLogEntry(entry);
            if (formatted == null) return; // Skip filtered entries
            
            _logLines.Enqueue(formatted);
            while (_logLines.Count > MaxLogEntries)
            {
                _logLines.Dequeue();
            }

            _logText.Text = string.Join("\n", _logLines);
        }

        private string FormatLogEntry(CombatLogEntry entry)
        {
            if (entry == null)
                return null;

            if (entry.Type == CombatLogEntryType.Debug ||
                entry.Type == CombatLogEntryType.TurnStarted ||
                entry.Type == CombatLogEntryType.TurnEnded ||
                entry.Type == CombatLogEntryType.RoundEnded ||
                entry.Type == CombatLogEntryType.MovementStarted ||
                entry.Type == CombatLogEntryType.MovementCompleted)
            {
                return null;
            }

            string color = entry.Type switch
            {
                CombatLogEntryType.RoundStarted => "#E1C46E",
                CombatLogEntryType.AbilityUsed => "#8AC1E8",
                CombatLogEntryType.AttackResolved => entry.IsMiss ? "#D38B8B" : "#8FCF8F",
                CombatLogEntryType.DamageDealt => "#E86A6A",
                CombatLogEntryType.HealingDone => "#6ABF6A",
                CombatLogEntryType.CombatantDowned => "#FF8B6E",
                CombatLogEntryType.StatusApplied => "#B080D0",
                CombatLogEntryType.StatusRemoved => "#808080",
                CombatLogEntryType.CombatStarted => "#7AAFCF",
                CombatLogEntryType.CombatEnded => "#7AAFCF",
                _ => "#A09888"
            };

            // Clean up messages for display
            string message = entry.Format();
            if (string.IsNullOrEmpty(message))
            {
                message = entry.Message ?? entry.Type.ToString();
            }

            if (entry.Type == CombatLogEntryType.RoundStarted)
            {
                return $"[color={color}][b]=== {message.ToUpperInvariant()} ===[/b][/color]";
            }

            if (entry.Type == CombatLogEntryType.AbilityUsed)
            {
                return $"[color={color}][b]{message}[/b][/color]";
            }

            return $"[color={color}]{message}[/color]";
        }

        public override void _Process(double delta)
        {
            if (Arena == null) return;
            
            // Prefer showing the active combatant (whose turn it is)
            string displayId = Arena.ActiveCombatantId;
            if (string.IsNullOrEmpty(displayId))
                displayId = Arena.SelectedCombatantId;
                
            if (!string.IsNullOrEmpty(displayId))
            {
                var combatant = Arena.Context?.GetCombatant(displayId);
                if (combatant != null)
                {
                    UpdateCharacterPortrait(combatant);
                }
            }
        }

        public void ShowInspect(Combatant combatant)
        {
            if (combatant == null)
            {
                _inspectPanel.Visible = false;
                return;
            }

            _inspectPanel.Visible = true;

            // Name and faction
            _inspectName.Text = combatant.Name;
            _inspectFaction.Text = combatant.Faction.ToString();
            _inspectFaction.Modulate = combatant.Faction == Entities.Faction.Player
                ? new Color(0.3f, 0.6f, 1.0f)
                : new Color(1.0f, 0.4f, 0.4f);

            // HP
            float hpPercent = combatant.Resources.MaxHP > 0
                ? (float)combatant.Resources.CurrentHP / combatant.Resources.MaxHP * 100
                : 0;
            _inspectHpBar.Value = hpPercent;
            _inspectHpText.Text = $"{combatant.Resources.CurrentHP} / {combatant.Resources.MaxHP}";

            // Color HP bar based on health
            var fillStyle = new StyleBoxFlat();
            if (hpPercent < 25)
                fillStyle.BgColor = new Color(0.9f, 0.2f, 0.2f);
            else if (hpPercent < 50)
                fillStyle.BgColor = new Color(0.9f, 0.7f, 0.2f);
            else
                fillStyle.BgColor = new Color(0.2f, 0.8f, 0.2f);
            _inspectHpBar.AddThemeStyleboxOverride("fill", fillStyle);

            // Initiative
            _inspectInitiative.Text = $"Initiative: {combatant.Initiative}";

            // Clear and populate statuses
            foreach (var child in _inspectStatusList.GetChildren())
            {
                child.QueueFree();
            }

            // Get statuses from StatusManager
            if (Arena?.Context != null)
            {
                var statusManager = Arena.Context.GetService<QDND.Combat.Statuses.StatusManager>();
                if (statusManager != null)
                {
                    var statuses = statusManager.GetStatuses(combatant.Id);
                    var visibleStatuses = statuses?
                        .Where(s => StatusPresentationPolicy.ShowInPortraitIndicators(s.Definition))
                        .ToList();

                    if (visibleStatuses != null && visibleStatuses.Count > 0)
                    {
                        foreach (var status in visibleStatuses)
                        {
                            var statusLabel = new Label();
                            string statusName = StatusPresentationPolicy.GetDisplayName(status.Definition);
                            statusLabel.Text = $"• {statusName} ({status.RemainingDuration} turns)";
                            statusLabel.AddThemeFontSizeOverride("font_size", 11);
                            statusLabel.Modulate = new Color(0.8f, 0.6f, 1.0f);
                            _inspectStatusList.AddChild(statusLabel);
                        }
                    }
                    else
                    {
                        var noStatus = new Label();
                        noStatus.Text = "(none)";
                        noStatus.AddThemeFontSizeOverride("font_size", 11);
                        noStatus.Modulate = new Color(0.5f, 0.5f, 0.5f);
                        _inspectStatusList.AddChild(noStatus);
                    }
                }
            }
        }

        public void HideInspect()
        {
            _inspectPanel.Visible = false;
        }
    }
}
