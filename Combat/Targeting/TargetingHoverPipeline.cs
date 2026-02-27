using System;
using Godot;
using QDND.Combat.Arena;
using QDND.Combat.Entities;

namespace QDND.Combat.Targeting;

/// <summary>
/// Performs per-frame cursor raycasting to produce <see cref="HoverData"/> for the targeting system.
/// Priority: Character collider (Area3D, layer 2) > Ground (StaticBody3D, layer 1).
/// Replaces the hover detection previously scattered across CombatInputHandler.
/// </summary>
public class TargetingHoverPipeline
{
    private const uint GROUND_LAYER = 1;   // Collision layer 1
    private const uint ENTITY_LAYER = 2;   // Collision layer 2 (Area3D combat entities)
    private const float RAY_LENGTH = 100f; // metres

    private readonly Func<string, Combatant> _getCombatant;

    /// <summary>
    /// Creates a new hover pipeline.
    /// </summary>
    /// <param name="getCombatant">
    /// Lookup function that resolves a combatant ID to a <see cref="Combatant"/>.
    /// Keeps the pipeline decoupled from the arena / registry.
    /// </param>
    public TargetingHoverPipeline(Func<string, Combatant> getCombatant)
    {
        _getCombatant = getCombatant ?? throw new ArgumentNullException(nameof(getCombatant));
    }

    /// <summary>
    /// Updates the hover state based on the current mouse position.
    /// Call this once per physics frame.
    /// </summary>
    /// <param name="camera">The active <see cref="Camera3D"/>.</param>
    /// <param name="mousePosition">Viewport-space mouse position (e.g. from <c>GetViewport().GetMousePosition()</c>).</param>
    /// <returns>A fully-populated <see cref="HoverData"/> snapshot for this frame.</returns>
    public HoverData Update(Camera3D camera, Vector2 mousePosition)
    {
        var from = camera.ProjectRayOrigin(mousePosition);
        var dir = camera.ProjectRayNormal(mousePosition);
        var to = from + dir * RAY_LENGTH;

        var spaceState = camera.GetWorld3D().DirectSpaceState;

        var result = new HoverData();

        // --- 1. Entity raycast (Area3D on layer 2) ---
        var entityParams = PhysicsRayQueryParameters3D.Create(from, to);
        entityParams.CollisionMask = ENTITY_LAYER;
        entityParams.CollideWithAreas = true;
        entityParams.CollideWithBodies = false;

        var entityHit = spaceState.IntersectRay(entityParams);

        if (entityHit.Count > 0)
        {
            var collider = entityHit["collider"].AsGodotObject();
            var visual = FindCombatantVisual(collider);

            if (visual != null)
            {
                result.HoveredEntityId = visual.CombatantId;
                result.HoveredCombatant = _getCombatant(visual.CombatantId);
            }
        }

        // --- 2. Ground raycast (StaticBody3D on layer 1) — always performed ---
        var groundParams = PhysicsRayQueryParameters3D.Create(from, to);
        groundParams.CollisionMask = GROUND_LAYER;
        groundParams.CollideWithAreas = false;
        groundParams.CollideWithBodies = true;

        var groundHit = spaceState.IntersectRay(groundParams);

        if (groundHit.Count > 0)
        {
            result.CursorWorldPoint = groundHit["position"].AsVector3();
            result.SurfaceNormal = groundHit["normal"].AsVector3();
            result.IsGroundHit = true;
        }
        else
        {
            // Fallback: intersect with the y=0 ground plane so we always have a cursor world point.
            var fallback = ProjectOntoGroundPlane(from, dir);
            if (fallback.HasValue)
            {
                result.CursorWorldPoint = fallback.Value;
                result.SurfaceNormal = Vector3.Up;
                result.IsGroundHit = false;
            }
            else if (result.HoveredEntityId != null && result.HoveredCombatant != null)
            {
                // Last resort: project the hovered entity's position onto y=0.
                // CombatantVisual is an Area3D, but we only have the combatant reference —
                // use the entity hit position projected down.
                var entityPos = entityHit.Count > 0
                    ? ((Vector3)entityHit["position"]) with { Y = 0f }
                    : Vector3.Zero;
                result.CursorWorldPoint = entityPos;
                result.SurfaceNormal = Vector3.Up;
                result.IsGroundHit = false;
            }
        }

        return result;
    }

    /// <summary>
    /// Walks up from a physics collider to find a <see cref="CombatantVisual"/> ancestor.
    /// Returns <c>null</c> if the collider isn't part of a combatant visual hierarchy.
    /// </summary>
    private static CombatantVisual FindCombatantVisual(GodotObject collider)
    {
        if (collider is CombatantVisual direct)
            return direct;

        // The collider might be a CollisionShape3D child — walk up the tree.
        if (collider is Node node)
        {
            var current = node.GetParent();
            while (current != null)
            {
                if (current is CombatantVisual ancestor)
                    return ancestor;
                current = current.GetParent();
            }
        }

        return null;
    }

    /// <summary>
    /// Intersects a ray with the y=0 horizontal plane.
    /// Returns <c>null</c> if the ray is parallel to the ground (dir.Y ≈ 0).
    /// </summary>
    private static Vector3? ProjectOntoGroundPlane(Vector3 from, Vector3 dir, float groundY = 0f)
    {
        // Avoid division by zero when the ray is nearly parallel to the ground.
        if (Mathf.IsZeroApprox(dir.Y))
            return null;

        float t = (groundY - from.Y) / dir.Y;

        // Only accept forward intersections.
        if (t < 0f)
            return null;

        return from + dir * t;
    }
}
