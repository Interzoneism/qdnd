using Godot;
using System;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Abilities;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Handles player input for combat: selection, targeting, and camera control.
    /// </summary>
    public partial class CombatInputHandler : Node
    {
        [Export] public CombatArena Arena;
        [Export] public Camera3D Camera;
        [Export] public float RayLength = 100f;
        [Export] public bool DebugInput = true;
        
        // Camera control settings
        [Export] public float CameraPanSpeed = 10f;
        [Export] public float CameraRotateSpeed = 60f; // degrees per second
        [Export] public float CameraZoomSpeed = 2f;
        [Export] public float MinZoom = 5f;
        [Export] public float MaxZoom = 30f;

        private float _cameraDistance = 15f;
        private float _cameraAngle = 0f; // rotation around Y axis
        
        private CombatantVisual _hoveredVisual;
        private PhysicsDirectSpaceState3D _spaceState;

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
        }

        public override void _PhysicsProcess(double delta)
        {
            UpdateHover();
            ProcessCameraInput((float)delta);
        }

        private void ProcessCameraInput(float delta)
        {
            if (Camera == null || Arena == null) return;
            
            // Pan
            Vector3 panDirection = Vector3.Zero;
            if (Input.IsActionPressed("camera_pan_up")) panDirection.Z -= 1;
            if (Input.IsActionPressed("camera_pan_down")) panDirection.Z += 1;
            if (Input.IsActionPressed("camera_pan_left")) panDirection.X -= 1;
            if (Input.IsActionPressed("camera_pan_right")) panDirection.X += 1;
            
            if (panDirection != Vector3.Zero)
            {
                panDirection = panDirection.Normalized();
                // Rotate pan direction to match camera facing
                panDirection = panDirection.Rotated(Vector3.Up, Mathf.DegToRad(_cameraAngle));
                Camera.Position += panDirection * CameraPanSpeed * delta;
            }
            
            // Rotate
            if (Input.IsActionPressed("camera_rotate_left"))
            {
                _cameraAngle += CameraRotateSpeed * delta;
                UpdateCameraRotation();
            }
            if (Input.IsActionPressed("camera_rotate_right"))
            {
                _cameraAngle -= CameraRotateSpeed * delta;
                UpdateCameraRotation();
            }
            
            // Zoom
            if (Input.IsActionJustPressed("camera_zoom_in"))
            {
                _cameraDistance = Mathf.Max(MinZoom, _cameraDistance - CameraZoomSpeed);
                UpdateCameraZoom();
            }
            if (Input.IsActionJustPressed("camera_zoom_out"))
            {
                _cameraDistance = Mathf.Min(MaxZoom, _cameraDistance + CameraZoomSpeed);
                UpdateCameraZoom();
            }
        }

        private void UpdateCameraRotation()
        {
            if (Camera == null) return;
            // Rotate camera around current focus point
            var angle = Mathf.DegToRad(_cameraAngle);
            // Maintain height and distance, just rotate horizontally
            Camera.Rotation = new Vector3(Camera.Rotation.X, angle, Camera.Rotation.Z);
        }

        private void UpdateCameraZoom()
        {
            if (Camera == null) return;
            // Adjust camera FOV based on distance
            Camera.Fov = Mathf.Clamp(50f * (15f / _cameraDistance), 30f, 70f);
        }

        public override void _Input(InputEvent @event)
        {
            // Only handle keyboard shortcuts here - let UI handle mouse clicks first
            if (@event is InputEventKey)
            {
                if (DebugInput)
                    GD.Print($"[InputHandler] Key event: {(@event as InputEventKey).Keycode}");
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
            
            _spaceState = Arena.GetWorld3D().DirectSpaceState;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollisionMask = 2; // Layer 2 for combatants
            var result = _spaceState.IntersectRay(query);
            
            CombatantVisual newHover = null;
            
            if (result.Count > 0)
            {
                var collider = result["collider"].As<Node>();
                if (DebugInput)
                    GD.Print($"[InputHandler] Raycast hit: {collider?.Name} (Type: {collider?.GetType().Name})");
                if (collider != null)
                {
                    // Walk up the tree to find CombatantVisual
                    var current = collider;
                    while (current != null)
                    {
                        if (DebugInput && current != collider) // Don't double-log the first one
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
                }
            }
            
            // Update hover state
            if (newHover != _hoveredVisual)
            {
                if (DebugInput)
                    GD.Print($"[InputHandler] Hover changed: {_hoveredVisual?.CombatantId ?? "null"} -> {newHover?.CombatantId ?? "null"}");
                if (_hoveredVisual != null && !_hoveredVisual.IsSelected)
                {
                    // Remove hover highlight
                }
                _hoveredVisual = newHover;
                if (_hoveredVisual != null)
                {
                    // Add hover highlight
                }
            }
        }

        private void HandleLeftClick()
        {
            if (DebugInput)
                GD.Print($"[InputHandler] HandleLeftClick - hoveredVisual: {_hoveredVisual?.CombatantId ?? "null"}, selectedAbility: {Arena.SelectedAbilityId ?? "null"}");
                
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
                        var ability = effectPipeline?.GetAbility(Arena.SelectedAbilityId);
                        
                        if (ability != null && targetValidator != null)
                        {
                            var validTargets = targetValidator.GetValidTargets(ability, actor, combatants);
                            if (validTargets.Any(t => t.Id == target.Id))
                            {
                                if (DebugInput)
                                    GD.Print($"[InputHandler] Valid target, executing ability {Arena.SelectedAbilityId} on {target.Id}");
                                Arena.ExecuteAbility(Arena.SelectedCombatantId, Arena.SelectedAbilityId, target.Id);
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
            // Cancel current selection/targeting
            Arena.ClearSelection();
        }

        private void SelectAbilityByIndex(int index)
        {
            if (string.IsNullOrEmpty(Arena.SelectedCombatantId)) return;
            
            var abilities = Arena.GetAbilitiesForCombatant(Arena.SelectedCombatantId);
            if (index >= 0 && index < abilities.Count)
            {
                Arena.SelectAbility(abilities[index].Id);
            }
        }
    }
}
