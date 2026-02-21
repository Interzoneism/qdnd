using Godot;
using QDND.Combat.Entities;
using QDND.Combat.UI;
using QDND.Combat.UI.Panels;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Action Editor scene — a modified combat arena used to create, edit, and test actions/spells.
    /// Enemy always skips its turn. No victory conditions. Player hotbar starts empty.
    /// </summary>
    public partial class ActionEditorArena : CombatArena
    {
        // The editor panel — added to HUD after base _Ready
        private ActionEditorPanel _actionEditorPanel;

        public override void _Ready()
        {
            base._Ready();
            // Deferred so HUD is fully initialized before we add the panel
            CallDeferred(nameof(AddActionEditorPanel));
        }

        private void AddActionEditorPanel()
        {
            // Find the CanvasLayer named "HUD"
            CanvasLayer hud = null;
            foreach (var child in GetChildren())
            {
                if (child is CanvasLayer cl)
                {
                    hud = cl;
                    break;
                }
            }

            if (hud == null)
            {
                GD.PrintErr("[ActionEditorArena] Could not find HUD CanvasLayer");
                return;
            }

            _actionEditorPanel = new ActionEditorPanel(this);
            _actionEditorPanel.Name = "ActionEditorPanel";
            hud.AddChild(_actionEditorPanel);

            // Position: right side of screen, below initiative ribbon
            _actionEditorPanel.Size = new Vector2(560, 700);
            _actionEditorPanel.SetScreenPosition(new Vector2(
                DisplayServer.WindowGetSize().X - 580, 110));

            GD.Print("[ActionEditorArena] Action Editor panel added.");
        }

        /// <summary>
        /// After each turn begins:
        /// - If player: clear the hotbar (player manages it manually via Action Editor)
        /// - If enemy dummy: immediately end the turn (and revive if dead)
        /// </summary>
        protected override void OnAfterBeginTurn(Combatant combatant)
        {
            if (combatant.Faction == Faction.Player || combatant.Faction == Faction.Ally)
            {
                // Clear the hotbar — player populates it via drag-from-editor
                ActionBarModel?.SetActions(new List<ActionBarEntry>());
                GD.Print("[ActionEditorArena] Player turn: hotbar cleared.");
            }
            else
            {
                // Enemy always skips its turn immediately
                GD.Print($"[ActionEditorArena] Dummy '{combatant.Name}' skipping turn immediately.");

                // Revive dummy if it somehow died (immortal training dummy)
                if (combatant.LifeState != CombatantLifeState.Alive)
                {
                    combatant.Resources.CurrentHP = combatant.Resources.MaxHP;
                    combatant.LifeState = CombatantLifeState.Alive;
                    combatant.ResetDeathSaves();
                    GD.Print($"[ActionEditorArena] Revived dummy '{combatant.Name}'.");
                }

                // Call EndCurrentTurn after a brief delay so state machine is settled
                GetTree().CreateTimer(0.1).Timeout += () =>
                {
                    if (!IsQueuedForDeletion())
                        EndCurrentTurn();
                };
            }
        }

        /// <summary>Disable victory/defeat — this scene runs forever.</summary>
        protected override bool ShouldAllowVictory() => false;
    }
}
