using Godot;
using System;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Actions;
using QDND.Combat.UI;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Targeting mode for input handling.
    /// </summary>
    public enum TargetingMode
    {
        None,
        Action,
        Move
    }

    /// <summary>
    /// Handles player input for combat: selection, targeting, and camera control.
    /// </summary>
    public partial class CombatInputHandler : Node
    {
        [Export] public CombatArena Arena;
        [Export] public Camera3D Camera;
        [Export] public float RayLength = 100f;
        [Export] public bool DebugInput = true;
        [Export] public bool DebugRaycastVerbose = false;
        [Export] public int DebugRaycastThrottleFrames = 30;
        [Export] public uint CombatantRaycastMask = 2; // Godot layer 2 (bitmask value 2)

        // Camera control settings
        [Export] public float CameraPanSpeed = 10f;
        [Export] public float CameraRotateSpeed = 60f; // degrees per second
        [Export] public float CameraZoomSpeed = 2f;
        [Export] public float MinZoom = 5f;
        [Export] public float MaxZoom = 30f;
        [Export] public float MinPitch = 20f; // Minimum pitch angle (more horizontal)
        [Export] public float MaxPitch = 80f; // Maximum pitch angle (more top-down)

        private CombatantVisual _hoveredVisual;
        private PhysicsDirectSpaceState3D _spaceState;
        private int _debugFrameCounter = 0;
        private int _rayDebugThrottleCounter = 0;
        private bool _previousRayHit = false;
        private HudController _hudController;

        private TargetingMode _currentMode = TargetingMode.None;
        private string _movingActorId;

        public override void _Ready()
        {
            if (Arena == null)
            {
                Arena = GetParent<CombatArena>();
            }
            if (Camera == null && Arena != null)
            {
                Camera = Arena.GetNodeOrNull<Camera3D>("TacticalCamera");
            }

            // Locate HudController in the HUD CanvasLayer
            if (Arena != null)
            {
                var hudLayer = Arena.GetNodeOrNull<CanvasLayer>("HUD");
                _hudController = hudLayer?.GetNodeOrNull<HudController>("HudController");
            }

            if (DebugInput)
            {
                GD.Print($"[InputHandler] Ready - Arena: {Arena != null}, Camera: {Camera != null}");
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            UpdateHover();
            ProcessCameraInput((float)delta);

            // Update AoE preview if ability selected
            if (!string.IsNullOrEmpty(Arena.SelectedAbilityId) && Camera != null && Arena != null)
            {
                var mousePos = GetViewport().GetMousePosition();
                var from = Camera.ProjectRayOrigin(mousePos);
                var direction = Camera.ProjectRayNormal(mousePos);
                var to = from + direction * RayLength;

                // Raycast to get ground position
                var spaceState = Arena.GetWorld3D().DirectSpaceState;
                var query = PhysicsRayQueryParameters3D.Create(from, to);
                query.CollisionMask = 1; // Ground layer
                query.CollideWithBodies = true;

                var result = spaceState.IntersectRay(query);
                if (result.Count > 0)
                {
                    var targetPos = result["position"].AsVector3();

                    // Convert world position to logical position (identity with TileSize=1)
                    var logicalPos = new Vector3(
                        targetPos.X / Arena.TileSize,
                        0,
                        targetPos.Z / Arena.TileSize
                    );

                    Arena.UpdateAoEPreview(logicalPos);
                }
            }

            // Update movement preview if in movement mode
            if (_currentMode == TargetingMode.Move && Camera != null && Arena != null)
            {
                var mousePos = GetViewport().GetMousePosition();
                var from = Camera.ProjectRayOrigin(mousePos);
                var direction = Camera.ProjectRayNormal(mousePos);
                var to = from + direction * RayLength;

                // Raycast to get ground position
                var spaceState = Arena.GetWorld3D().DirectSpaceState;
                var query = PhysicsRayQueryParameters3D.Create(from, to);
                query.CollisionMask = 1; // Ground layer
                query.CollideWithBodies = true;

                var result = spaceState.IntersectRay(query);
                if (result.Count > 0)
                {
                    var targetPos = result["position"].AsVector3();
                    Arena.UpdateMovementPreview(targetPos);
                }
            }

            // Periodic debug - every 120 frames (2 seconds at 60fps)
            if (DebugInput)
            {
                _debugFrameCounter++;
                if (_debugFrameCounter >= 120)
                {
                    _debugFrameCounter = 0;
                    DebugListCombatants();
                }
            }
        }

        private void DebugListCombatants()
        {
            if (Arena == null) return;

            var combatants = Arena.GetTree().GetNodesInGroup("combatants");
            GD.Print($"[InputHandler] === Combatants in 'combatants' group: {combatants.Count} ===");

            // Also check all Area3D nodes on layer 2
            var allNodes = Arena.GetTree().Root.FindChildren("*", "Area3D", true, false);
            int layer2Count = 0;
            foreach (var node in allNodes)
            {
                if (node is Area3D area && (area.CollisionLayer & 2) != 0)
                {
                    layer2Count++;
                    GD.Print($"[InputHandler]   Area3D on layer 2: {area.Name} at {area.GlobalPosition}, InTree: {area.IsInsideTree()}");
                }
            }
            GD.Print($"[InputHandler] === Total Area3D on layer 2: {layer2Count} ===");
        }

        private void ProcessCameraInput(float delta)
        {
            if (Camera == null || Arena == null) return;

            bool cameraChanged = false;
            bool pointerOverUi = IsPointerOverUi();

            // Pan - shift the look target
            Vector3 panDirection = Vector3.Zero;
            if (Input.IsActionPressed("camera_pan_up")) panDirection.Z -= 1;
            if (Input.IsActionPressed("camera_pan_down")) panDirection.Z += 1;
            if (Input.IsActionPressed("camera_pan_left")) panDirection.X -= 1;
            if (Input.IsActionPressed("camera_pan_right")) panDirection.X += 1;

            if (panDirection != Vector3.Zero)
            {
                panDirection = panDirection.Normalized();
                // Rotate pan direction to match camera yaw
                panDirection = panDirection.Rotated(Vector3.Up, Mathf.DegToRad(Arena.CameraYaw));
                Arena.CameraLookTarget += panDirection * CameraPanSpeed * delta;
                cameraChanged = true;
            }

            // Rotate - orbit around look target
            if (Input.IsActionPressed("camera_rotate_left"))
            {
                Arena.CameraYaw += CameraRotateSpeed * delta;
                cameraChanged = true;
            }
            if (Input.IsActionPressed("camera_rotate_right"))
            {
                Arena.CameraYaw -= CameraRotateSpeed * delta;
                cameraChanged = true;
            }

            // Zoom - move camera closer/farther
            if (!pointerOverUi && Input.IsActionJustPressed("camera_zoom_in"))
            {
                Arena.CameraDistance = Mathf.Max(MinZoom, Arena.CameraDistance - CameraZoomSpeed);
                cameraChanged = true;
            }
            if (!pointerOverUi && Input.IsActionJustPressed("camera_zoom_out"))
            {
                Arena.CameraDistance = Mathf.Min(MaxZoom, Arena.CameraDistance + CameraZoomSpeed);
                cameraChanged = true;
            }

            // Optional: Pitch control (could be added with new input actions)
            // For now, pitch is controlled by arena defaults

            // Update camera position if any control was used
            if (cameraChanged)
            {
                UpdateCameraOrbit();
            }
        }

        private bool IsPointerOverUi()
        {
            var viewport = GetViewport();
            if (viewport == null)
            {
                return false;
            }

            return viewport.GuiGetHoveredControl() != null;
        }

        /// <summary>
        /// Update camera position based on current orbit parameters.
        /// </summary>
        private void UpdateCameraOrbit()
        {
            if (Camera == null || Arena == null) return;

            // Calculate camera position using spherical coordinates
            float pitchRad = Mathf.DegToRad(Arena.CameraPitch);
            float yawRad = Mathf.DegToRad(Arena.CameraYaw);

            float horizontalDist = Arena.CameraDistance * Mathf.Cos(pitchRad);
            float verticalDist = Arena.CameraDistance * Mathf.Sin(pitchRad);

            Vector3 offset = new Vector3(
                horizontalDist * Mathf.Sin(yawRad),
                verticalDist,
                horizontalDist * Mathf.Cos(yawRad)
            );

            Camera.GlobalPosition = Arena.CameraLookTarget + offset;
            Camera.LookAt(Arena.CameraLookTarget, Vector3.Up);
        }

        public override void _Input(InputEvent @event)
        {
            // Only handle keyboard shortcuts here - let UI handle mouse clicks first
            if (@event is InputEventKey)
            {
                if (DebugInput)
                    GD.Print($"[InputHandler] Key event: {(@event as InputEventKey).Keycode}");
            }

            // Inventory toggle available regardless of turn state
            if (Input.IsActionJustPressed("toggle_inventory"))
            {
                _hudController?.ToggleInventory();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!Arena.IsPlayerTurn) return;

            // Use input actions instead of raw keycodes
            if (Input.IsActionJustPressed("combat_end_turn"))
            {
                Arena.EndCurrentTurn();
                GetViewport().SetInputAsHandled();
            }
            else if (Input.IsActionJustPressed("combat_cancel"))
            {
                Arena.ClearSelection();
                GetViewport().SetInputAsHandled();
            }
            else if (Input.IsActionJustPressed("combat_move"))
            {
                // Enter movement mode
                Arena.EnterMovementMode();
                GetViewport().SetInputAsHandled();
            }
            else if (Input.IsActionJustPressed("combat_ability_1"))
            {
                SelectAbilityByIndex(0);
                GetViewport().SetInputAsHandled();
            }
            else if (Input.IsActionJustPressed("combat_ability_2"))
            {
                SelectAbilityByIndex(1);
                GetViewport().SetInputAsHandled();
            }
            else if (Input.IsActionJustPressed("combat_ability_3"))
            {
                SelectAbilityByIndex(2);
                GetViewport().SetInputAsHandled();
            }
            else if (Input.IsActionJustPressed("combat_ability_4"))
            {
                SelectAbilityByIndex(3);
                GetViewport().SetInputAsHandled();
            }
            else if (Input.IsActionJustPressed("combat_ability_5"))
            {
                SelectAbilityByIndex(4);
                GetViewport().SetInputAsHandled();
            }
            else if (Input.IsActionJustPressed("combat_ability_6"))
            {
                SelectAbilityByIndex(5);
                GetViewport().SetInputAsHandled();
            }
            else if (Input.IsActionJustPressed("debug_toggle"))
            {
                // Emit signal or call method to toggle debug panel
                GD.Print("[Input] Debug panel toggle requested");
                GetViewport().SetInputAsHandled();
            }
        }

        // Handle unhandled input (after UI has had a chance to process)
        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Arena.IsPlayerTurn) return;

            if (@event is InputEventMouseButton mouseButton)
            {
                if (DebugInput)
                    GD.Print($"[InputHandler] Unhandled mouse button: {mouseButton.ButtonIndex}, Pressed: {mouseButton.Pressed}, Position: {mouseButton.Position}");

                if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
                {
                    if (DebugInput)
                        GD.Print($"[InputHandler] Handling left click, hovered: {_hoveredVisual?.CombatantId ?? "null"}");

                    // If hover is null, do a one-shot diagnostic raycast on click.
                    if (DebugInput && _hoveredVisual == null)
                    {
                        DebugRaycastOnClick(mouseButton.Position);
                    }
                    HandleLeftClick();
                    GetViewport().SetInputAsHandled();
                }
                else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
                {
                    if (DebugInput)
                        GD.Print("[InputHandler] Handling right click");
                    HandleRightClick();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private void UpdateHover()
        {
            if (Camera == null || Arena == null) return;

            var mousePos = GetViewport().GetMousePosition();
            var from = Camera.ProjectRayOrigin(mousePos);
            var to = from + Camera.ProjectRayNormal(mousePos) * RayLength;

            // Throttle noisy ray prints unless explicitly verbose.
            _rayDebugThrottleCounter++;
            bool allowRayLogThisFrame = DebugRaycastVerbose || _rayDebugThrottleCounter >= Math.Max(1, DebugRaycastThrottleFrames);
            if (allowRayLogThisFrame)
                _rayDebugThrottleCounter = 0;

            _spaceState = Arena.GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollisionMask = CombatantRaycastMask;
            // Combatants are Area3D, so we must enable areas in the query.
            query.CollideWithAreas = true;
            query.CollideWithBodies = true;

            if (DebugInput && allowRayLogThisFrame)
            {
                GD.Print($"[InputHandler] UpdateHover - Mouse: {mousePos}, RayFrom: {from}, RayTo: {to}");
                GD.Print($"[InputHandler] Raycast query - Mask: {query.CollisionMask}, Exclude: {query.Exclude.Count}, Areas: {query.CollideWithAreas}, Bodies: {query.CollideWithBodies}");
            }

            var result = _spaceState.IntersectRay(query);

            bool rayHit = result.Count > 0;
            if (DebugInput && allowRayLogThisFrame)
                GD.Print($"[InputHandler] Raycast result count: {result.Count}");

            CombatantVisual newHover = null;

            if (result.Count > 0)
            {
                var collider = result["collider"].As<Node>();
                var position = result.ContainsKey("position") ? result["position"].AsVector3() : Vector3.Zero;
                if (DebugInput && (DebugRaycastVerbose || !_previousRayHit))
                    GD.Print($"[InputHandler] Raycast HIT: {collider?.Name} (Type: {collider?.GetType().Name}) at {position}");
                if (collider != null)
                {
                    // Walk up the tree to find CombatantVisual
                    var current = collider;
                    while (current != null)
                    {
                        if (DebugInput && DebugRaycastVerbose && current != collider) // Don't double-log the first one
                            GD.Print($"[InputHandler]   Checking parent: {current.Name} (Type: {current.GetType().Name})");
                        if (current is CombatantVisual visual)
                        {
                            newHover = visual;
                            if (DebugInput)
                                GD.Print($"[InputHandler]   -> Found CombatantVisual: {visual.CombatantId}");
                            break;
                        }
                        current = current.GetParent();
                    }

                    if (newHover == null && DebugInput && (DebugRaycastVerbose || !_previousRayHit))
                        GD.Print($"[InputHandler]   -> No CombatantVisual found in parent hierarchy");
                }
            }

            // Log only on miss/hit transitions unless verbose.
            if (DebugInput && !rayHit && (_previousRayHit || DebugRaycastVerbose))
                GD.Print("[InputHandler] Raycast MISS - no collision detected");

            _previousRayHit = rayHit;

            // Update hover state
            if (newHover != _hoveredVisual)
            {
                if (DebugInput)
                    GD.Print($"[InputHandler] Hover changed: {_hoveredVisual?.CombatantId ?? "null"} -> {newHover?.CombatantId ?? "null"}");
                if (_hoveredVisual != null && !_hoveredVisual.IsSelected)
                {
                    // Remove hover highlight and outline
                    _hoveredVisual.SetHovered(false);
                }
                _hoveredVisual = newHover;
                if (_hoveredVisual != null)
                {
                    // Add hover highlight and outline
                    _hoveredVisual.SetHovered(true);
                }
            }

            // Keep hovered targeting preview in sync with current cursor target.
            Arena.UpdateHoveredTargetPreview(_hoveredVisual?.CombatantId);
        }

        private void DebugRaycastOnClick(Vector2 mousePos)
        {
            if (Camera == null || Arena == null) return;

            var from = Camera.ProjectRayOrigin(mousePos);
            var to = from + Camera.ProjectRayNormal(mousePos) * RayLength;
            var spaceState = Arena.GetWorld3D().DirectSpaceState;

            // First: combatant mask, areas enabled.
            var qCombatants = PhysicsRayQueryParameters3D.Create(from, to);
            qCombatants.CollisionMask = CombatantRaycastMask;
            qCombatants.CollideWithAreas = true;
            qCombatants.CollideWithBodies = true;
            var rCombatants = spaceState.IntersectRay(qCombatants);

            GD.Print($"[InputHandler] Click-ray (combatant mask={CombatantRaycastMask}) result count: {rCombatants.Count}");
            if (rCombatants.Count > 0)
            {
                var collider = rCombatants["collider"].As<Node>();
                var hitPos = rCombatants.ContainsKey("position") ? rCombatants["position"].AsVector3() : Vector3.Zero;
                GD.Print($"[InputHandler] Click-ray hit (combatant mask): {collider?.Name} ({collider?.GetType().Name}) at {hitPos}");
                if (collider is CollisionObject3D co)
                    GD.Print($"[InputHandler]   Collider layers: {co.CollisionLayer}, masks: {co.CollisionMask}, input_pickable: {co.InputRayPickable}");
            }

            // Second: all layers, to detect if we're hitting *anything* at all.
            var qAll = PhysicsRayQueryParameters3D.Create(from, to);
            qAll.CollisionMask = uint.MaxValue;
            qAll.CollideWithAreas = true;
            qAll.CollideWithBodies = true;
            var rAll = spaceState.IntersectRay(qAll);

            GD.Print($"[InputHandler] Click-ray (ALL layers) result count: {rAll.Count}");
            if (rAll.Count > 0)
            {
                var collider = rAll["collider"].As<Node>();
                var hitPos = rAll.ContainsKey("position") ? rAll["position"].AsVector3() : Vector3.Zero;
                GD.Print($"[InputHandler] Click-ray hit (ALL): {collider?.Name} ({collider?.GetType().Name}) at {hitPos}");
                if (collider is CollisionObject3D co)
                    GD.Print($"[InputHandler]   Collider layers: {co.CollisionLayer}, masks: {co.CollisionMask}, input_pickable: {co.InputRayPickable}");
            }
        }

        private void HandleLeftClick()
        {
            if (DebugInput)
                GD.Print($"[InputHandler] HandleLeftClick - hoveredVisual: {_hoveredVisual?.CombatantId ?? "null"}, selectedAbility: {Arena.SelectedAbilityId ?? "null"}, mode: {_currentMode}");

            // Handle movement mode
            if (_currentMode == TargetingMode.Move && !string.IsNullOrEmpty(_movingActorId))
            {
                if (DebugInput)
                    GD.Print("[InputHandler] In movement mode, attempting to execute move");

                // Get mouse position in world
                var mousePos = GetViewport().GetMousePosition();
                var from = Camera.ProjectRayOrigin(mousePos);
                var direction = Camera.ProjectRayNormal(mousePos);
                var to = from + direction * RayLength;

                // Raycast to get ground position
                var spaceState = Arena.GetWorld3D().DirectSpaceState;
                var query = PhysicsRayQueryParameters3D.Create(from, to);
                query.CollisionMask = 1; // Ground layer
                query.CollideWithBodies = true;

                var result = spaceState.IntersectRay(query);
                if (result.Count > 0)
                {
                    var targetPos = result["position"].AsVector3();
                    if (DebugInput)
                        GD.Print($"[InputHandler] Executing move to {targetPos}");
                    Arena.ExecuteMovement(_movingActorId, targetPos);
                }

                ExitMovementMode();
                GetViewport().SetInputAsHandled();
                return;
            }

            // Handle AoE ability targeting
            if (!string.IsNullOrEmpty(Arena.SelectedAbilityId))
            {
                var actor = Arena.Context.GetCombatant(Arena.SelectedCombatantId);
                var effectPipeline = Arena.Context.GetService<EffectPipeline>();
                var action = effectPipeline?.GetAction(Arena.SelectedAbilityId);

                if (actor != null && action != null)
                {
                    // Check if this is an AoE ability
                    bool isAoE = action.TargetType == TargetType.Circle ||
                                 action.TargetType == TargetType.Cone ||
                                 action.TargetType == TargetType.Line;

                    if (isAoE)
                    {
                        // Get mouse position in world
                        var mousePos = GetViewport().GetMousePosition();
                        var from = Camera.ProjectRayOrigin(mousePos);
                        var direction = Camera.ProjectRayNormal(mousePos);
                        var to = from + direction * RayLength;

                        // Raycast to get ground position
                        var spaceState = Arena.GetWorld3D().DirectSpaceState;
                        var query = PhysicsRayQueryParameters3D.Create(from, to);
                        query.CollisionMask = 1; // Ground layer
                        query.CollideWithBodies = true;

                        var result = spaceState.IntersectRay(query);
                        if (result.Count > 0)
                        {
                            var targetPos = result["position"].AsVector3();

                            // Convert world position to logical position (identity with TileSize=1)
                            var logicalPos = new Vector3(
                                targetPos.X / Arena.TileSize,
                                0,
                                targetPos.Z / Arena.TileSize
                            );

                            // Validate cast point is within range
                            float distanceToCastPoint = actor.Position.DistanceTo(logicalPos);
                            if (distanceToCastPoint > action.Range)
                            {
                                if (DebugInput)
                                    GD.Print($"[InputHandler] AoE cast point out of range: {distanceToCastPoint:F2} > {action.Range:F2}");
                                GetViewport().SetInputAsHandled();
                                return;
                            }

                            if (DebugInput)
                                GD.Print($"[InputHandler] Executing AoE ability at {logicalPos}");

                            // Execute against the target point - arena resolves affected targets.
                            Arena.ExecuteAbilityAtPosition(
                                Arena.SelectedCombatantId,
                                Arena.SelectedAbilityId,
                                logicalPos,
                                Arena.GetSelectedAbilityOptions()
                            );
                        }

                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
            }

            // Handle self/all/none target abilities - execute on any click
            if (!string.IsNullOrEmpty(Arena.SelectedAbilityId))
            {
                var actor = Arena.Context.GetCombatant(Arena.SelectedCombatantId);
                var effectPipeline = Arena.Context.GetService<EffectPipeline>();
                var action = effectPipeline?.GetAction(Arena.SelectedAbilityId);

                if (actor != null && action != null)
                {
                    // Check if this is a self/all/none target ability
                    if (action.TargetType == TargetType.Self ||
                        action.TargetType == TargetType.All ||
                        action.TargetType == TargetType.None)
                    {
                        if (DebugInput)
                            GD.Print($"[InputHandler] Executing {action.TargetType} ability on any click: {Arena.SelectedAbilityId}");

                        Arena.ExecuteAction(
                            Arena.SelectedCombatantId,
                            Arena.SelectedAbilityId,
                            Arena.GetSelectedAbilityOptions()
                        );
                        GetViewport().SetInputAsHandled();
                        return;
                    }
                }
            }

            if (_hoveredVisual != null)
            {
                if (!string.IsNullOrEmpty(Arena.SelectedAbilityId))
                {
                    if (DebugInput)
                        GD.Print("[InputHandler] In targeting mode, attempting to execute ability");
                    // In targeting mode - try to execute ability
                    var actor = Arena.Context.GetCombatant(Arena.SelectedCombatantId);
                    var target = _hoveredVisual.Entity;

                    if (actor != null && target != null)
                    {
                        // Check if valid target
                        var combatants = Arena.GetCombatants().ToList();
                        var targetValidator = Arena.Context.GetService<QDND.Combat.Targeting.TargetValidator>();
                        var effectPipeline = Arena.Context.GetService<EffectPipeline>();
                        var action = effectPipeline?.GetAction(Arena.SelectedAbilityId);

                        if (action != null && targetValidator != null)
                        {
                            var validTargets = targetValidator.GetValidTargets(action, actor, combatants);
                            if (validTargets.Any(t => t.Id == target.Id))
                            {
                                if (DebugInput)
                                    GD.Print($"[InputHandler] Valid target, executing ability {Arena.SelectedAbilityId} on {target.Id}");
                                Arena.ExecuteAction(
                                    Arena.SelectedCombatantId,
                                    Arena.SelectedAbilityId,
                                    target.Id,
                                    Arena.GetSelectedAbilityOptions()
                                );
                            }
                            else
                            {
                                if (DebugInput)
                                    GD.Print($"[InputHandler] Invalid target for ability");
                            }
                        }
                    }
                }
                else
                {
                    if (DebugInput)
                        GD.Print($"[InputHandler] Selecting combatant: {_hoveredVisual.CombatantId}");
                    // Selection mode - select combatant
                    Arena.SelectCombatant(_hoveredVisual.CombatantId);
                }
            }
            else
            {
                if (DebugInput)
                    GD.Print("[InputHandler] Clicked on empty space, clearing selection");
                // Clicked on empty space
                Arena.ClearSelection();
            }
        }

        private void HandleRightClick()
        {
            if (DebugInput)
                GD.Print("[InputHandler] HandleRightClick - clearing selection");

            // Exit movement mode if active
            if (_currentMode == TargetingMode.Move)
            {
                ExitMovementMode();
                GetViewport().SetInputAsHandled();
                return;
            }

            // Cancel current selection/targeting
            Arena.ClearSelection();
        }

        /// <summary>
        /// Enter movement mode for the specified actor.
        /// </summary>
        public void EnterMovementMode(string actorId)
        {
            _currentMode = TargetingMode.Move;
            _movingActorId = actorId;

            if (DebugInput)
                GD.Print($"[InputHandler] Entered movement mode for {actorId}");
        }

        /// <summary>
        /// Exit movement mode.
        /// </summary>
        public void ExitMovementMode()
        {
            _currentMode = TargetingMode.None;
            _movingActorId = null;
            Arena.ClearMovementPreview();

            if (DebugInput)
                GD.Print("[InputHandler] Exited movement mode");
        }

        private void SelectAbilityByIndex(int index)
        {
            if (Arena?.ActionBarModel == null || string.IsNullOrEmpty(Arena.SelectedCombatantId))
            {
                return;
            }

            var orderedEntries = Arena.ActionBarModel.Actions
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.ActionId))
                .OrderBy(a => a.SlotIndex)
                .ToList();

            if (index >= 0 && index < orderedEntries.Count)
            {
                Arena.SelectAction(orderedEntries[index].ActionId);
            }
        }
    }
}
