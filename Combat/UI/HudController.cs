using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Actions;
using QDND.Combat.Environment;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;
using QDND.Combat.UI.Panels;
using QDND.Combat.UI.Overlays;
using QDND.Combat.UI.Screens;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;
using QDND.Data.Passives;

namespace QDND.Combat.UI
{
    /// <summary>
    /// Main HUD orchestrator. Replaces the monolithic CombatHUD.
    /// Creates and manages all HUD panels, subscribes to combat events, and
    /// routes updates to the appropriate panels.
    /// </summary>
    public partial class HudController : Control
    {
        [Export] public CombatArena Arena;
        [Export] public bool DebugUI = false;

        // ── Panels ─────────────────────────────────────────────────
        private InitiativeRibbon _initiativeRibbon;
        private PartyPanel _partyPanel;
        private ActionBarPanel _actionBarPanel;
        private ResourceBarPanel _resourceBarPanel;
        private TurnControlsPanel _turnControlsPanel;
        private CombatLogPanel _combatLogPanel;

        // ── Portrait (BG3-style, left of hotbar) ──────────────────
        private Control _portraitContainer;
        private ColorRect _portraitColorRect;
        private TextureRect _portraitTextureRect;
        private Label _portraitHpLabel;

        // ── Concentration indicator above portrait ───────────────
        private Control _concentrationContainer;
        private TextureRect _concentrationIcon;

        // ── Reaction Icons (right side of hotbar) ─────────────────
        private HBoxContainer _reactionIconContainer;

        // ── Overlays ───────────────────────────────────────────────
        private ReactionPromptOverlay _reactionPrompt;
        private CharacterInventoryScreen _characterInventoryScreen;
        private HudWindowManager _windowManager;
        private TurnAnnouncementOverlay _turnAnnouncement;
        private MultiTargetPromptOverlay _multiTargetPrompt;

        // ── Tooltip ────────────────────────────────────────────────
        private FloatingTooltip _unifiedTooltip;
        private PanelContainer _tooltipPanel;
        private TextureRect _tooltipIcon;
        private Label _tooltipName;
        private Label _tooltipCost;
        private RichTextLabel _tooltipDesc;
        private Label _tooltipRange;
        private Label _tooltipDamage;
        private Label _tooltipSave;
        private Label _tooltipSchool;
        private Label _tooltipAoE;
        private Label _tooltipConcentration;
        private bool _tooltipPending;
        private float _tooltipDelayMs;
        private ActionBarEntry _pendingTooltipAction;

        // ── Hit Chance ─────────────────────────────────────────────
        private PanelContainer _hitChancePanel;
        private Label _hitChanceLabel;

        // ── Hover Info Panel ────────────────────────────────────────
        private PanelContainer _hoverInfoPanel;
        private Label _hoverNameLabel;
        private ProgressBar _hoverHpBar;
        private Label _hoverHpText;
        private HBoxContainer _hoverConditionsRow;
        private TextureRect _hoverActionIcon;
        private Label _hoverActionChance;
        private HBoxContainer _hoverTargetingRow;

        // ── AI Action Banner ────────────────────────────────────────
        private AIActionBannerOverlay _aiActionBanner;
        private CommandService _commandService;

        // ── Variant popup ──────────────────────────────────────────
        private PopupMenu _variantPopup;
        private List<ActionVariant> _pendingVariants;
        private string _pendingVariantActionId;

        // ── Upcast popup ────────────────────────────────────────────
        private bool _isUpcastMode;
        private string _pendingUpcastActionId;
        private int _pendingUpcastBaseLevel;

        // ── Service references for cleanup ─────────────────────────
        private CombatStateMachine _stateMachine;
        private TurnQueueService _turnQueue;
        private CombatLog _combatLog;
        private StatusManager _statusManager;
        private ConcentrationSystem _concentrationSystem;
        private bool _statusManagerSubscribed;
        private bool _concentrationSystemSubscribed;
        private bool _turnTrackerSubscribed;
        private bool _resourceModelSubscribed;
        private bool _actionModelSubscribed;
        private bool _combatLogBackfilled;
        private int _syncedCombatLogEntries;

        // ── Layout persistence ─────────────────────────────────────
        private Timer _layoutSaveTimer;
        private Dictionary<string, HudPanel> _panels;

        private bool _disposed;
        private Combatant _trackedSpellSlotCombatant;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore; // Pass clicks through to 3D

            CallDeferred(nameof(DeferredInit));
        }

        public override void _ExitTree()
        {
            _disposed = true;
            UnsubscribeAll();
            base._ExitTree();
        }

        /// <summary>
        /// Public cleanup method for graceful shutdown.
        /// </summary>
        public void Cleanup()
        {
            _disposed = true;
            UnsubscribeAll();
        }

        // ════════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ════════════════════════════════════════════════════════════

        private void DeferredInit()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;

            if (Arena == null)
                Arena = GetTree().Root.FindChild("CombatArena", true, false) as CombatArena;

            // Skip HUD in fast auto-battle mode
            if (Arena != null && Arena.IsAutoBattleMode && !QDND.Tools.DebugFlags.IsFullFidelity)
            {
                _disposed = true;
                return;
            }

            if (DebugUI) GD.Print("[HudController] Initializing...");

            CreatePanels();
            CreateOverlays();
            CreateTooltip();
            _unifiedTooltip = new FloatingTooltip();
            AddChild(_unifiedTooltip);
            CreateVariantPopup();
            CreateHitChanceDisplay();
            CreateHoverInfoPanel();
            InitializeLayoutPersistence(); // Must be after all panels/overlays are created
            SubscribeToEvents();
            InitialSync();
            SyncCharacterSheetForCurrentTurn();

            if (DebugUI) GD.Print("[HudController] Initialization complete.");
        }

        // ── Panel Creation ─────────────────────────────────────────

        private void CreatePanels()
        {
            var screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);

            // ── Hotbar geometry ─────────────────────────────────────
            // 12 cols × 44px + 11 gaps × 3px = 528 + 33 = 561
            const int slotSize = 44;
            const int slotGap = 3;
            const int gridCols = 12;
            float actionGridWidth = gridCols * slotSize + (gridCols - 1) * slotGap;
            float actionBarWidth = actionGridWidth + 24 + 44; // 24 padding + 44 resize buttons
            const float portraitCircleSize = 125;
            const float hotbarGap = 4;

            float hotbarBottom = screenSize.Y - 54;

            // Initiative Ribbon — top center (auto-sizes to portrait count)
            _initiativeRibbon = new InitiativeRibbon();
            AddChild(_initiativeRibbon);
            _initiativeRibbon.Size = new Vector2(400, 100); // Initial size, auto-adjusts
            _initiativeRibbon.SetScreenPosition(new Vector2((screenSize.X - 400) / 2, 12));

            // Party Panel — left side (BG3-style compact portraits)
            _partyPanel = new PartyPanel();
            AddChild(_partyPanel);
            float partyPanelWidth = Mathf.Max(170f, _partyPanel.CustomMinimumSize.X + 12f);
            _partyPanel.Size = new Vector2(partyPanelWidth, 460);
            float partyPanelHeight = _partyPanel.Size.Y;
            _partyPanel.SetScreenPosition(new Vector2(15, (screenSize.Y - partyPanelHeight) / 2));
            _partyPanel.OnMemberClicked += OnPartyMemberClicked;

            // ── Active Character Portrait — left of hotbar (125×125 circle) ─────────
            // Plain Control so children keep their manual position/size (PanelContainer force-fits children).
            _portraitContainer = new Control();
            _portraitContainer.CustomMinimumSize = new Vector2(portraitCircleSize, portraitCircleSize);
            _portraitContainer.Size = new Vector2(portraitCircleSize, portraitCircleSize);
            AddChild(_portraitContainer);

            // Fallback color background — visible only when no portrait texture is loaded
            _portraitColorRect = new ColorRect();
            _portraitColorRect.SetAnchorsPreset(LayoutPreset.FullRect);
            _portraitColorRect.Color = HudTheme.PlayerBlue;
            _portraitColorRect.MouseFilter = MouseFilterEnum.Ignore;
            _portraitContainer.AddChild(_portraitColorRect);

            // Portrait texture — KeepAspectCovered zooms to fill 125×125; circular shader clips to circle
            var circleShader = new Shader();
            circleShader.Code =
@"shader_type canvas_item;
void fragment() {
    vec2 uv = UV - vec2(0.5);
    if (length(uv) > 0.5) discard;
    COLOR = texture(TEXTURE, UV);
}";
            var circleShaderMat = new ShaderMaterial();
            circleShaderMat.Shader = circleShader;
            _portraitTextureRect = new TextureRect();
            _portraitTextureRect.SetAnchorsPreset(LayoutPreset.FullRect);
            _portraitTextureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _portraitTextureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            _portraitTextureRect.Material = circleShaderMat;
            _portraitTextureRect.MouseFilter = MouseFilterEnum.Ignore;
            _portraitContainer.AddChild(_portraitTextureRect);

            // Gold circular border ring — non-interactive overlay drawn on top of portrait
            var portraitBorderRing = new PanelContainer();
            portraitBorderRing.SetAnchorsPreset(LayoutPreset.FullRect);
            portraitBorderRing.MouseFilter = MouseFilterEnum.Ignore;
            portraitBorderRing.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(Colors.Transparent, HudTheme.Gold, cornerRadius: 63, borderWidth: 2, contentMargin: 0));
            _portraitContainer.AddChild(portraitBorderRing);

            // ── Action Bar — bottom center ──────────────────────────
            _actionBarPanel = new ActionBarPanel();
            AddChild(_actionBarPanel);
            float actionBarHeight = _actionBarPanel.CalculateHeight();
            _actionBarPanel.Size = new Vector2(actionBarWidth, actionBarHeight);
            float actionBarX = (screenSize.X - actionBarWidth) / 2;
            float actionBarY = hotbarBottom - actionBarHeight;
            _actionBarPanel.SetScreenPosition(new Vector2(actionBarX, actionBarY));
            _actionBarPanel.OnActionPressed += OnActionPressed;
            _actionBarPanel.OnActionHovered += OnActionHovered;
            _actionBarPanel.OnActionHoverExited += OnActionHoverExited;
            _actionBarPanel.OnActionReordered += OnActionReordered;
            _actionBarPanel.OnGridResized += OnHotbarGridResized;

            // Portrait — left of hotbar, 30px from screen bottom
            float portraitX = actionBarX - hotbarGap - portraitCircleSize;
            float portraitY = screenSize.Y - 30 - portraitCircleSize;
            _portraitContainer.GlobalPosition = new Vector2(portraitX, portraitY);

            // ── Concentration Spell — centered above the portrait circle ────
            const float concIconSize = 40;
            _concentrationContainer = new Control();
            _concentrationContainer.CustomMinimumSize = new Vector2(concIconSize, concIconSize);
            _concentrationContainer.Size = new Vector2(concIconSize, concIconSize);
            _concentrationContainer.Visible = false;
            _concentrationContainer.MouseFilter = MouseFilterEnum.Stop;
            AddChild(_concentrationContainer);

            var concBg = new ColorRect();
            concBg.SetAnchorsPreset(LayoutPreset.FullRect);
            concBg.Color = new Color(0.45f, 0.18f, 0.75f, 0.7f);
            concBg.MouseFilter = MouseFilterEnum.Ignore;
            _concentrationContainer.AddChild(concBg);

            _concentrationIcon = new TextureRect();
            _concentrationIcon.SetAnchorsPreset(LayoutPreset.FullRect);
            _concentrationIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _concentrationIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _concentrationIcon.MouseFilter = MouseFilterEnum.Ignore;
            _concentrationContainer.AddChild(_concentrationIcon);

            var concBorder = new PanelContainer();
            concBorder.SetAnchorsPreset(LayoutPreset.FullRect);
            concBorder.MouseFilter = MouseFilterEnum.Ignore;
            concBorder.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(Colors.Transparent, new Color(0.6f, 0.3f, 1.0f), cornerRadius: 4, borderWidth: 1, contentMargin: 0));
            _concentrationContainer.AddChild(concBorder);

            UpdateConcentrationIndicatorPosition(portraitX, portraitY, portraitCircleSize, concIconSize);

            // ── Resource Bar — centered above hotbar, 4px margin ───
            _resourceBarPanel = new ResourceBarPanel();
            AddChild(_resourceBarPanel);
            float resWidth = Mathf.Max(actionBarWidth, 500);
            _resourceBarPanel.Size = new Vector2(resWidth, 80);
            _resourceBarPanel.SetScreenPosition(new Vector2(
                (screenSize.X - resWidth) / 2, actionBarY - 4 - 80));

            // ── Turn Controls (circular End Turn) — right of hotbar ─
            _turnControlsPanel = new TurnControlsPanel();
            AddChild(_turnControlsPanel);
            _turnControlsPanel.Size = new Vector2(125, 125 + 10);
            float turnX = actionBarX + actionBarWidth + hotbarGap;
            float turnY = screenSize.Y - 30 - (125 + 10);
            _turnControlsPanel.SetScreenPosition(new Vector2(turnX, turnY));
            _turnControlsPanel.OnEndTurnPressed += OnEndTurnPressed;
            _turnControlsPanel.OnActionEditorPressed += OnActionEditorPressed;

            // ── Active Reaction Icons — right of hotbar, 10px above hotbar top ─
            _reactionIconContainer = new HBoxContainer();
            _reactionIconContainer.AddThemeConstantOverride("separation", 2);
            _reactionIconContainer.LayoutDirection = LayoutDirectionEnum.Rtl;
            _reactionIconContainer.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_reactionIconContainer);
            float reactionX = actionBarX + actionBarWidth + 4;
            float reactionY = actionBarY - 10 - 30; // 10px above hotbar top, icons are 30px tall
            _reactionIconContainer.GlobalPosition = new Vector2(reactionX, reactionY);

            // ── Combat Log — right side ─────────────────────────────
            _combatLogPanel = new CombatLogPanel();
            AddChild(_combatLogPanel);
            _combatLogPanel.Size = new Vector2(300, 600);
            _combatLogPanel.SetScreenPosition(new Vector2(screenSize.X - 320, 100));

            // Initialize resource bar with defaults
            int maxMove = Mathf.RoundToInt(Arena?.DefaultMovePoints ?? global::QDND.Combat.Actions.ActionBudget.DefaultMaxMovement);
            _resourceBarPanel.InitializeDefaults(maxMove);
        }

        private void CreateOverlays()
        {
            _windowManager = new HudWindowManager { Name = "WindowManager" };
            AddChild(_windowManager);

            _reactionPrompt = new ReactionPromptOverlay();
            _reactionPrompt.Visible = false;
            _reactionPrompt.OnUseReaction += OnReactionUse;
            _reactionPrompt.OnDeclineReaction += OnReactionDecline;
            _windowManager.AddChild(_reactionPrompt); // Must be in tree for _Ready

            _characterInventoryScreen = new CharacterInventoryScreen();
            _characterInventoryScreen.Visible = false;
            _characterInventoryScreen.OnCloseRequested += OnCharacterInventoryScreenClosed;
            _windowManager.AddChild(_characterInventoryScreen);

            _turnAnnouncement = new TurnAnnouncementOverlay();
            AddChild(_turnAnnouncement);

            _multiTargetPrompt = new MultiTargetPromptOverlay();
            _multiTargetPrompt.Visible = false;
            AddChild(_multiTargetPrompt);
        }

        private void CreateTooltip()
        {
            _tooltipPanel = new PanelContainer();
            _tooltipPanel.Visible = false;
            _tooltipPanel.MouseFilter = MouseFilterEnum.Ignore;
            _tooltipPanel.CustomMinimumSize = new Vector2(280, 100);
            _tooltipPanel.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    new Color(18f / 255f, 14f / 255f, 26f / 255f, 0.96f),
                    HudTheme.PanelBorderBright, 8, 2, 12));
            AddChild(_tooltipPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            vbox.MouseFilter = MouseFilterEnum.Ignore;
            _tooltipPanel.AddChild(vbox);

            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 8);
            header.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(header);

            _tooltipIcon = new TextureRect();
            _tooltipIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _tooltipIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _tooltipIcon.CustomMinimumSize = new Vector2(32, 32);
            _tooltipIcon.MouseFilter = MouseFilterEnum.Ignore;
            header.AddChild(_tooltipIcon);

            var nameCol = new VBoxContainer();
            nameCol.AddThemeConstantOverride("separation", 0);
            nameCol.MouseFilter = MouseFilterEnum.Ignore;
            header.AddChild(nameCol);

            _tooltipName = new Label();
            HudTheme.StyleLabel(_tooltipName, HudTheme.FontMedium, HudTheme.Gold);
            _tooltipName.MouseFilter = MouseFilterEnum.Ignore;
            nameCol.AddChild(_tooltipName);

            _tooltipCost = new Label();
            HudTheme.StyleLabel(_tooltipCost, HudTheme.FontSmall, HudTheme.MutedBeige);
            _tooltipCost.MouseFilter = MouseFilterEnum.Ignore;
            nameCol.AddChild(_tooltipCost);

            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            vbox.AddChild(sep);

            _tooltipDesc = new RichTextLabel();
            _tooltipDesc.BbcodeEnabled = true;
            _tooltipDesc.FitContent = true;
            _tooltipDesc.ScrollActive = false;
            _tooltipDesc.CustomMinimumSize = new Vector2(256, 30);
            _tooltipDesc.AddThemeFontSizeOverride("normal_font_size", HudTheme.FontNormal);
            _tooltipDesc.AddThemeColorOverride("default_color", HudTheme.WarmWhite);
            _tooltipDesc.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(_tooltipDesc);

            _tooltipRange = CreateTooltipInfoLabel(HudTheme.MutedBeige);
            vbox.AddChild(_tooltipRange);
            _tooltipDamage = CreateTooltipInfoLabel(HudTheme.WarmWhite);
            vbox.AddChild(_tooltipDamage);
            _tooltipSave = CreateTooltipInfoLabel(HudTheme.MutedBeige);
            vbox.AddChild(_tooltipSave);
            _tooltipSchool = CreateTooltipInfoLabel(HudTheme.MutedBeige);
            vbox.AddChild(_tooltipSchool);
            _tooltipAoE = CreateTooltipInfoLabel(HudTheme.MutedBeige);
            vbox.AddChild(_tooltipAoE);
            _tooltipConcentration = CreateTooltipInfoLabel(new Color(0.7f, 0.5f, 1.0f));
            vbox.AddChild(_tooltipConcentration);
        }

        private Label CreateTooltipInfoLabel(Color color = default)
        {
            var lbl = new Label();
            HudTheme.StyleLabel(lbl, HudTheme.FontSmall, color.A == 0 ? HudTheme.MutedBeige : color);
            lbl.MouseFilter = MouseFilterEnum.Ignore;
            lbl.Visible = false;
            return lbl;
        }

        private void CreateVariantPopup()
        {
            _variantPopup = new PopupMenu();
            _variantPopup.IdPressed += OnVariantSelected;
            AddChild(_variantPopup);
        }

        private void CreateHitChanceDisplay()
        {
            _hitChancePanel = new PanelContainer();
            _hitChancePanel.Visible = false;
            _hitChancePanel.MouseFilter = MouseFilterEnum.Ignore;
            _hitChancePanel.CustomMinimumSize = new Vector2(100, 36);
            _hitChancePanel.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    new Color(18f / 255f, 14f / 255f, 26f / 255f, 0.92f),
                    HudTheme.PanelBorderBright, 6, 1, 8));
            AddChild(_hitChancePanel);

            _hitChanceLabel = new Label();
            _hitChanceLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_hitChanceLabel, HudTheme.FontMedium, HudTheme.WarmWhite);
            _hitChanceLabel.MouseFilter = MouseFilterEnum.Ignore;
            _hitChancePanel.AddChild(_hitChanceLabel);

            // Position near top-center of screen
            var screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            _hitChancePanel.GlobalPosition = new Vector2(
                (screenSize.X - 100) / 2, screenSize.Y * 0.35f);
        }

        private void CreateHoverInfoPanel()
        {
            _hoverInfoPanel = new PanelContainer();
            _hoverInfoPanel.Visible = false;
            _hoverInfoPanel.MouseFilter = MouseFilterEnum.Ignore;
            _hoverInfoPanel.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    new Color(12f / 255f, 10f / 255f, 18f / 255f, 0.92f),
                    HudTheme.PanelBorderBright, 6, 1, 8));
            AddChild(_hoverInfoPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            vbox.MouseFilter = MouseFilterEnum.Ignore;
            _hoverInfoPanel.AddChild(vbox);

            // Name + Level line
            _hoverNameLabel = new Label();
            _hoverNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_hoverNameLabel, HudTheme.FontMedium, HudTheme.Gold);
            _hoverNameLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(_hoverNameLabel);

            // HP bar container (300px wide)
            var hpContainer = new CenterContainer();
            hpContainer.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(hpContainer);

            var hpStack = new Control();
            hpStack.CustomMinimumSize = new Vector2(300, 20);
            hpStack.MouseFilter = MouseFilterEnum.Ignore;
            hpContainer.AddChild(hpStack);

            _hoverHpBar = new ProgressBar();
            _hoverHpBar.CustomMinimumSize = new Vector2(300, 20);
            _hoverHpBar.SetAnchorsPreset(LayoutPreset.FullRect);
            _hoverHpBar.ShowPercentage = false;
            _hoverHpBar.MaxValue = 100;
            _hoverHpBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            _hoverHpBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(HudTheme.HealthGreen));
            _hoverHpBar.MouseFilter = MouseFilterEnum.Ignore;
            hpStack.AddChild(_hoverHpBar);

            _hoverHpText = new Label();
            _hoverHpText.SetAnchorsPreset(LayoutPreset.FullRect);
            _hoverHpText.HorizontalAlignment = HorizontalAlignment.Center;
            _hoverHpText.VerticalAlignment = VerticalAlignment.Center;
            HudTheme.StyleLabel(_hoverHpText, HudTheme.FontSmall, HudTheme.WarmWhite);
            _hoverHpText.MouseFilter = MouseFilterEnum.Ignore;
            hpStack.AddChild(_hoverHpText);

            // Conditions row
            _hoverConditionsRow = new HBoxContainer();
            _hoverConditionsRow.AddThemeConstantOverride("separation", 6);
            _hoverConditionsRow.Alignment = BoxContainer.AlignmentMode.Center;
            _hoverConditionsRow.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(_hoverConditionsRow);

            // Targeting info row (action icon + success chance)
            _hoverTargetingRow = new HBoxContainer();
            _hoverTargetingRow.AddThemeConstantOverride("separation", 6);
            _hoverTargetingRow.Alignment = BoxContainer.AlignmentMode.Center;
            _hoverTargetingRow.MouseFilter = MouseFilterEnum.Ignore;
            _hoverTargetingRow.Visible = false;
            vbox.AddChild(_hoverTargetingRow);

            _hoverActionIcon = new TextureRect();
            _hoverActionIcon.CustomMinimumSize = new Vector2(24, 24);
            _hoverActionIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _hoverActionIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _hoverActionIcon.MouseFilter = MouseFilterEnum.Ignore;
            _hoverTargetingRow.AddChild(_hoverActionIcon);

            _hoverActionChance = new Label();
            HudTheme.StyleLabel(_hoverActionChance, HudTheme.FontMedium, HudTheme.WarmWhite);
            _hoverActionChance.MouseFilter = MouseFilterEnum.Ignore;
            _hoverTargetingRow.AddChild(_hoverActionChance);
        }

        private void InitializeLayoutPersistence()
        {
            // Build panel dictionary for layout persistence (must include all draggable panels)
            _panels = new Dictionary<string, HudPanel>
            {
                { "initiative_ribbon", _initiativeRibbon },
                { "party_panel", _partyPanel },
                { "action_bar", _actionBarPanel },
                { "resource_bar", _resourceBarPanel },
                { "turn_controls", _turnControlsPanel },
                { "combat_log", _combatLogPanel },
                { "character_inventory", _characterInventoryScreen },
            };

            // Load saved layout from previous session
            var loadedPanelIds = HudLayoutService.LoadLayout(_panels);
            if (loadedPanelIds.Count > 0)
            {
                GD.Print("[HudController] Loaded saved HUD layout.");
            }
            else
            {
                GD.Print("[HudController] Using default HUD layout.");
            }

            // Start auto-save timer (save every 5 seconds)
            _layoutSaveTimer = new Timer();
            _layoutSaveTimer.WaitTime = 5.0f;
            _layoutSaveTimer.Autostart = true;
            _layoutSaveTimer.Timeout += OnLayoutSaveTimer;
            AddChild(_layoutSaveTimer);
            _layoutSaveTimer.Start();

            GD.Print("[HudController] Layout auto-save enabled (every 5 seconds).");
        }

        private void OnLayoutSaveTimer()
        {
            if (_panels != null && !_disposed)
            {
                HudLayoutService.SaveLayout(_panels);
            }
        }

        private void OnCharacterInventoryScreenClosed()
        {
            // Called when the unified screen is closed (fade-out complete)
            if (DebugUI) GD.Print("[HudController] Character/Inventory screen closed.");
        }

        // ════════════════════════════════════════════════════════════
        //  EVENT SUBSCRIPTION
        // ════════════════════════════════════════════════════════════

        private void SubscribeToEvents()
        {
            if (Arena == null) return;

            TryBindServiceEvents();
            TryBindModelEvents();
        }

        private bool TryBindServiceEvents()
        {
            if (Arena?.Context == null)
            {
                return false;
            }

            bool boundNew = false;

            if (_stateMachine == null)
            {
                _stateMachine = Arena.Context.GetService<CombatStateMachine>();
                if (_stateMachine != null)
                {
                    _stateMachine.OnStateChanged += OnStateChanged;
                    boundNew = true;
                }
            }

            if (_turnQueue == null)
            {
                _turnQueue = Arena.Context.GetService<TurnQueueService>();
                if (_turnQueue != null)
                {
                    _turnQueue.OnTurnChanged += OnTurnChanged;
                    boundNew = true;
                }
            }

            if (_combatLog == null)
            {
                _combatLog = Arena.Context.GetService<CombatLog>();
                if (_combatLog != null)
                {
                    _combatLog.OnEntryAdded += OnLogEntryAdded;
                    boundNew = true;
                }
            }

            if (_commandService == null)
            {
                _commandService = Arena.Context.GetService<CommandService>();
                if (_commandService != null)
                {
                    boundNew = true;
                }
            }

            if (_statusManager == null)
            {
                _statusManager = Arena.Context.GetService<StatusManager>();
                if (_statusManager != null)
                {
                    boundNew = true;
                }
            }

            if (_statusManager != null && !_statusManagerSubscribed)
            {
                _statusManager.OnStatusApplied += OnStatusApplied;
                _statusManager.OnStatusRemoved += OnStatusRemoved;
                _statusManagerSubscribed = true;
                boundNew = true;
            }

            if (_concentrationSystem == null)
            {
                _concentrationSystem = Arena.Context.GetService<ConcentrationSystem>();
                if (_concentrationSystem != null)
                {
                    boundNew = true;
                }
            }

            if (_concentrationSystem != null && !_concentrationSystemSubscribed)
            {
                _concentrationSystem.OnConcentrationStarted += OnConcentrationChanged;
                _concentrationSystem.OnConcentrationBroken += OnConcentrationBrokenHandler;
                _concentrationSystemSubscribed = true;
                boundNew = true;
            }

            if (_combatLog != null && !_combatLogBackfilled)
            {
                foreach (var entry in _combatLog.GetRecentEntries(100))
                {
                    if (entry.Type == CombatLogEntryType.Debug || entry.Type == CombatLogEntryType.Error)
                        continue;
                    if (entry.Severity == LogSeverity.Verbose)
                        continue;
                    _combatLogPanel?.AddEntry(entry);
                }
                _syncedCombatLogEntries = _combatLog.Entries.Count;
                _combatLogBackfilled = true;
                boundNew = true;
            }

            return boundNew;
        }

        private bool TryBindModelEvents()
        {
            if (Arena == null)
            {
                return false;
            }

            bool boundNew = false;

            if (!_turnTrackerSubscribed && Arena.TurnTrackerModel != null)
            {
                Arena.TurnTrackerModel.TurnOrderChanged += OnTurnOrderChanged;
                Arena.TurnTrackerModel.ActiveCombatantChanged += OnActiveCombatantChanged;
                Arena.TurnTrackerModel.EntryUpdated += OnTurnEntryUpdated;
                Arena.TurnTrackerModel.RoundChanged += OnRoundChanged;
                Arena.CombatantHoverChanged += OnCombatantHoverChanged;
                Arena.OnAIAbilityUsed += OnAIAbilityUsed;
                _turnTrackerSubscribed = true;
                boundNew = true;
            }

            if (!_resourceModelSubscribed && Arena.ResourceBarModel != null)
            {
                Arena.ResourceBarModel.ResourceChanged += OnResourceChanged;
                Arena.ResourceBarModel.HealthChanged += OnHealthChanged;
                _resourceModelSubscribed = true;
                boundNew = true;
            }

            if (!_actionModelSubscribed && Arena.ActionBarModel != null)
            {
                Arena.ActionBarModel.ActionsChanged += OnActionsChanged;
                Arena.ActionBarModel.ActionUpdated += OnActionUpdated;
                Arena.ActionBarModel.SelectionChanged += OnActionSelectionChanged;
                _actionModelSubscribed = true;
                boundNew = true;
            }

            return boundNew;
        }

        private void UnsubscribeAll()
        {
            UnsubscribeSpellSlotTracking();
            if (_stateMachine != null) _stateMachine.OnStateChanged -= OnStateChanged;
            if (_turnQueue != null) _turnQueue.OnTurnChanged -= OnTurnChanged;
            if (_combatLog != null) _combatLog.OnEntryAdded -= OnLogEntryAdded;
            if (_statusManager != null && _statusManagerSubscribed)
            {
                _statusManager.OnStatusApplied -= OnStatusApplied;
                _statusManager.OnStatusRemoved -= OnStatusRemoved;
            }
            if (_concentrationSystem != null && _concentrationSystemSubscribed)
            {
                _concentrationSystem.OnConcentrationStarted -= OnConcentrationChanged;
                _concentrationSystem.OnConcentrationBroken -= OnConcentrationBrokenHandler;
            }

            if (Arena != null)
            {
                Arena.CombatantHoverChanged -= OnCombatantHoverChanged;
                Arena.OnAIAbilityUsed -= OnAIAbilityUsed;
                if (Arena.TurnTrackerModel != null)
                {
                    Arena.TurnTrackerModel.TurnOrderChanged -= OnTurnOrderChanged;
                    Arena.TurnTrackerModel.ActiveCombatantChanged -= OnActiveCombatantChanged;
                    Arena.TurnTrackerModel.EntryUpdated -= OnTurnEntryUpdated;
                    Arena.TurnTrackerModel.RoundChanged -= OnRoundChanged;
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
                    Arena.ActionBarModel.SelectionChanged -= OnActionSelectionChanged;
                }
            }

            _turnTrackerSubscribed = false;
            _resourceModelSubscribed = false;
            _actionModelSubscribed = false;
            _statusManagerSubscribed = false;
            _concentrationSystemSubscribed = false;
            _combatLogBackfilled = false;
            _syncedCombatLogEntries = 0;

            // Panel events
            if (_turnControlsPanel != null)
            {
                _turnControlsPanel.OnEndTurnPressed -= OnEndTurnPressed;
                _turnControlsPanel.OnActionEditorPressed -= OnActionEditorPressed;
            }
            if (_actionBarPanel != null)
            {
                _actionBarPanel.OnActionPressed -= OnActionPressed;
                _actionBarPanel.OnActionHovered -= OnActionHovered;
                _actionBarPanel.OnActionHoverExited -= OnActionHoverExited;
                _actionBarPanel.OnActionReordered -= OnActionReordered;
            }
            if (_partyPanel != null) _partyPanel.OnMemberClicked -= OnPartyMemberClicked;
            if (_reactionPrompt != null)
            {
                _reactionPrompt.OnUseReaction -= OnReactionUse;
                _reactionPrompt.OnDeclineReaction -= OnReactionDecline;
            }

            // Stop layout save timer and save final state
            if (_layoutSaveTimer != null && GodotObject.IsInstanceValid(_layoutSaveTimer))
            {
                _layoutSaveTimer.Stop();
                _layoutSaveTimer.QueueFree();
            }

            // Final layout save on cleanup
            if (_panels != null)
            {
                HudLayoutService.SaveLayout(_panels);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  INITIAL SYNC
        // ════════════════════════════════════════════════════════════

        private void InitialSync()
        {
            if (Arena == null)
            {
                GD.PrintErr("[HudController] Arena is null during InitialSync!");
                return;
            }

            // Sync combat state — we may have missed the initial state transition
            if (_stateMachine != null)
            {
                bool isPlayerDecision = _stateMachine.CurrentState == CombatState.PlayerDecision;
                bool isPlayerTurn = Arena?.IsPlayerTurn ?? false;
                bool showPlayerHud = isPlayerDecision ||
                    (_stateMachine.CurrentState == CombatState.ActionExecution && isPlayerTurn);
                _actionBarPanel?.SetVisible(showPlayerHud);
                _resourceBarPanel?.SetVisible(showPlayerHud);
                _turnControlsPanel?.SetPlayerTurn(isPlayerDecision);
                _turnControlsPanel?.SetEnabled(isPlayerDecision);
            }

            // Sync turn tracker
            if (Arena.TurnTrackerModel != null && Arena.TurnTrackerModel.Entries.Count > 0)
            {
                _initiativeRibbon?.SetTurnOrder(
                    Arena.TurnTrackerModel.Entries,
                    Arena.TurnTrackerModel.ActiveCombatantId);
                _initiativeRibbon?.SetRound(Arena.TurnTrackerModel.CurrentRound);
            }
            else
            {
                // Fallback: initialize from combatants
                var combatants = Arena.GetCombatants()?.ToList();
                if (combatants != null && combatants.Count > 0)
                {
                    var entries = combatants.Select(c => new TurnTrackerEntry
                    {
                        CombatantId = c.Id,
                        DisplayName = c.Name,
                        Initiative = c.Initiative,
                        IsPlayer = c.Faction == Faction.Player,
                        HpPercent = c.Resources.MaxHP > 0
                            ? (float)c.Resources.CurrentHP / c.Resources.MaxHP
                            : 0,
                        IsDead = c.LifeState == CombatantLifeState.Dead,
                        PortraitPath = c.PortraitPath,
                    }).ToList();

                    _initiativeRibbon?.SetTurnOrder(entries, Arena.ActiveCombatantId);
                }
            }

            // Sync party panel
            SyncPartyPanel();

            // Sync action bar
            if (Arena.ActionBarModel != null && Arena.ActionBarModel.Actions.Count > 0)
            {
                _actionBarPanel?.SetActions(Arena.ActionBarModel.Actions);
                _actionBarPanel?.SetSelectedAction(Arena.ActionBarModel.SelectedActionId);
            }

            // Sync resources
            SyncResources();
            SyncCharacterSheetForCurrentTurn();
            UpdatePortraitHp();
            RefreshConcentrationIndicator();

            // Subscribe to spell slot tracking for active combatant
            var initialCombatant = Arena?.ActiveCombatantId != null
                ? Arena.Context?.GetCombatant(Arena.ActiveCombatantId)
                : null;
            if (initialCombatant != null && initialCombatant.IsPlayerControlled)
                SubscribeSpellSlotTracking(initialCombatant);
        }

        private void SyncPartyPanel()
        {
            if (Arena == null || _partyPanel == null) return;

            var combatants = Arena.GetCombatants()?.ToList();
            if (combatants == null) return;

            var partyMembers = combatants
                .Where(c => c.Faction == Faction.Player)
                .Select(c => new PartyMemberData
                {
                    Id = c.Id,
                    Name = c.Name,
                    HpCurrent = c.Resources.CurrentHP,
                    HpMax = c.Resources.MaxHP,
                    Conditions = GetPortraitConditionIndicators(c),
                    IsSelected = c.Id == Arena.SelectedCombatantId,
                    PortraitPath = c.PortraitPath,
                })
                .ToList();

            _partyPanel.SetPartyMembers(partyMembers);
        }

        private List<ConditionIndicator> GetPortraitConditionIndicators(Combatant combatant)
        {
            if (combatant == null || Arena?.Context == null)
                return new List<ConditionIndicator>();

            var manager = _statusManager ?? Arena.Context.GetService<StatusManager>();
            if (manager == null)
                return new List<ConditionIndicator>();

            return manager.GetStatuses(combatant.Id)
                .Where(s => s?.Definition != null && StatusPresentationPolicy.ShowInPortraitIndicators(s.Definition))
                .Select(s => new ConditionIndicator
                {
                    IconPath = !string.IsNullOrWhiteSpace(s.Definition.Icon) ? s.Definition.Icon : null,
                    DisplayName = StatusPresentationPolicy.GetDisplayName(s.Definition),
                    Description = s.Definition.Description ?? string.Empty,
                })
                .Where(c => !string.IsNullOrWhiteSpace(c.DisplayName))
                .GroupBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        private void OnStatusApplied(StatusInstance instance)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            QueuePartyMemberConditionRefresh(instance?.TargetId);
        }

        private void OnStatusRemoved(StatusInstance instance)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            QueuePartyMemberConditionRefresh(instance?.TargetId);
        }

        private void QueuePartyMemberConditionRefresh(string combatantId)
        {
            if (string.IsNullOrWhiteSpace(combatantId))
                return;

            CallDeferred(nameof(RefreshPartyMemberConditionIndicators), combatantId);
        }

        private void RefreshPartyMemberConditionIndicators(string combatantId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            var combatant = Arena?.Context?.GetCombatant(combatantId);
            if (combatant == null || combatant.Faction != Faction.Player)
                return;

            _partyPanel?.UpdateMember(
                combatantId,
                combatant.Resources.CurrentHP,
                combatant.Resources.MaxHP,
                GetPortraitConditionIndicators(combatant));
        }

        private void UpdatePortraitHp()
        {
            if (Arena == null) return;
            var combatant = GetActivePlayerCombatant();
            if (combatant != null)
            {
                if (_portraitColorRect != null)
                {
                    _portraitColorRect.Color = combatant.Faction == Faction.Player
                        ? HudTheme.PlayerBlue
                        : HudTheme.EnemyRed;
                }
                // Load portrait texture from combatant's assigned portrait
                if (_portraitTextureRect != null)
                {
                    if (!string.IsNullOrEmpty(combatant.PortraitPath) && ResourceLoader.Exists(combatant.PortraitPath))
                        _portraitTextureRect.Texture = GD.Load<Texture2D>(combatant.PortraitPath);
                    else
                        _portraitTextureRect.Texture = null;
                }
            }
            else
            {
                if (_portraitColorRect != null)
                    _portraitColorRect.Color = HudTheme.PlayerBlue;
                if (_portraitTextureRect != null)
                    _portraitTextureRect.Texture = null;
            }
        }

        private void RefreshConcentrationIndicator()
        {
            if (_concentrationContainer == null)
                return;

            var combatant = GetActivePlayerCombatant();
            if (combatant == null)
            {
                _concentrationContainer.Visible = false;
                return;
            }

            var concentrationSystem = _concentrationSystem ?? Arena?.Context?.GetService<ConcentrationSystem>();
            var info = concentrationSystem?.GetConcentratedEffect(combatant.Id);
            if (info == null)
            {
                _concentrationContainer.Visible = false;
                return;
            }

            var action = Arena?.GetActionById(info.ActionId);
            if (action == null)
            {
                _concentrationContainer.Visible = false;
                return;
            }

            if (_concentrationIcon != null)
            {
                if (!string.IsNullOrWhiteSpace(action.Icon) && ResourceLoader.Exists(action.Icon))
                    _concentrationIcon.Texture = GD.Load<Texture2D>(action.Icon);
                else
                    _concentrationIcon.Texture = null;
            }

            string tooltip = action.Name ?? info.ActionId;
            if (!string.IsNullOrWhiteSpace(action.Description))
                tooltip += "\n" + action.Description;

            _concentrationContainer.TooltipText = "Concentrating: " + tooltip;
            _concentrationContainer.Visible = true;
        }

        private void OnConcentrationChanged(string combatantId, ConcentrationInfo info)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            CallDeferred(nameof(RefreshConcentrationIndicator));
        }

        private void OnConcentrationBrokenHandler(string combatantId, ConcentrationInfo info, string reason)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            CallDeferred(nameof(RefreshConcentrationIndicator));
        }

        private void UpdateConcentrationIndicatorPosition(float portraitX, float portraitY, float portraitCircleSize, float concIconSize)
        {
            if (_concentrationContainer == null)
                return;

            float concX = portraitX + (portraitCircleSize - concIconSize) / 2f;
            float concY = portraitY - concIconSize - 4f;
            _concentrationContainer.GlobalPosition = new Vector2(concX, concY);
        }

        private void SyncResources()
        {
            if (Arena?.ResourceBarModel == null || _resourceBarPanel == null) return;

            foreach (var id in new[] { "action", "bonus_action", "movement", "reaction" })
            {
                var r = Arena.ResourceBarModel.GetResource(id);
                if (r != null)
                    _resourceBarPanel.SetResource(id, r.Current, r.Maximum);
            }

            SyncSpellSlots();
        }

        private void SyncSpellSlots()
        {
            if (_resourceBarPanel == null) return;

            var combatant = GetActivePlayerCombatant();
            if (combatant?.ActionResources == null) return;

            // Standard spell slots (levels 1-9)
            if (combatant.ActionResources.HasResource("SpellSlot"))
            {
                for (int level = 1; level <= 9; level++)
                {
                    int current = combatant.ActionResources.GetCurrent("SpellSlot", level);
                    int max = combatant.ActionResources.GetMax("SpellSlot", level);
                    _resourceBarPanel.SetSpellSlots(level, current, max);
                }
            }

            // Warlock pact magic slots
            if (combatant.ActionResources.HasResource("WarlockSpellSlot"))
            {
                // Find the active warlock slot level (highest level with max > 0)
                int warlockLevel = 0;
                int warlockCurrent = 0;
                int warlockMax = 0;
                for (int level = 9; level >= 1; level--)
                {
                    int max = combatant.ActionResources.GetMax("WarlockSpellSlot", level);
                    if (max > 0)
                    {
                        warlockLevel = level;
                        warlockCurrent = combatant.ActionResources.GetCurrent("WarlockSpellSlot", level);
                        warlockMax = max;
                        break;
                    }
                }
                if (warlockMax > 0)
                {
                    _resourceBarPanel.SetWarlockSlots(warlockCurrent, warlockMax, warlockLevel);
                }
            }
        }

        private void SubscribeSpellSlotTracking(Combatant combatant)
        {
            UnsubscribeSpellSlotTracking();
            if (combatant?.ActionResources == null) return;
            _trackedSpellSlotCombatant = combatant;
            combatant.ActionResources.OnResourcesChanged += OnActionResourcesChanged;
        }

        private void UnsubscribeSpellSlotTracking()
        {
            if (_trackedSpellSlotCombatant?.ActionResources != null)
                _trackedSpellSlotCombatant.ActionResources.OnResourcesChanged -= OnActionResourcesChanged;
            _trackedSpellSlotCombatant = null;
        }

        private void OnActionResourcesChanged()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            // Defer to avoid redundant syncs when multiple resources change in one frame
            CallDeferred(nameof(SyncSpellSlots));
        }

        private void SyncCharacterSheetForCurrentTurn()
        {
            if (Arena == null || _characterInventoryScreen == null)
            {
                return;
            }

            string activeId = Arena.ActiveCombatantId;
            if (!Arena.IsPlayerTurn || string.IsNullOrWhiteSpace(activeId))
            {
                if (_characterInventoryScreen.IsOpen)
                    _characterInventoryScreen.Close();
                return;
            }

            var combatant = Arena.Context?.GetCombatant(activeId);
            if (combatant == null || !combatant.IsPlayerControlled)
            {
                if (_characterInventoryScreen.IsOpen)
                    _characterInventoryScreen.Close();
                return;
            }

            // If the screen is open, refresh its data
            if (_characterInventoryScreen.IsOpen)
            {
                var data = BuildCharacterDisplayData(combatant);
                _characterInventoryScreen.RefreshData(data);
            }
        }

        // ════════════════════════════════════════════════════════════
        //  COMBAT EVENT HANDLERS
        // ════════════════════════════════════════════════════════════

        public void ShowMultiTargetPrompt(string abilityName, int pickedSoFar, int totalNeeded)
        {
            _multiTargetPrompt?.Show(abilityName, pickedSoFar, totalNeeded);
        }

        public void HideMultiTargetPrompt()
        {
            _multiTargetPrompt?.Hide();
        }

        private void OnStateChanged(StateTransitionEvent evt)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;

            HideMultiTargetPrompt();
            bool isPlayerDecision = evt.ToState == CombatState.PlayerDecision;
            bool isPlayerTurn = Arena != null && Arena.IsPlayerTurn;

            // Keep HUD visible during the player's own action execution (movement, spells, attacks).
            // Only hide it during AI turns (AIDecision + ActionExecution on AI turn).
            bool showPlayerHud = isPlayerDecision ||
                (evt.ToState == CombatState.ActionExecution && isPlayerTurn);

            _actionBarPanel?.SetVisible(showPlayerHud);
            _resourceBarPanel?.SetVisible(showPlayerHud);

            // Controls are only interactive during the player's decision phase
            _turnControlsPanel?.SetPlayerTurn(isPlayerDecision);
            _turnControlsPanel?.SetEnabled(isPlayerDecision);
            SyncCharacterSheetForCurrentTurn();
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;

            HideMultiTargetPrompt();
            // Update initiative ribbon
            if (Arena != null)
            {
                var combatants = Arena.GetCombatants()?.ToList();
                if (combatants != null)
                {
                    var entries = combatants.Select(c => new TurnTrackerEntry
                    {
                        CombatantId = c.Id,
                        DisplayName = c.Name,
                        Initiative = c.Initiative,
                        IsPlayer = c.Faction == Faction.Player,
                        HpPercent = c.Resources.MaxHP > 0
                            ? (float)c.Resources.CurrentHP / c.Resources.MaxHP
                            : 0,
                        IsDead = c.LifeState == CombatantLifeState.Dead,
                        PortraitPath = c.PortraitPath,
                    }).ToList();

                    _initiativeRibbon?.SetTurnOrder(entries, evt.CurrentCombatant?.Id);
                }
            }

            // Clear stale resource state before syncing new combatant
            _resourceBarPanel?.Reset();

            // Update resources for new combatant
            SyncResources();
            SyncCharacterSheetForCurrentTurn();

            // Track spell slot changes for the active combatant
            var activeCombatant = evt.CurrentCombatant;
            if (activeCombatant != null && activeCombatant.IsPlayerControlled)
                SubscribeSpellSlotTracking(activeCombatant);
            else
                UnsubscribeSpellSlotTracking();

            // Update party highlight
            if (evt.CurrentCombatant != null)
                _partyPanel?.SetSelectedMember(evt.CurrentCombatant.Id);

            UpdatePortraitHp();
            RefreshConcentrationIndicator();

            // Turn announcement overlay
            if (evt.CurrentCombatant != null)
            {
                var isPlayer = evt.CurrentCombatant.Faction == Faction.Player;
                if (_reactionPrompt == null || !_reactionPrompt.Visible)
                {
                    _turnAnnouncement?.Show(evt.CurrentCombatant.Name, isPlayer);
                }
            }
        }

        private void OnLogEntryAdded(CombatLogEntry entry)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;

            // Filter out debug/verbose entries — they're not player-facing feedback
            if (entry.Type == CombatLogEntryType.Debug || entry.Type == CombatLogEntryType.Error)
                return;
            if (entry.Severity == LogSeverity.Verbose)
                return;

            _combatLogPanel?.AddEntry(entry);
            _syncedCombatLogEntries = _combatLog?.Entries.Count ?? _syncedCombatLogEntries;
        }

        // ── UI Model Handlers ──────────────────────────────────────

        private void OnTurnOrderChanged()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            if (Arena?.TurnTrackerModel == null) return;

            _initiativeRibbon?.SetTurnOrder(
                Arena.TurnTrackerModel.Entries,
                Arena.TurnTrackerModel.ActiveCombatantId);
        }

        private void OnActiveCombatantChanged(string combatantId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            _initiativeRibbon?.SetActiveCombatant(combatantId);
            SyncCharacterSheetForCurrentTurn();
        }

        private void OnTurnEntryUpdated(string combatantId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            var entry = Arena?.TurnTrackerModel?.GetEntry(combatantId);
            if (entry != null)
                _initiativeRibbon?.UpdateEntry(combatantId, entry);

            // Also update party panel HP
            var combatant = Arena?.Context?.GetCombatant(combatantId);
            if (combatant != null)
            {
                _partyPanel?.UpdateMember(combatantId,
                    combatant.Resources.CurrentHP,
                    combatant.Resources.MaxHP,
                    GetPortraitConditionIndicators(combatant));
            }
        }

        private void OnRoundChanged(int round)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            _initiativeRibbon?.SetRound(round);
        }

        private void OnResourceChanged(string resourceId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            if (resourceId == "health") return;
            var resource = Arena?.ResourceBarModel?.GetResource(resourceId);
            if (resource != null)
                _resourceBarPanel?.SetResource(resourceId, resource.Current, resource.Maximum);
        }

        private void OnHealthChanged(int current, int max, int temp)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            SyncCharacterSheetForCurrentTurn();
            UpdatePortraitHp();
            RefreshConcentrationIndicator();
        }

        private void OnActionsChanged()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            if (Arena?.ActionBarModel == null) return;

            _actionBarPanel?.RefreshSpellLevelTabs(Arena.ActionBarModel.GetAvailableSpellLevels());
            _actionBarPanel?.SetActions(Arena.ActionBarModel.Actions);
            _actionBarPanel?.SetSelectedAction(Arena.ActionBarModel.SelectedActionId);
        }

        private void OnActionUpdated(string actionId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            if (Arena?.ActionBarModel == null) return;

            var action = Arena.ActionBarModel.Actions.FirstOrDefault(a => a.ActionId == actionId);
            if (action != null)
            {
                _actionBarPanel?.UpdateAction(actionId, action);
            }
            _actionBarPanel?.SetSelectedAction(Arena.ActionBarModel.SelectedActionId);
        }

        private void OnActionSelectionChanged(string actionId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            _actionBarPanel?.SetSelectedAction(string.IsNullOrWhiteSpace(actionId) ? null : actionId);
        }

        // ── Panel Event Handlers ───────────────────────────────────

        private void OnEndTurnPressed()
        {
            if (DebugUI) GD.Print("[HudController] End turn pressed");
            Arena?.EndCurrentTurn();
        }

        private void OnActionEditorPressed()
        {
            GD.Print("[HudController] Navigating to Action Editor scene.");
            GetTree().ChangeSceneToFile("res://Combat/Arena/ActionEditorArena.tscn");
        }

        private void OnActionPressed(int index)
        {
            if (DebugUI) GD.Print($"[HudController] Action pressed: {index}");
            if (Arena == null || !Arena.IsPlayerTurn || Arena.ActionBarModel == null) return;

            var entry = Arena.ActionBarModel.Actions.FirstOrDefault(a => a.SlotIndex == index);
            if (entry == null || string.IsNullOrWhiteSpace(entry.ActionId)) return;

            var action = Arena.GetActionById(entry.ActionId);
            if (action == null) return;

            // Check for variants
            if (action.Variants != null && action.Variants.Count > 0)
            {
                _pendingVariants = action.Variants;
                _pendingVariantActionId = action.Id;

                _variantPopup.Clear();
                for (int i = 0; i < action.Variants.Count; i++)
                    _variantPopup.AddItem(action.Variants[i].DisplayName ?? action.Variants[i].VariantId, i);

                // Position near mouse
                _variantPopup.Position = (Vector2I)GetGlobalMousePosition();
                _variantPopup.Popup();
            }
            else if (action.CanUpcast && action.SpellLevel > 0)
            {
                // Build list of available higher-level spell slots
                var combatant = Arena.Context?.GetCombatant(Arena.ActiveCombatantId);
                var upcastLevels = new List<int>();
                for (int lvl = action.SpellLevel + 1; lvl <= 9; lvl++)
                {
                    int slots = combatant?.ActionResources?.GetCurrent("SpellSlot", lvl) ?? 0;
                    if (slots > 0)
                        upcastLevels.Add(lvl);
                }

                if (upcastLevels.Count > 0)
                {
                    _isUpcastMode = true;
                    _pendingUpcastActionId = action.Id;
                    _pendingUpcastBaseLevel = action.SpellLevel;

                    _variantPopup.Clear();
                    // Base level option
                    int baseSlots = combatant?.ActionResources?.GetCurrent("SpellSlot", action.SpellLevel) ?? 0;
                    string baseSuffix = baseSlots > 0 ? $" ({baseSlots} slot{(baseSlots == 1 ? "" : "s")})" : " (base)";
                    _variantPopup.AddItem($"Level {action.SpellLevel}{baseSuffix}", action.SpellLevel);
                    foreach (int lvl in upcastLevels)
                    {
                        int slots = combatant.ActionResources.GetCurrent("SpellSlot", lvl);
                        _variantPopup.AddItem($"Level {lvl} ({slots} slot{(slots == 1 ? "" : "s")})", lvl);
                    }
                    _variantPopup.Position = (Vector2I)GetGlobalMousePosition();
                    _variantPopup.Popup();
                }
                else
                {
                    Arena.SelectAction(action.Id);
                }
            }
            else
            {
                Arena.SelectAction(action.Id);
            }
        }

        private void OnVariantSelected(long id)
        {
            // Upcast mode: id is the chosen spell slot level
            if (_isUpcastMode)
            {
                _isUpcastMode = false;
                var upcastActionId = _pendingUpcastActionId;
                int baseLevel = _pendingUpcastBaseLevel;
                _pendingUpcastActionId = null;
                _pendingUpcastBaseLevel = 0;

                var upcastAction = Arena?.GetActionById(upcastActionId);
                if (upcastAction != null)
                {
                    int upcastLevel = (int)id - baseLevel;
                    var opts = new ActionExecutionOptions { UpcastLevel = upcastLevel };
                    Arena.SelectAction(upcastAction.Id, opts);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(_pendingVariantActionId) || _pendingVariants == null || id >= _pendingVariants.Count)
                return;

            var variant = _pendingVariants[(int)id];
            var action = Arena?.GetActionById(_pendingVariantActionId);
            if (action != null)
            {
                var options = new ActionExecutionOptions { VariantId = variant.VariantId };
                Arena.SelectAction(action.Id, options);
            }

            _pendingVariants = null;
            _pendingVariantActionId = null;
        }

        private void OnActionHovered(int index)
        {
            if (_disposed || Arena?.ActionBarModel == null) return;

            var action = Arena.ActionBarModel.Actions.FirstOrDefault(a => a.SlotIndex == index);
            if (action == null) return;

            // Position tooltip above action bar
            if (_unifiedTooltip != null)
            {
                var screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
                _unifiedTooltip.SetFixedPosition(new Vector2(
                    (screenSize.X - 280) / 2,
                    _actionBarPanel?.GlobalPosition.Y - 160 ?? (screenSize.Y - 380)));
                _unifiedTooltip.ShowAction(action);
            }
        }

        private void OnActionReordered(int fromSlot, int toSlot)
        {
            if (Arena == null || !Arena.IsPlayerTurn)
            {
                return;
            }

            Arena.ReorderActionBarSlots(Arena.ActiveCombatantId, fromSlot, toSlot);
        }

        private void OnHotbarGridResized(int newColumns)
        {
            // Re-layout the bottom cluster when hotbar size changes
            if (_actionBarPanel == null) return;
            var screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);

            float newWidth = _actionBarPanel.CalculateWidth();
            const float portraitCircleSize = 125;
            const float hotbarGap = 4;
            float actionBarHeight = _actionBarPanel.CalculateHeight();

            float hotbarBottom = screenSize.Y - 54;
            float actionBarX = (screenSize.X - newWidth) / 2;
            float actionBarY = hotbarBottom - actionBarHeight;

            _actionBarPanel.Size = new Vector2(newWidth, actionBarHeight);
            _actionBarPanel.SetScreenPosition(new Vector2(actionBarX, actionBarY));

            float portraitX = actionBarX - hotbarGap - portraitCircleSize;
            float portraitY = screenSize.Y - 30 - portraitCircleSize;
            _portraitContainer.GlobalPosition = new Vector2(portraitX, portraitY);
            UpdateConcentrationIndicatorPosition(
                portraitX,
                portraitY,
                portraitCircleSize,
                _concentrationContainer?.Size.X ?? 40f);

            if (_resourceBarPanel != null)
            {
                float resWidth = Mathf.Max(newWidth, 500);
                _resourceBarPanel.Size = new Vector2(resWidth, 80);
                _resourceBarPanel.SetScreenPosition(new Vector2(
                    (screenSize.X - resWidth) / 2, actionBarY - 4 - 80));
            }

            if (_turnControlsPanel != null)
            {
                float turnX = actionBarX + newWidth + hotbarGap;
                float turnY = screenSize.Y - 30 - (125 + 10);
                _turnControlsPanel.SetScreenPosition(new Vector2(turnX, turnY));
            }

            if (_reactionIconContainer != null)
            {
                float reactionX = actionBarX + newWidth + 4;
                float reactionY = actionBarY - 10 - 30;
                _reactionIconContainer.GlobalPosition = new Vector2(reactionX, reactionY);
            }
        }

        private void OnActionHoverExited()
        {
            _unifiedTooltip?.Hide();
        }

        private void OnPartyMemberClicked(string memberId)
        {
            if (DebugUI) GD.Print($"[HudController] Party member clicked: {memberId}");

            if (Arena == null || !Arena.IsPlayerTurn || memberId != Arena.ActiveCombatantId)
            {
                return;
            }

            // Show character sheet for active player-controlled combatant
            var combatant = Arena?.Context?.GetCombatant(memberId);
            if (combatant != null)
                ShowCharacterSheet(combatant);
        }

        // ── Reaction Handling ──────────────────────────────────────

        private Action<bool> _reactionCallback;

        public void ShowReactionPrompt(string name, string description, string iconPath, Action<bool> callback)
        {
            _reactionCallback = callback;
            _reactionPrompt?.ShowPrompt(name, description, iconPath);
            if (_reactionPrompt != null)
                _windowManager?.ShowModal(_reactionPrompt);
        }

        private void OnReactionUse()
        {
            _windowManager?.CloseModal(_reactionPrompt);
            _reactionCallback?.Invoke(true);
            _reactionCallback = null;
        }

        private void OnReactionDecline()
        {
            _windowManager?.CloseModal(_reactionPrompt);
            _reactionCallback?.Invoke(false);
            _reactionCallback = null;
        }

        // ── Active Reaction Icons ──────────────────────────────────

        public void SetActiveReactions(System.Collections.Generic.IReadOnlyList<(string name, string iconPath)> reactions)
        {
            HudTheme.ClearChildren(_reactionIconContainer);
            if (reactions == null) return;
            foreach (var (name, iconPath) in reactions)
            {
                var icon = new TextureRect();
                icon.CustomMinimumSize = new Vector2(30, 30);
                icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
                icon.MouseFilter = MouseFilterEnum.Ignore;
                if (!string.IsNullOrEmpty(iconPath) && ResourceLoader.Exists(iconPath))
                    icon.Texture = GD.Load<Texture2D>(iconPath);
                _reactionIconContainer.AddChild(icon);
            }
        }

        // ── Character Sheet ────────────────────────────────────────

        /// <summary>
        /// Show the character sheet for a combatant. Called by CombatArena on select.
        /// </summary>
        public void ShowCharacterSheet(Combatant combatant)
        {
            if (combatant == null || _characterInventoryScreen == null) return;

            var invService = Arena?.Context?.GetService<InventoryService>();
            var data = BuildCharacterDisplayData(combatant);
            _characterInventoryScreen.Open(combatant, invService, data);
        }

        private CharacterDisplayData BuildCharacterDisplayData(Combatant combatant)
        {
            var data = new CharacterDisplayData
            {
                Name = combatant.Name,
                HpCurrent = combatant.Resources.CurrentHP,
                HpMax = combatant.Resources.MaxHP,
            };

            var rc = combatant.ResolvedCharacter;
            var passiveRegistry = Arena?.Context?.GetService<PassiveRegistry>();
            var charRegistry = Arena?.Context?.GetService<CharacterDataRegistry>();
            if (rc?.Sheet != null)
            {
                var classLevels = rc.Sheet.ClassLevels ?? new List<ClassLevel>();

                data.Race = ResolveRaceDisplayName(rc.Sheet.RaceId, charRegistry);
                data.Class = classLevels.Count > 0
                    ? string.Join(", ", classLevels
                        .GroupBy(cl => cl.ClassId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .Select(g => $"{ResolveClassDisplayName(g.Key, charRegistry)} {g.Count()}"))
                    : "";
                data.Level = classLevels.Count;
                data.ArmorClass = rc.BaseAC;
                data.Initiative = combatant.Initiative;
                data.Speed = Mathf.RoundToInt(rc.Speed);
                data.ProficiencyBonus = combatant.ProficiencyBonus;

                // Ability scores (individual properties)
                if (rc.AbilityScores != null)
                {
                    data.Strength = rc.AbilityScores.GetValueOrDefault(Data.CharacterModel.AbilityType.Strength, 10);
                    data.Dexterity = rc.AbilityScores.GetValueOrDefault(Data.CharacterModel.AbilityType.Dexterity, 10);
                    data.Constitution = rc.AbilityScores.GetValueOrDefault(Data.CharacterModel.AbilityType.Constitution, 10);
                    data.Intelligence = rc.AbilityScores.GetValueOrDefault(Data.CharacterModel.AbilityType.Intelligence, 10);
                    data.Wisdom = rc.AbilityScores.GetValueOrDefault(Data.CharacterModel.AbilityType.Wisdom, 10);
                    data.Charisma = rc.AbilityScores.GetValueOrDefault(Data.CharacterModel.AbilityType.Charisma, 10);
                }

                // Saving throws — list of proficient save names
                data.ProficientSaves = new List<string>();
                foreach (var ab in new[] {
                    Data.CharacterModel.AbilityType.Strength,
                    Data.CharacterModel.AbilityType.Dexterity,
                    Data.CharacterModel.AbilityType.Constitution,
                    Data.CharacterModel.AbilityType.Intelligence,
                    Data.CharacterModel.AbilityType.Wisdom,
                    Data.CharacterModel.AbilityType.Charisma })
                {
                    if (rc.Proficiencies.IsProficientInSave(ab))
                        data.ProficientSaves.Add(ab.ToString());
                }

                // Skills — Dictionary<string, int> of bonus values
                data.Skills = new Dictionary<string, int>();
                foreach (var skill in rc.Proficiencies.Skills)
                {
                    int bonus = rc.GetSkillBonus(skill, combatant.ProficiencyBonus);
                    string name = System.Text.RegularExpressions.Regex.Replace(
                        skill.ToString(), "([A-Z])", " $1").Trim();
                    data.Skills[name] = bonus;
                }

                // Features
                data.Features = rc.Features?
                    .Select(f =>
                    {
                        var passiveData = ResolvePassiveForFeature(passiveRegistry, f);
                        return ResolveFeatureDisplayName(f, passiveData);
                    })
                    .ToList()
                    ?? new List<string>();

                // Resources — convert from Dictionary<string, int> (max) to (current, max) tuples
                data.Resources = rc.Resources?.ToDictionary(
                    kvp => kvp.Key, kvp => (kvp.Value, kvp.Value))
                    ?? new Dictionary<string, (int current, int max)>();

                // ── New fields ──────────────────────────────────────────

                // Subclass — from first class level that has a subclass
                var subclassLevel = classLevels.FirstOrDefault(cl => !string.IsNullOrWhiteSpace(cl.SubclassId));
                data.Subclass = ResolveSubclassDisplayName(subclassLevel?.ClassId, subclassLevel?.SubclassId, charRegistry);

                // Background
                data.Background = ResolveDisplayIdentifier(rc.Sheet.BackgroundId);

                // Darkvision
                data.DarkvisionRange = Mathf.RoundToInt(rc.DarkvisionRange);

                // Carrying capacity (D&D 5e: STR × 15)
                data.CarryingCapacity = data.Strength * 15;

                // Primary ability: determine from class
                data.PrimaryAbility = DeterminePrimaryAbility(rc.Sheet.StartingClassId);

                // Saving throw modifiers
                data.SavingThrowModifiers = new Dictionary<string, int>();
                foreach (var ab in new[] {
                    Data.CharacterModel.AbilityType.Strength,
                    Data.CharacterModel.AbilityType.Dexterity,
                    Data.CharacterModel.AbilityType.Constitution,
                    Data.CharacterModel.AbilityType.Intelligence,
                    Data.CharacterModel.AbilityType.Wisdom,
                    Data.CharacterModel.AbilityType.Charisma })
                {
                    data.SavingThrowModifiers[ab.ToString()] = combatant.GetSavingThrowModifier(ab);
                }

                // Proficient skills
                data.ProficientSkills = new List<string>();
                if (rc.Proficiencies?.Skills != null)
                {
                    foreach (var skill in rc.Proficiencies.Skills)
                    {
                        string name = System.Text.RegularExpressions.Regex.Replace(
                            skill.ToString(), "([A-Z])", " $1").Trim();
                        data.ProficientSkills.Add(name);
                    }
                }

                // Expertise skills
                data.ExpertiseSkills = new List<string>();
                if (rc.Proficiencies?.Expertise != null)
                {
                    foreach (var skill in rc.Proficiencies.Expertise)
                    {
                        string name = System.Text.RegularExpressions.Regex.Replace(
                            skill.ToString(), "([A-Z])", " $1").Trim();
                        data.ExpertiseSkills.Add(name);
                    }
                }

                // Armor proficiencies
                data.ArmorProficiencies = rc.Proficiencies?.ArmorCategories?
                    .Select(a => a.ToString()).ToList() ?? new List<string>();

                // Weapon proficiencies — include both categories and specific weapons
                data.WeaponProficiencies = new List<string>();
                if (rc.Proficiencies?.WeaponCategories != null)
                    data.WeaponProficiencies.AddRange(rc.Proficiencies.WeaponCategories.Select(w => w.ToString()));
                if (rc.Proficiencies?.Weapons != null)
                    data.WeaponProficiencies.AddRange(rc.Proficiencies.Weapons.Select(w => w.ToString()));

                // Tags from combatant
                data.Tags = combatant.Tags ?? new List<string>();

                // Size from combatant
                data.Size = combatant.CreatureSize.ToString();
            }
            else
            {
                data.Race = "Unknown";
                data.Class = "";
                data.ArmorClass = 10 + combatant.Initiative / 2;
                data.Initiative = combatant.Initiative;
            }

            // ── Weapon stats (melee/ranged attack bonus + damage) ──
            var invService = Arena?.Context?.GetService<InventoryService>();
            if (invService != null)
            {
                var inv = invService.GetInventory(combatant.Id);
                if (inv != null)
                {
                    // Melee weapon stats
                    if (inv.EquippedItems.TryGetValue(EquipSlot.MainHand, out var meleeWeapon) && meleeWeapon?.WeaponDef != null)
                    {
                        var wep = meleeWeapon.WeaponDef;
                        int strMod = (int)Math.Floor((data.Strength - 10) / 2.0);
                        int dexMod = (int)Math.Floor((data.Dexterity - 10) / 2.0);
                        int abilityMod = wep.IsFinesse ? Math.Max(strMod, dexMod) : strMod;
                        int enchantment = wep.EnchantmentBonus;
                        data.MeleeAttackBonus = abilityMod + data.ProficiencyBonus + enchantment;
                        int minDmg = wep.DamageDiceCount + abilityMod + enchantment;
                        int maxDmg = wep.DamageDiceCount * wep.DamageDieFaces + abilityMod + enchantment;
                        data.MeleeDamageRange = $"{Math.Max(1, minDmg)}-{maxDmg}";
                        data.MeleeWeaponIconPath = meleeWeapon.IconPath ?? "";
                    }

                    // Ranged weapon stats
                    if (inv.EquippedItems.TryGetValue(EquipSlot.RangedMainHand, out var rangedWeapon) && rangedWeapon?.WeaponDef != null)
                    {
                        var wep = rangedWeapon.WeaponDef;
                        int dexMod = (int)Math.Floor((data.Dexterity - 10) / 2.0);
                        int enchantment = wep.EnchantmentBonus;
                        data.RangedAttackBonus = dexMod + data.ProficiencyBonus + enchantment;
                        int minDmg = wep.DamageDiceCount + dexMod + enchantment;
                        int maxDmg = wep.DamageDiceCount * wep.DamageDieFaces + dexMod + enchantment;
                        data.RangedDamageRange = $"{Math.Max(1, minDmg)}-{maxDmg}";
                        data.RangedWeaponIconPath = rangedWeapon.IconPath ?? "";
                    }

                    // Weight calculation: sum all items
                    int totalWeight = 0;
                    foreach (var item in inv.BagItems)
                        totalWeight += item.Weight * item.Quantity;
                    foreach (var kvp in inv.EquippedItems)
                        totalWeight += kvp.Value.Weight;
                    data.WeightCurrent = totalWeight;
                }
            }

            // Weight capacity = STR × 15 (D&D 5e standard)
            data.WeightMax = data.Strength * 15;

            // Notable features for equipment tab
            var rc2 = combatant.ResolvedCharacter;
            if (rc2?.Features != null)
            {
                data.NotableFeatures = rc2.Features
                    .Select(f =>
                    {
                        var passiveData = ResolvePassiveForFeature(passiveRegistry, f);
                        string name = ResolveFeatureDisplayName(f, passiveData);
                        return new FeatureDisplayData
                        {
                            Name = name,
                            Description = ResolveFeatureDescription(f, passiveData),
                            IconPath = HudIcons.ResolvePassiveFeatureIcon(f.Id, name, passiveData?.Icon)
                        };
                    })
                    .Take(8) // Show top 8 features
                    .ToList();
            }

            return data;
        }

        private static BG3PassiveData ResolvePassiveForFeature(PassiveRegistry passiveRegistry, Feature feature)
        {
            if (passiveRegistry == null || feature == null)
                return null;

            foreach (var candidate in EnumeratePassiveCandidates(feature))
            {
                var passive = passiveRegistry.GetPassive(candidate);
                if (passive != null)
                    return passive;
            }

            return null;
        }

        private static IEnumerable<string> EnumeratePassiveCandidates(Feature feature)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddCandidate(string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    seen.Add(value.Trim());
            }

            void AddDerived(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return;

                string trimmed = raw.Trim();
                AddCandidate(trimmed);
                AddCandidate(trimmed.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty));
                AddCandidate(ToPascalIdentifier(trimmed));
                AddCandidate(ToPascalIdentifier(RemoveFeatureSuffix(trimmed)));
            }

            AddDerived(feature.Id);
            AddDerived(feature.Name);
            return seen;
        }

        private static string ResolveFeatureDisplayName(Feature feature, BG3PassiveData passiveData)
        {
            if (!string.IsNullOrWhiteSpace(feature?.Name))
                return feature.Name;
            if (!string.IsNullOrWhiteSpace(passiveData?.DisplayName))
                return passiveData.DisplayName;
            if (!string.IsNullOrWhiteSpace(feature?.Id))
                return HumanizeIdentifier(feature.Id);
            return "Unnamed";
        }

        private static string ResolveFeatureDescription(Feature feature, BG3PassiveData passiveData)
        {
            if (!string.IsNullOrWhiteSpace(feature?.Description))
                return feature.Description;
            return passiveData?.Description ?? string.Empty;
        }

        private static string RemoveFeatureSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string[] suffixes =
            {
                "_feature", "_passive", "_ability", "_resource", "_toggle",
                "Feature", "Passive", "Ability", "Resource", "Toggle"
            };

            foreach (var suffix in suffixes)
            {
                if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return value[..^suffix.Length];
            }

            return value;
        }

        private static string ToPascalIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new System.Text.StringBuilder(value.Length);
            bool capNext = true;
            foreach (char c in value)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    capNext = true;
                    continue;
                }

                sb.Append(capNext ? char.ToUpperInvariant(c) : c);
                capNext = false;
            }

            return sb.ToString();
        }

        private static string HumanizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new System.Text.StringBuilder(value.Length + 8);
            char prev = '\0';
            foreach (char c in value)
            {
                if (c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[^1] != ' ')
                        sb.Append(' ');
                    prev = c;
                    continue;
                }

                if (char.IsUpper(c) && sb.Length > 0 && prev != ' ' && prev != '_' && prev != '-' && char.IsLower(prev))
                    sb.Append(' ');

                sb.Append(c);
                prev = c;
            }

            return sb.ToString().Trim();
        }

        // ── Tooltip ────────────────────────────────────────────────

        private void ShowTooltip(ActionBarEntry action)
        {
            if (_tooltipPanel == null) return;

            _tooltipName.Text = action.DisplayName ?? "Unknown";

            var costParts = new List<string>();
            if (action.ActionPointCost > 0) costParts.Add("Action");
            if (action.BonusActionCost > 0) costParts.Add("Bonus Action");
            if (action.MovementCost > 0) costParts.Add($"{action.MovementCost}m Movement");
            _tooltipCost.Text = costParts.Count > 0 ? string.Join(" · ", costParts) : "Free";

            _tooltipDesc.Text = "";
            _tooltipDesc.AppendText(action.Description ?? "No description available.");

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

            // Rich info fields
            SetTooltipLabel(_tooltipRange,
                action.Range > 0 ? $"Range: {action.Range:0.#}m" : null);
            SetTooltipLabel(_tooltipDamage,
                !string.IsNullOrEmpty(action.DamageSummary) ? $"Damage: {action.DamageSummary}" : null);
            SetTooltipLabel(_tooltipSave,
                !string.IsNullOrEmpty(action.SaveType) && action.SaveDC > 0
                    ? $"Save: {action.SaveType} DC {action.SaveDC}"
                    : null);
            SetTooltipLabel(_tooltipSchool,
                !string.IsNullOrEmpty(action.SpellSchool) ? $"School: {action.SpellSchool}" : null);
            SetTooltipLabel(_tooltipAoE,
                !string.IsNullOrEmpty(action.AoEShape) && action.AreaRadius > 0
                    ? $"Area: {action.AreaRadius:0.#}m {action.AoEShape}"
                    : (!string.IsNullOrEmpty(action.AoEShape) ? $"Area: {action.AoEShape}" : null));
            if (_tooltipConcentration != null)
            {
                _tooltipConcentration.Text = "Concentration";
                _tooltipConcentration.Visible = action.RequiresConcentration;
            }

            // Position above action bar
            var screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            _tooltipPanel.GlobalPosition = new Vector2(
                (screenSize.X - 280) / 2,
                _actionBarPanel?.GlobalPosition.Y - 160 ?? (screenSize.Y - 380));

            _tooltipPanel.Visible = true;
        }

        private static void SetTooltipLabel(Label lbl, string text)
        {
            if (lbl == null) return;
            lbl.Visible = !string.IsNullOrEmpty(text);
            if (lbl.Visible) lbl.Text = text;
        }

        private void HideTooltip()
        {
            _tooltipPending = false;
            _pendingTooltipAction = null;
            _tooltipDelayMs = 0f;
            if (_tooltipPanel != null && IsInstanceValid(_tooltipPanel))
                _tooltipPanel.Visible = false;
        }

        // ════════════════════════════════════════════════════════════
        //  PER-FRAME UPDATE
        // ════════════════════════════════════════════════════════════

        public override void _Process(double delta)
        {
            if (_disposed || Arena == null)
            {
                return;
            }

            bool boundServiceEvents = TryBindServiceEvents();
            bool boundModelEvents = TryBindModelEvents();
            if (boundServiceEvents || boundModelEvents)
            {
                InitialSync();
            }

            // Fallback sync: if event hookups are delayed or dropped, pull missing log entries directly.
            if (_combatLog != null && _combatLogPanel != null && _syncedCombatLogEntries < _combatLog.Entries.Count)
            {
                for (int i = _syncedCombatLogEntries; i < _combatLog.Entries.Count; i++)
                {
                    var entry = _combatLog.Entries[i];
                    if (entry.Type == CombatLogEntryType.Debug || entry.Type == CombatLogEntryType.Error)
                        continue;
                    if (entry.Severity == LogSeverity.Verbose)
                        continue;
                    _combatLogPanel.AddEntry(entry);
                }
                _syncedCombatLogEntries = _combatLog.Entries.Count;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API (for CombatArena compatibility)
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Update resources directly (backward compat with old HUD).
        /// </summary>
        public void UpdateResources(int action, int maxAction, int bonus, int maxBonus,
            int move, int maxMove, int reaction, int maxReaction)
        {
            _resourceBarPanel?.SetResource("action", action, maxAction);
            _resourceBarPanel?.SetResource("bonus_action", bonus, maxBonus);
            _resourceBarPanel?.SetResource("movement", move, maxMove);
            _resourceBarPanel?.SetResource("reaction", reaction, maxReaction);
        }

        /// <summary>
        /// Show the inspect/character-sheet for a combatant.
        /// </summary>
        public void ShowInspect(Combatant combatant)
        {
            ShowCharacterSheet(combatant);
        }

        /// <summary>
        /// Hide the inspect panel.
        /// </summary>
        public void HideInspect()
        {
            if (_characterInventoryScreen != null && _characterInventoryScreen.IsOpen)
            {
                _characterInventoryScreen.Close();
            }
        }

        /// <summary>
        /// Set the panel visibility for action bar elements during AI turns.
        /// </summary>
        public void SetActionBarVisible(bool visible)
        {
            _actionBarPanel?.SetVisible(visible);
            _resourceBarPanel?.SetVisible(visible);
        }

        // ── Hit Chance Display ─────────────────────────────────────

        /// <summary>
        /// Show hit chance overlay when hovering a target with an attack action.
        /// Called from CombatArena targeting.
        /// </summary>
        public void ShowTargetHitChance(int hitChance, string targetName = null)
        {
            if (_hitChancePanel == null || _hitChanceLabel == null) return;

            var prefix = string.IsNullOrEmpty(targetName) ? "" : $"{targetName}: ";
            _hitChanceLabel.Text = $"{prefix}{hitChance}% Hit";

            // Color code: green >= 70, yellow >= 40, red < 40
            Color textColor;
            if (hitChance >= 70) textColor = HudTheme.ActionGreen;
            else if (hitChance >= 40) textColor = HudTheme.MoveYellow;
            else textColor = HudTheme.HealthRed;
            _hitChanceLabel.AddThemeColorOverride("font_color", textColor);

            _hitChancePanel.Visible = true;
        }

        /// <summary>
        /// Hide the hit chance overlay.
        /// </summary>
        public void HideTargetHitChance()
        {
            if (_hitChancePanel != null && IsInstanceValid(_hitChancePanel))
                _hitChancePanel.Visible = false;
        }

        // ── Inventory ──────────────────────────────────────────────

        /// <summary>
        /// Toggle the inventory panel open/closed. Called by input handler on "I" press.
        /// </summary>
        public void ToggleInventory()
        {
            if (_characterInventoryScreen == null) return;

            var combatant = GetActivePlayerCombatant();
            if (combatant == null) return;

            var invService = Arena?.Context?.GetService<InventoryService>();
            if (invService == null) return;

            var data = BuildCharacterDisplayData(combatant);
            _characterInventoryScreen.Toggle(combatant, invService, data);
        }

        /// <summary>
        /// Show inventory for a specific combatant.
        /// </summary>
        public void ShowInventory(Combatant combatant)
        {
            if (_characterInventoryScreen == null || combatant == null) return;

            var invService = Arena?.Context?.GetService<InventoryService>();
            if (invService == null) return;

            var data = BuildCharacterDisplayData(combatant);
            _characterInventoryScreen.Open(combatant, invService, data);
        }

        public bool IsWorldInteractionBlocked()
        {
            if (_characterInventoryScreen?.IsOpen == true)
                return true;

            return _windowManager?.AnyModalOpen == true;
        }

        public void ShowSurfaceTooltip(SurfaceInstance surface)
        {
            if (_unifiedTooltip == null || surface?.Definition == null)
            {
                return;
            }

            string layerLabel = surface.Definition.Layer == SurfaceLayer.Cloud ? "Cloud" : "Surface";
            string title = $"{surface.Definition.Name} ({layerLabel})";
            string body = BuildSurfaceTooltipBody(surface);
            _unifiedTooltip.ShowText(title, body, HudTheme.Gold);
        }

        public void HideSurfaceTooltip()
        {
            _unifiedTooltip?.Hide();
        }

        private Combatant GetActivePlayerCombatant()
        {
            if (Arena == null) return null;
            string activeId = Arena.ActiveCombatantId;
            if (string.IsNullOrWhiteSpace(activeId)) return null;
            var combatant = Arena.Context?.GetCombatant(activeId);
            return combatant?.IsPlayerControlled == true ? combatant : null;
        }

        private static string ResolveRaceDisplayName(string raceId, CharacterDataRegistry registry)
        {
            if (string.IsNullOrWhiteSpace(raceId))
                return "Unknown";

            var race = registry?.GetRace(raceId);
            if (!string.IsNullOrWhiteSpace(race?.Name))
                return race.Name;

            return ResolveDisplayIdentifier(raceId);
        }

        private static string ResolveClassDisplayName(string classId, CharacterDataRegistry registry)
        {
            if (string.IsNullOrWhiteSpace(classId))
                return "Unknown";

            var classDef = registry?.GetClass(classId);
            if (!string.IsNullOrWhiteSpace(classDef?.Name))
                return classDef.Name;

            return ResolveDisplayIdentifier(classId);
        }

        private static string ResolveSubclassDisplayName(string classId, string subclassId, CharacterDataRegistry registry)
        {
            if (string.IsNullOrWhiteSpace(subclassId))
                return string.Empty;

            var classDef = registry?.GetClass(classId);
            var subclass = classDef?.Subclasses?.FirstOrDefault(s =>
                string.Equals(s.Id, subclassId, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(subclass?.Name))
                return subclass.Name;

            return ResolveDisplayIdentifier(subclassId);
        }

        private static string ResolveDisplayIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new System.Text.StringBuilder(value.Length + 8);
            char prev = '\0';
            foreach (char c in value)
            {
                if (c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[^1] != ' ')
                        sb.Append(' ');
                    prev = c;
                    continue;
                }

                if (char.IsUpper(c) && sb.Length > 0 && prev != ' ' && prev != '_' && prev != '-' && char.IsLower(prev))
                    sb.Append(' ');

                sb.Append(c);
                prev = c;
            }

            var words = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
            }
            return string.Join(' ', words);
        }

        private static string DeterminePrimaryAbility(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return "";
            return classId.ToLowerInvariant() switch
            {
                "wizard" => "INT",
                "artificer" => "INT",
                "cleric" => "WIS",
                "druid" => "WIS",
                "ranger" => "WIS",
                "monk" => "WIS",
                "sorcerer" => "CHA",
                "warlock" => "CHA",
                "bard" => "CHA",
                "paladin" => "CHA",
                "fighter" => "STR",
                "barbarian" => "STR",
                "rogue" => "DEX",
                _ => "",
            };
        }

        private static string BuildSurfaceTooltipBody(SurfaceInstance surface)
        {
            var definition = surface.Definition;
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(definition.Description))
            {
                lines.Add(definition.Description.Trim());
            }

            lines.Add(surface.IsPermanent
                ? "Duration: Permanent"
                : $"Duration: {surface.RemainingDuration} round(s)");
            lines.Add($"Radius: {surface.Radius:0.0}m");

            if (definition.DamagePerTrigger > 0f)
            {
                string damageType = string.IsNullOrWhiteSpace(definition.DamageType)
                    ? "damage"
                    : ResolveDisplayIdentifier(definition.DamageType).ToLowerInvariant();
                lines.Add($"Damage: {definition.DamagePerTrigger:0.#} {damageType} on enter/turn start");
            }

            if (!string.IsNullOrWhiteSpace(definition.DamageDicePerDistanceUnit))
            {
                lines.Add($"Traversal: {definition.DamageDicePerDistanceUnit} per {definition.DamageDistanceUnit:0.#}m moved");
            }

            if (!string.IsNullOrWhiteSpace(definition.AppliesStatusId))
            {
                string statusName = ResolveDisplayIdentifier(definition.AppliesStatusId);
                if (definition.SaveAbility.HasValue && definition.SaveDC.HasValue)
                {
                    string saveAbility = ResolveDisplayIdentifier(definition.SaveAbility.Value.ToString());
                    lines.Add($"Status: {statusName} (Save: {saveAbility} DC {definition.SaveDC.Value})");
                }
                else
                {
                    lines.Add($"Status: {statusName}");
                }
            }

            if (definition.MovementCostMultiplier > 1.01f)
            {
                lines.Add($"Movement Cost: x{definition.MovementCostMultiplier:0.##}");
            }

            if (definition.Tags != null && definition.Tags.Count > 0)
            {
                var tags = definition.Tags
                    .OrderBy(tag => tag)
                    .Select(ResolveDisplayIdentifier);
                lines.Add($"Tags: {string.Join(", ", tags)}");
            }

            return string.Join("\n", lines);
        }

        // ════════════════════════════════════════════════════════════
        //  HOVER INFO PANEL (Features 2 & 3)
        // ════════════════════════════════════════════════════════════

        private void OnCombatantHoverChanged(string combatantId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;

            if (string.IsNullOrEmpty(combatantId))
            {
                if (_hoverInfoPanel != null) _hoverInfoPanel.Visible = false;
                return;
            }

            var combatant = Arena?.GetCombatants()?.FirstOrDefault(c => c.Id == combatantId);
            if (combatant == null)
            {
                if (_hoverInfoPanel != null) _hoverInfoPanel.Visible = false;
                return;
            }

            UpdateHoverInfoPanel(combatant);
        }

        private void UpdateHoverInfoPanel(Combatant combatant)
        {
            if (_hoverInfoPanel == null) return;

            // Name + Level
            int level = combatant.ResolvedCharacter?.Sheet?.TotalLevel ?? 1;
            _hoverNameLabel.Text = $"{combatant.Name}  Lv {level}";

            // HP bar
            int currentHp = combatant.Resources.CurrentHP;
            int maxHp = combatant.Resources.MaxHP;
            float hpPercent = maxHp > 0 ? (float)currentHp / maxHp * 100f : 0f;
            _hoverHpBar.Value = hpPercent;
            _hoverHpBar.AddThemeStyleboxOverride("fill",
                HudTheme.CreateProgressBarFill(HudTheme.GetHealthColor(hpPercent)));
            _hoverHpText.Text = $"{currentHp}/{maxHp}";

            // Conditions
            foreach (var child in _hoverConditionsRow.GetChildren())
                child.QueueFree();

            var statusManager = Arena?.Context?.GetService<StatusManager>();
            if (statusManager != null)
            {
                var statuses = statusManager.GetStatuses(combatant.Id);
                foreach (var status in statuses)
                {
                    if (!StatusPresentationPolicy.ShowInPortraitIndicators(status.Definition))
                        continue;

                    var conditionBox = new HBoxContainer();
                    conditionBox.AddThemeConstantOverride("separation", 2);
                    conditionBox.MouseFilter = MouseFilterEnum.Ignore;

                    if (!string.IsNullOrWhiteSpace(status.Definition.Icon) &&
                        ResourceLoader.Exists(status.Definition.Icon))
                    {
                        var iconTex = ResourceLoader.Load<Texture2D>(status.Definition.Icon);
                        if (iconTex != null)
                        {
                            var iconRect = new TextureRect();
                            iconRect.CustomMinimumSize = new Vector2(16, 16);
                            iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                            iconRect.Texture = iconTex;
                            iconRect.MouseFilter = MouseFilterEnum.Ignore;
                            conditionBox.AddChild(iconRect);
                        }
                    }

                    var condLabel = new Label();
                    condLabel.Text = StatusPresentationPolicy.GetDisplayName(status.Definition);
                    HudTheme.StyleLabel(condLabel, HudTheme.FontTiny, HudTheme.MutedBeige);
                    condLabel.MouseFilter = MouseFilterEnum.Ignore;
                    conditionBox.AddChild(condLabel);
                    _hoverConditionsRow.AddChild(conditionBox);
                }
            }
            _hoverConditionsRow.Visible = _hoverConditionsRow.GetChildCount() > 0;

            // Targeting info (Feature 3)
            UpdateHoverTargetingInfo(combatant);

            // Position centered below initiative ribbon, deferred so panel is sized
            CallDeferred(nameof(PositionHoverInfoPanel));
            _hoverInfoPanel.Visible = true;
        }

        private void UpdateHoverTargetingInfo(Combatant target)
        {
            if (_hoverTargetingRow == null) return;

            var selectedAbilityId = Arena?.SelectedAbilityId;
            var selectedCombatantId = Arena?.SelectedCombatantId;

            if (string.IsNullOrEmpty(selectedAbilityId) || string.IsNullOrEmpty(selectedCombatantId))
            {
                _hoverTargetingRow.Visible = false;
                return;
            }

            var action = Arena?.GetActionById(selectedAbilityId);
            var actor = Arena?.GetCombatants()?.FirstOrDefault(c => c.Id == selectedCombatantId);
            if (action == null || actor == null)
            {
                _hoverTargetingRow.Visible = false;
                return;
            }

            // Load action icon at 50% size (24×24 instead of 48×48)
            _hoverActionIcon.Texture = null;
            if (!string.IsNullOrWhiteSpace(action.Icon) && ResourceLoader.Exists(action.Icon))
                _hoverActionIcon.Texture = ResourceLoader.Load<Texture2D>(action.Icon);

            string chanceText = "";
            if (action.AttackType.HasValue)
            {
                var rulesEngine = Arena?.Context?.GetService<RulesEngine>();
                if (rulesEngine != null)
                {
                    var effectPipeline = Arena?.Context?.GetService<EffectPipeline>();
                    int attackBonus = effectPipeline?.GetAttackBonus(actor, action) ?? 0;
                    int heightMod = effectPipeline?.Heights?.GetAttackModifier(actor, target) ?? 0;

                    var hitQuery = new QueryInput
                    {
                        Type = QueryType.AttackRoll,
                        Source = actor,
                        Target = target,
                        BaseValue = attackBonus + heightMod
                    };
                    var result = rulesEngine.CalculateHitChance(hitQuery);
                    int hitChance = (int)result.FinalValue;
                    chanceText = $"{hitChance}% Hit";
                    _hoverActionChance.AddThemeColorOverride("font_color",
                        hitChance >= 70 ? HudTheme.ActionGreen :
                        hitChance >= 40 ? HudTheme.MoveYellow : HudTheme.HealthRed);
                }
            }
            else if (!string.IsNullOrEmpty(action.SaveType))
            {
                var rulesEngine = Arena?.Context?.GetService<RulesEngine>();
                var effectPipeline = Arena?.Context?.GetService<EffectPipeline>();
                if (rulesEngine != null && effectPipeline != null)
                {
                    int saveDC = effectPipeline.GetSaveDC(actor, action);
                    int saveBonus = effectPipeline.GetSaveBonus(target, action.SaveType);
                    var saveQuery = new QueryInput
                    {
                        Type = QueryType.SavingThrow,
                        Source = actor,
                        Target = target,
                        DC = saveDC,
                        BaseValue = saveBonus
                    };
                    var result = rulesEngine.CalculateSaveFailChance(saveQuery);
                    int failChance = (int)result.FinalValue;
                    chanceText = $"{failChance}% Fail";
                    _hoverActionChance.AddThemeColorOverride("font_color",
                        failChance >= 70 ? HudTheme.ActionGreen :
                        failChance >= 40 ? HudTheme.MoveYellow : HudTheme.HealthRed);
                }
            }

            _hoverActionChance.Text = chanceText;
            _hoverTargetingRow.Visible = !string.IsNullOrEmpty(chanceText);
        }

        private void PositionHoverInfoPanel()
        {
            if (_hoverInfoPanel == null || _initiativeRibbon == null) return;
            if (!IsInstanceValid(_hoverInfoPanel) || !IsInstanceValid(_initiativeRibbon)) return;

            var ribbonPos = _initiativeRibbon.GlobalPosition;
            var ribbonSize = _initiativeRibbon.Size;

            float panelWidth = _hoverInfoPanel.Size.X;
            float ribbonCenterX = ribbonPos.X + ribbonSize.X / 2f;
            float panelX = ribbonCenterX - panelWidth / 2f;
            float panelY = ribbonPos.Y + ribbonSize.Y + 4f;

            _hoverInfoPanel.GlobalPosition = new Vector2(panelX, panelY);
        }

        // ════════════════════════════════════════════════════════════
        //  AI ACTION BANNER (Feature 4)
        // ════════════════════════════════════════════════════════════

        private void OnAIAbilityUsed(Combatant actor, ActionDefinition action)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            if (actor == null || actor.IsPlayerControlled) return;
            if (action == null) return;

            _aiActionBanner?.Show(action.Name ?? action.Id, action.Icon);
        }
    }
}
