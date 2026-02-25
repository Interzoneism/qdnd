using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Actions;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;
using QDND.Combat.UI.Panels;
using QDND.Combat.UI.Overlays;
using QDND.Combat.UI.Screens;
using QDND.Combat.Rules;

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
        private PanelContainer _portraitContainer;
        private ColorRect _portraitColorRect;
        private TextureRect _portraitTextureRect;
        private Label _portraitHpLabel;

        // ── Overlays ───────────────────────────────────────────────
        private ReactionPromptOverlay _reactionPrompt;
        private CharacterInventoryScreen _characterInventoryScreen;
        private HudWindowManager _windowManager;
        private TurnAnnouncementOverlay _turnAnnouncement;

        // ── Tooltip ────────────────────────────────────────────────
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
        private float _tooltipDelayMs;
        private bool _tooltipPending;
        private ActionBarEntry _pendingTooltipAction;
        private const float TooltipDelayThreshold = 400f; // ms

        // ── Hit Chance ─────────────────────────────────────────────
        private PanelContainer _hitChancePanel;
        private Label _hitChanceLabel;

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
        private bool _turnTrackerSubscribed;
        private bool _resourceModelSubscribed;
        private bool _actionModelSubscribed;
        private bool _combatLogBackfilled;
        private int _syncedCombatLogEntries;

        // ── Layout persistence ─────────────────────────────────────
        private Timer _layoutSaveTimer;
        private Dictionary<string, HudPanel> _panels;

        private bool _disposed;

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
            CreateVariantPopup();
            CreateHitChanceDisplay();
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
            // 12 cols × 48px + 11 gaps × 3px = 576 + 33 = 609
            const int slotSize = 48;
            const int slotGap = 3;
            const int gridCols = 12;
            float actionGridWidth = gridCols * slotSize + (gridCols - 1) * slotGap; // ~609
            float actionBarWidth = actionGridWidth + 24; // padding
            float actionBarHeight = 148; // will be dynamic once panel loads
            const float portraitSize = 72;
            const float turnBtnSize = 80;
            const float hotbarGap = 8;

            // Center the whole cluster: [portrait] [actionbar] [endturn]
            float clusterWidth = portraitSize + hotbarGap + actionBarWidth + hotbarGap + turnBtnSize;
            float clusterLeft = (screenSize.X - clusterWidth) / 2;
            float hotbarBottom = screenSize.Y - 8;

            // Initiative Ribbon — top center (auto-sizes to portrait count)
            _initiativeRibbon = new InitiativeRibbon();
            AddChild(_initiativeRibbon);
            _initiativeRibbon.Size = new Vector2(400, 100); // Initial size, auto-adjusts
            _initiativeRibbon.SetScreenPosition(new Vector2((screenSize.X - 400) / 2, 12));

            // Party Panel — left side (BG3-style compact portraits)
            _partyPanel = new PartyPanel();
            AddChild(_partyPanel);
            _partyPanel.Size = new Vector2(90, 460);
            _partyPanel.SetScreenPosition(new Vector2(4, 80));
            _partyPanel.OnMemberClicked += OnPartyMemberClicked;

            // ── Active Character Portrait — left of hotbar ─────────
            _portraitContainer = new PanelContainer();
            _portraitContainer.CustomMinimumSize = new Vector2(portraitSize, portraitSize);
            _portraitContainer.Size = new Vector2(portraitSize, portraitSize + 18);
            _portraitContainer.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(HudTheme.SecondaryDark, HudTheme.Gold, (int)(portraitSize / 2), 2, 4));
            _portraitContainer.ClipChildren = ClipChildrenMode.AndDraw;
            AddChild(_portraitContainer);

            var portraitVBox = new VBoxContainer();
            portraitVBox.Alignment = BoxContainer.AlignmentMode.Center;
            portraitVBox.AddThemeConstantOverride("separation", 2);
            _portraitContainer.AddChild(portraitVBox);

            // Portrait image area: ColorRect fallback + TextureRect overlay in a container
            var portraitImageContainer = new Control();
            portraitImageContainer.CustomMinimumSize = new Vector2(portraitSize - 8, portraitSize - 8);
            portraitImageContainer.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            portraitVBox.AddChild(portraitImageContainer);

            // Faction-colored portrait background (fallback)
            _portraitColorRect = new ColorRect();
            _portraitColorRect.SetAnchorsPreset(LayoutPreset.FullRect);
            _portraitColorRect.Color = HudTheme.PlayerBlue;
            _portraitColorRect.MouseFilter = MouseFilterEnum.Ignore;
            portraitImageContainer.AddChild(_portraitColorRect);

            // Portrait texture overlay (loaded from combatant PortraitPath)
            _portraitTextureRect = new TextureRect();
            _portraitTextureRect.SetAnchorsPreset(LayoutPreset.FullRect);
            _portraitTextureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _portraitTextureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            _portraitTextureRect.MouseFilter = MouseFilterEnum.Ignore;
            portraitImageContainer.AddChild(_portraitTextureRect);

            _portraitHpLabel = new Label();
            _portraitHpLabel.Text = "";
            _portraitHpLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_portraitHpLabel, HudTheme.FontTiny, HudTheme.HealthGreen);
            _portraitHpLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
            _portraitHpLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            _portraitHpLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            portraitVBox.AddChild(_portraitHpLabel);

            float portraitX = clusterLeft;
            float portraitY = hotbarBottom - (portraitSize + 18);
            _portraitContainer.GlobalPosition = new Vector2(portraitX, portraitY);

            // ── Action Bar — bottom center (2×12 grid + tabs) ──────
            _actionBarPanel = new ActionBarPanel();
            AddChild(_actionBarPanel);
            _actionBarPanel.Size = new Vector2(actionBarWidth, actionBarHeight);
            float actionBarX = clusterLeft + portraitSize + hotbarGap;
            float actionBarY = hotbarBottom - actionBarHeight;
            _actionBarPanel.SetScreenPosition(new Vector2(actionBarX, actionBarY));
            _actionBarPanel.OnActionPressed += OnActionPressed;
            _actionBarPanel.OnActionHovered += OnActionHovered;
            _actionBarPanel.OnActionHoverExited += OnActionHoverExited;
            _actionBarPanel.OnActionReordered += OnActionReordered;
            _actionBarPanel.OnGridResized += OnHotbarGridResized;

            // ── Resource Bar — above the action grid, same width ───
            _resourceBarPanel = new ResourceBarPanel();
            AddChild(_resourceBarPanel);
            _resourceBarPanel.Size = new Vector2(Mathf.Max(actionBarWidth, 500), 80);
            _resourceBarPanel.SetScreenPosition(new Vector2(
                (screenSize.X - Mathf.Max(actionBarWidth, 500)) / 2, actionBarY - 84));

            // ── Turn Controls (circular End Turn) — right of hotbar ─
            _turnControlsPanel = new TurnControlsPanel();
            AddChild(_turnControlsPanel);
            _turnControlsPanel.Size = new Vector2(turnBtnSize, turnBtnSize + 24);
            float turnX = actionBarX + actionBarWidth + hotbarGap;
            float turnY = hotbarBottom - (turnBtnSize + 24);
            _turnControlsPanel.SetScreenPosition(new Vector2(turnX, turnY));
            _turnControlsPanel.OnEndTurnPressed += OnEndTurnPressed;
            _turnControlsPanel.OnActionEditorPressed += OnActionEditorPressed;

            // ── Combat Log — right side ─────────────────────────────
            _combatLogPanel = new CombatLogPanel();
            AddChild(_combatLogPanel);
            _combatLogPanel.Size = new Vector2(300, 600);
            _combatLogPanel.SetScreenPosition(new Vector2(screenSize.X - 320, 100));

            // Initialize resource bar with defaults
            int maxMove = Mathf.RoundToInt(Arena?.DefaultMovePoints ?? 10f);
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

            if (_combatLog != null && !_combatLogBackfilled)
            {
                foreach (var entry in _combatLog.GetRecentEntries(100))
                {
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
            if (_stateMachine != null) _stateMachine.OnStateChanged -= OnStateChanged;
            if (_turnQueue != null) _turnQueue.OnTurnChanged -= OnTurnChanged;
            if (_combatLog != null) _combatLog.OnEntryAdded -= OnLogEntryAdded;

            if (Arena != null)
            {
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
                _actionBarPanel?.SetVisible(isPlayerDecision);
                _resourceBarPanel?.SetVisible(isPlayerDecision);
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
                    Conditions = new List<string>(),
                    IsSelected = c.Id == Arena.SelectedCombatantId,
                    PortraitPath = c.PortraitPath,
                })
                .ToList();

            _partyPanel.SetPartyMembers(partyMembers);
        }

        private void UpdatePortraitHp()
        {
            if (_portraitHpLabel == null || Arena == null) return;
            var combatant = GetActivePlayerCombatant();
            if (combatant != null)
            {
                _portraitHpLabel.Text = $"{combatant.Resources.CurrentHP}/{combatant.Resources.MaxHP}";
                if (_portraitColorRect != null)
                {
                    _portraitColorRect.Color = combatant.Faction == Faction.Player
                        ? HudTheme.PlayerBlue
                        : HudTheme.EnemyRed;
                }
                // Load portrait texture from combatant's assigned portrait
                if (_portraitTextureRect != null && !string.IsNullOrEmpty(combatant.PortraitPath))
                {
                    if (ResourceLoader.Exists(combatant.PortraitPath))
                        _portraitTextureRect.Texture = GD.Load<Texture2D>(combatant.PortraitPath);
                }
            }
            else
            {
                _portraitHpLabel.Text = "--/--";
                if (_portraitColorRect != null)
                    _portraitColorRect.Color = HudTheme.PlayerBlue;
                if (_portraitTextureRect != null)
                    _portraitTextureRect.Texture = null;
            }
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

        private void OnStateChanged(StateTransitionEvent evt)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;

            bool isPlayerDecision = evt.ToState == CombatState.PlayerDecision;

            _actionBarPanel?.SetVisible(isPlayerDecision);
            _resourceBarPanel?.SetVisible(isPlayerDecision);
            _turnControlsPanel?.SetPlayerTurn(isPlayerDecision);
            _turnControlsPanel?.SetEnabled(isPlayerDecision);
            SyncCharacterSheetForCurrentTurn();
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;

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

            // Update party highlight
            if (evt.CurrentCombatant != null)
                _partyPanel?.SetSelectedMember(evt.CurrentCombatant.Id);

            UpdatePortraitHp();

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
                    new List<string>());
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
            var resource = Arena?.ResourceBarModel?.GetResource(resourceId);
            if (resource != null)
                _resourceBarPanel?.SetResource(resourceId, resource.Current, resource.Maximum);
        }

        private void OnHealthChanged(int current, int max, int temp)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            SyncCharacterSheetForCurrentTurn();
            UpdatePortraitHp();
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
            if (action == null)
            {
                return;
            }
            // Start delayed tooltip show (BG3-style)
            _pendingTooltipAction = action;
            _tooltipDelayMs = 0f;
            _tooltipPending = true;
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
            const float portraitSize = 72;
            const float turnBtnSize = 80;
            const float hotbarGap = 8;
            float actionBarHeight = _actionBarPanel.CalculateHeight();

            float clusterWidth = portraitSize + hotbarGap + newWidth + hotbarGap + turnBtnSize;
            float clusterLeft = (screenSize.X - clusterWidth) / 2;
            float hotbarBottom = screenSize.Y - 8;

            _actionBarPanel.Size = new Vector2(newWidth, actionBarHeight);
            _actionBarPanel.SetScreenPosition(new Vector2(clusterLeft + portraitSize + hotbarGap, hotbarBottom - actionBarHeight));

            _portraitContainer.GlobalPosition = new Vector2(clusterLeft, hotbarBottom - (portraitSize + 18));

            if (_resourceBarPanel != null)
            {
                float resWidth = Mathf.Max(newWidth, 500);
                _resourceBarPanel.Size = new Vector2(resWidth, 80);
                _resourceBarPanel.SetScreenPosition(new Vector2(
                    (screenSize.X - resWidth) / 2, hotbarBottom - actionBarHeight - 84));
            }

            if (_turnControlsPanel != null)
            {
                float turnX = clusterLeft + portraitSize + hotbarGap + newWidth + hotbarGap;
                _turnControlsPanel.SetScreenPosition(new Vector2(turnX, hotbarBottom - (turnBtnSize + 24)));
            }
        }

        private void OnActionHoverExited()
        {
            HideTooltip();
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

        // ── Character Sheet ────────────────────────────────────────

        /// <summary>
        /// Show the character sheet for a combatant. Called by CombatArena on select.
        /// </summary>
        public void ShowCharacterSheet(Combatant combatant)
        {
            if (combatant == null || _characterInventoryScreen == null) return;

            var invService = Arena?.Context?.GetService<InventoryService>();
            var data = BuildCharacterDisplayData(combatant);
            _characterInventoryScreen.Open(combatant, invService, data, tabIndex: 1);
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
            if (rc?.Sheet != null)
            {
                data.Race = rc.Sheet.RaceId ?? "Unknown";
                data.Class = rc.Sheet.ClassLevels?.Count > 0
                    ? string.Join(", ", rc.Sheet.ClassLevels.GroupBy(cl => cl.ClassId)
                        .Select(g => $"{g.Key} {g.Count()}"))
                    : "";
                data.Level = rc.Sheet.ClassLevels?.Count ?? 0;
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
                data.Features = rc.Features?.Select(f => f.Name ?? "Unnamed").ToList()
                    ?? new List<string>();

                // Resources — convert from Dictionary<string, int> (max) to (current, max) tuples
                data.Resources = rc.Resources?.ToDictionary(
                    kvp => kvp.Key, kvp => (kvp.Value, kvp.Value))
                    ?? new Dictionary<string, (int current, int max)>();
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
                    .Select(f => new FeatureDisplayData
                    {
                        Name = f.Name ?? "Unnamed",
                        Description = f.Description ?? "",
                        IconPath = ""
                    })
                    .Take(8) // Show top 8 features
                    .ToList();
            }

            return data;
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

            // Tooltip delay tick
            if (_tooltipPending && _pendingTooltipAction != null)
            {
                _tooltipDelayMs += (float)(delta * 1000.0);
                if (_tooltipDelayMs >= TooltipDelayThreshold)
                {
                    _tooltipPending = false;
                    ShowTooltip(_pendingTooltipAction);
                    _pendingTooltipAction = null;
                }
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
                    _combatLogPanel.AddEntry(_combatLog.Entries[i]);
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

        // ── Spell Slots ────────────────────────────────────────────

        /// <summary>
        /// Update spell slot display for a given level.
        /// </summary>
        public void UpdateSpellSlots(int level, int current, int max)
        {
            _resourceBarPanel?.SetSpellSlots(level, current, max);
        }

        /// <summary>
        /// Update warlock pact slot display.
        /// </summary>
        public void UpdateWarlockSlots(int current, int max, int level)
        {
            _resourceBarPanel?.SetWarlockSlots(current, max, level);
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
            _characterInventoryScreen.Open(combatant, invService, data, tabIndex: 0);
        }

        private Combatant GetActivePlayerCombatant()
        {
            if (Arena == null) return null;
            string activeId = Arena.ActiveCombatantId;
            if (string.IsNullOrWhiteSpace(activeId)) return null;
            var combatant = Arena.Context?.GetCombatant(activeId);
            return combatant?.IsPlayerControlled == true ? combatant : null;
        }
    }
}
