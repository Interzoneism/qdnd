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
using QDND.Combat.UI.Panels;
using QDND.Combat.UI.Overlays;

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

        // ── Overlays ───────────────────────────────────────────────
        private ReactionPromptOverlay _reactionPrompt;
        private CharacterSheetModal _characterSheet;
        private InventoryPanel _inventoryPanel;
        private HudWindowManager _windowManager;

        // ── Tooltip ────────────────────────────────────────────────
        private PanelContainer _tooltipPanel;
        private TextureRect _tooltipIcon;
        private Label _tooltipName;
        private Label _tooltipCost;
        private RichTextLabel _tooltipDesc;

        // ── Variant popup ──────────────────────────────────────────
        private PopupMenu _variantPopup;
        private List<ActionVariant> _pendingVariants;
        private string _pendingVariantActionId;

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

            // Initiative Ribbon — top center, auto-sized to fit all combatant portraits
            _initiativeRibbon = new InitiativeRibbon();
            AddChild(_initiativeRibbon);
            // Use full screen width so the CenterContainer inside can center the portraits
            _initiativeRibbon.Size = new Vector2(screenSize.X - 24, 100);
            _initiativeRibbon.SetScreenPosition(new Vector2(12, 12));

            // Party Panel — left side
            _partyPanel = new PartyPanel();
            AddChild(_partyPanel);
            _partyPanel.Size = new Vector2(240, 400);
            _partyPanel.SetScreenPosition(new Vector2(10, 100));
            _partyPanel.OnMemberClicked += OnPartyMemberClicked;

            // Action Bar — bottom center
            _actionBarPanel = new ActionBarPanel();
            AddChild(_actionBarPanel);
            _actionBarPanel.Size = new Vector2(800, 160);
            _actionBarPanel.SetScreenPosition(new Vector2((screenSize.X - 800) / 2, screenSize.Y - 200));
            _actionBarPanel.OnActionPressed += OnActionPressed;
            _actionBarPanel.OnActionHovered += OnActionHovered;
            _actionBarPanel.OnActionHoverExited += OnActionHoverExited;

            // Resource Bar — above action bar, centered
            _resourceBarPanel = new ResourceBarPanel();
            AddChild(_resourceBarPanel);
            _resourceBarPanel.Size = new Vector2(280, 40);
            _resourceBarPanel.SetScreenPosition(new Vector2(
                (screenSize.X - 280) / 2, screenSize.Y - 244));

            // Turn Controls — bottom-right of action bar
            _turnControlsPanel = new TurnControlsPanel();
            AddChild(_turnControlsPanel);
            _turnControlsPanel.Size = new Vector2(160, 80);
            _turnControlsPanel.SetScreenPosition(new Vector2(
                (screenSize.X + 800) / 2 + 10, screenSize.Y - 120));
            _turnControlsPanel.OnEndTurnPressed += OnEndTurnPressed;

            // Combat Log — right side
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

            _characterSheet = new CharacterSheetModal();
            _characterSheet.Visible = false;
            _characterSheet.Size = new Vector2(380, 600);
            _windowManager.AddChild(_characterSheet); // Must be in tree for _Ready

            _inventoryPanel = new InventoryPanel();
            _inventoryPanel.Visible = false;
            _inventoryPanel.Size = new Vector2(660, 480);
            _inventoryPanel.OnCloseRequested += () => _windowManager?.CloseModal(_inventoryPanel);
            _windowManager.AddChild(_inventoryPanel); // Must be in tree for _Ready
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
        }

        private void CreateVariantPopup()
        {
            _variantPopup = new PopupMenu();
            _variantPopup.IdPressed += OnVariantSelected;
            AddChild(_variantPopup);
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
                { "character_sheet", _characterSheet },
                { "inventory_panel", _inventoryPanel }
            };

            // Load saved layout from previous session
            var loadedPanelIds = HudLayoutService.LoadLayout(_panels);
            if (loadedPanelIds.Count > 0)
            {
                GD.Print("[HudController] Loaded saved HUD layout.");
                
                // Mark character sheet as already positioned if it had saved data
                // This prevents window manager from re-centering it on first show
                if (loadedPanelIds.Contains("character_sheet") && _characterSheet != null && _windowManager != null)
                {
                    // Defer to ensure position has been applied and character sheet is fully in tree
                    CallDeferred(nameof(DeferredMarkCharacterSheetPositioned));
                }
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

        private void DeferredMarkCharacterSheetPositioned()
        {
            if (_characterSheet != null && _windowManager != null)
            {
                var currentPos = _characterSheet.GlobalPosition;
                GD.Print($"[HudController] Character sheet current position: {currentPos}");
                _windowManager.MarkAsPositioned(_characterSheet);
                GD.Print("[HudController] Character sheet marked as positioned - will not be re-centered on first show.");
            }
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
                }
            }

            _turnTrackerSubscribed = false;
            _resourceModelSubscribed = false;
            _actionModelSubscribed = false;
            _combatLogBackfilled = false;
            _syncedCombatLogEntries = 0;

            // Panel events
            if (_turnControlsPanel != null) _turnControlsPanel.OnEndTurnPressed -= OnEndTurnPressed;
            if (_actionBarPanel != null)
            {
                _actionBarPanel.OnActionPressed -= OnActionPressed;
                _actionBarPanel.OnActionHovered -= OnActionHovered;
                _actionBarPanel.OnActionHoverExited -= OnActionHoverExited;
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
                _actionBarPanel?.SetActions(Arena.ActionBarModel.Actions);

            // Sync resources
            SyncResources();
            SyncCharacterSheetForCurrentTurn();
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
                })
                .ToList();

            _partyPanel.SetPartyMembers(partyMembers);
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
            if (Arena == null || _characterSheet == null)
            {
                return;
            }

            string activeId = Arena.ActiveCombatantId;
            if (!Arena.IsPlayerTurn || string.IsNullOrWhiteSpace(activeId))
            {
                _windowManager.CloseModal(_characterSheet);
                _characterSheet.Visible = false;
                return;
            }

            var combatant = Arena.Context?.GetCombatant(activeId);
            if (combatant == null || !combatant.IsPlayerControlled)
            {
                _windowManager.CloseModal(_characterSheet);
                _characterSheet.Visible = false;
                return;
            }

            var data = BuildCharacterSheetData(combatant);
            _characterSheet.SetCombatant(data);
            _characterSheet.Visible = true;
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

            // Update resources for new combatant
            SyncResources();
            SyncCharacterSheetForCurrentTurn();

            // Update party highlight
            if (evt.CurrentCombatant != null)
                _partyPanel?.SetSelectedMember(evt.CurrentCombatant.Id);
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
        }

        private void OnActionsChanged()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            if (Arena?.ActionBarModel == null) return;

            _actionBarPanel?.SetActions(Arena.ActionBarModel.Actions);
        }

        private void OnActionUpdated(string actionId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree()) return;
            if (Arena?.ActionBarModel == null) return;

            var action = Arena.ActionBarModel.Actions.FirstOrDefault(a => a.ActionId == actionId);
            if (action != null)
                _actionBarPanel?.UpdateAction(actionId, action);
        }

        // ── Panel Event Handlers ───────────────────────────────────

        private void OnEndTurnPressed()
        {
            if (DebugUI) GD.Print("[HudController] End turn pressed");
            Arena?.EndCurrentTurn();
        }

        private void OnActionPressed(int index)
        {
            if (DebugUI) GD.Print($"[HudController] Action pressed: {index}");
            if (Arena == null || !Arena.IsPlayerTurn || Arena.ActionBarModel == null) return;

            var entries = Arena.ActionBarModel.Actions;
            if (index < 0 || index >= entries.Count) return;

            var entry = entries[index];
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
            else
            {
                Arena.SelectAction(action.Id);
                _actionBarPanel?.SetSelectedAction(action.Id);
            }
        }

        private void OnVariantSelected(long id)
        {
            if (string.IsNullOrWhiteSpace(_pendingVariantActionId) || _pendingVariants == null || id >= _pendingVariants.Count)
                return;

            var variant = _pendingVariants[(int)id];
            var action = Arena?.GetActionById(_pendingVariantActionId);
            if (action != null)
            {
                var options = new ActionExecutionOptions { VariantId = variant.VariantId };
                Arena.SelectAction(action.Id, options);
                _actionBarPanel?.SetSelectedAction(action.Id);
            }

            _pendingVariants = null;
            _pendingVariantActionId = null;
        }

        private void OnActionHovered(int index)
        {
            if (_disposed || Arena?.ActionBarModel == null) return;

            var actions = Arena.ActionBarModel.Actions;
            if (index < 0 || index >= actions.Count) return;

            var action = actions[index];
            ShowTooltip(action);
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
            if (combatant == null || _characterSheet == null) return;

            var data = BuildCharacterSheetData(combatant);
            _characterSheet.SetCombatant(data);
            _characterSheet.Visible = true;
        }

        private CharacterSheetData BuildCharacterSheetData(Combatant combatant)
        {
            var data = new CharacterSheetData
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

            // Position above action bar
            var screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            _tooltipPanel.GlobalPosition = new Vector2(
                (screenSize.X - 280) / 2,
                _actionBarPanel?.GlobalPosition.Y - 160 ?? (screenSize.Y - 380));

            _tooltipPanel.Visible = true;
        }

        private void HideTooltip()
        {
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
            if (_characterSheet != null)
            {
                _windowManager?.CloseModal(_characterSheet);
                _characterSheet.Visible = false;
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

        // ── Inventory ──────────────────────────────────────────────

        /// <summary>
        /// Toggle the inventory panel open/closed. Called by input handler on "I" press.
        /// </summary>
        public void ToggleInventory()
        {
            if (_inventoryPanel == null || _windowManager == null) return;

            if (_windowManager.IsModalOpen(_inventoryPanel))
            {
                _windowManager.CloseModal(_inventoryPanel);
            }
            else
            {
                // Show inventory for the currently active player combatant
                var combatant = GetActivePlayerCombatant();
                if (combatant != null)
                {
                    var invService = Arena?.Context?.GetService<InventoryService>();
                    if (invService != null)
                    {
                        _inventoryPanel.SetCombatant(combatant, invService);
                        _windowManager.AllowStacking = true;
                        _windowManager.ShowModal(_inventoryPanel);
                        _windowManager.AllowStacking = false;
                    }
                }
            }
        }

        /// <summary>
        /// Show inventory for a specific combatant.
        /// </summary>
        public void ShowInventory(Combatant combatant)
        {
            if (_inventoryPanel == null || _windowManager == null || combatant == null) return;

            var invService = Arena?.Context?.GetService<InventoryService>();
            if (invService != null)
            {
                _inventoryPanel.SetCombatant(combatant, invService);
                _windowManager.AllowStacking = true;
                _windowManager.ShowModal(_inventoryPanel);
                _windowManager.AllowStacking = false;
            }
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
