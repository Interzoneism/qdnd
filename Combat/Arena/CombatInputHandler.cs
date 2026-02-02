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
        }

        public override void _Input(InputEvent @event)
        {
            if (!Arena.IsPlayerTurn) return;

            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
                {
                    HandleLeftClick();
                    GetViewport().SetInputAsHandled();
                }
                else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
                {
                    HandleRightClick();
                    GetViewport().SetInputAsHandled();
                }
            }
            
            // Keyboard shortcuts
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                switch (keyEvent.Keycode)
                {
                    case Key.Space:
                    case Key.Enter:
                        Arena.EndCurrentTurn();
                        break;
                    case Key.Escape:
                        Arena.ClearSelection();
                        break;
                    // Number keys for ability selection
                    case Key.Key1:
                        SelectAbilityByIndex(0);
                        break;
                    case Key.Key2:
                        SelectAbilityByIndex(1);
                        break;
                    case Key.Key3:
                        SelectAbilityByIndex(2);
                        break;
                    case Key.Key4:
                        SelectAbilityByIndex(3);
                        break;
                    case Key.Key5:
                        SelectAbilityByIndex(4);
                        break;
                    case Key.Key6:
                        SelectAbilityByIndex(5);
                        break;
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
                GD.Print($"Raycast hit: {collider?.Name} (Type: {collider?.GetType().Name})");
                if (collider != null)
                {
                    // Walk up the tree to find CombatantVisual
                    var current = collider;
                    while (current != null)
                    {
                        GD.Print($"  Checking: {current.Name} (Type: {current.GetType().Name})");
                        if (current is CombatantVisual visual)
                        {
                            newHover = visual;
                            GD.Print($"  -> Found CombatantVisual: {visual.CombatantId}");
                            break;
                        }
                        current = current.GetParent();
                    }
                }
            }
            
            // Update hover state
            if (newHover != _hoveredVisual)
            {
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
            if (_hoveredVisual != null)
            {
                if (!string.IsNullOrEmpty(Arena.SelectedAbilityId))
                {
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
                                Arena.ExecuteAbility(Arena.SelectedCombatantId, Arena.SelectedAbilityId, target.Id);
                            }
                        }
                    }
                }
                else
                {
                    // Selection mode - select combatant
                    Arena.SelectCombatant(_hoveredVisual.CombatantId);
                }
            }
            else
            {
                // Clicked on empty space
                Arena.ClearSelection();
            }
        }

        private void HandleRightClick()
        {
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
