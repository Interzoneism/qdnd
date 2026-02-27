using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Rules;
using QDND.Combat.States;
using QDND.Combat.Targeting.Modes;
using QDND.Combat.Targeting.Visuals;

namespace QDND.Combat.Targeting;

/// <summary>
/// Developer showcase scene that demonstrates all 12 targeting modes with
/// a fixed camera and canned setups. Attach to a <see cref="Node3D"/> in the
/// editor, press 1–9 / 0 / Q / W to cycle through modes.
/// <para>
/// This is a self-contained debug tool — it does <b>not</b> require the full
/// <see cref="Arena.CombatArena"/> or turn infrastructure.
/// </para>
/// </summary>
[GlobalClass]
public partial class TargetingShowcase : Node3D
{
    // ── Configuration ────────────────────────────────────────────────

    /// <summary>World-space position of the "caster" dummy.</summary>
    private static readonly Vector3 CasterPos = new(0f, 0f, 0f);

    /// <summary>Fixed camera look-at target (center of the arena).</summary>
    private static readonly Vector3 CameraTarget = new(0f, 0f, -2f);

    /// <summary>Fixed camera position (isometric-ish overhead).</summary>
    private static readonly Vector3 CameraOffset = new(0f, 14f, 10f);

    // ── Scene nodes ──────────────────────────────────────────────────

    private Camera3D _camera;
    private Label _infoLabel;
    private TargetingVisualSystem _visualSystem;

    // ── Targeting infrastructure ─────────────────────────────────────

    private CombatStateMachine _stateMachine;
    private TargetingSystem _targetingSystem;
    private TargetingHoverPipeline _hoverPipeline;

    // ── Shared services ──────────────────────────────────────────────

    private TargetValidator _validator;
    private LOSService _los;
    private RulesEngine _rules;

    // ── Dummy data ───────────────────────────────────────────────────

    private Combatant _caster;
    private readonly Dictionary<string, Combatant> _combatants = new();
    private readonly Dictionary<string, MeshInstance3D> _dummyVisuals = new();
    private readonly List<Node3D> _obstacleNodes = new();

    // ── Current scenario ─────────────────────────────────────────────

    private int _activeScenario = -1;

    /// <summary>
    /// Scenario metadata: key binding, display name, builder delegate, and
    /// the <see cref="ActionDefinition"/> to use.
    /// </summary>
    private record struct ScenarioDef(
        Key Hotkey,
        string Label,
        Action SetupAction,
        Func<ActionDefinition> ActionFactory);

    private ScenarioDef[] _scenarios;

    // ══════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ══════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        BuildSceneGeometry();
        BuildCamera();
        BuildHUD();
        BuildTargetingInfrastructure();
        BuildScenarioTable();
        ActivateScenario(0);
    }

    public override void _Process(double delta)
    {
        // Feed hover data into the targeting system every frame.
        if (_targetingSystem.CurrentPhase != TargetingPhase.Inactive)
        {
            var mousePos = GetViewport().GetMousePosition();
            var hover = _hoverPipeline.Update(_camera, mousePos);
            _targetingSystem.UpdateFrame(hover);
            _visualSystem.Render(
                _targetingSystem.CurrentPreview,
                _camera,
                _ => null); // No CombatantVisual nodes in showcase
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        // Scenario hotkeys
        for (int i = 0; i < _scenarios.Length; i++)
        {
            if (key.Keycode == _scenarios[i].Hotkey)
            {
                ActivateScenario(i);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // LMB confirm / RMB cancel are handled by mouse events below
        if (key.Keycode == Key.Escape)
        {
            _targetingSystem.HandleEscapeCancel();
            UpdateLabel("Targeting cancelled. Press a key to select a mode.");
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        // Intentionally left empty — input handled in _UnhandledInput.
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                var mousePos = GetViewport().GetMousePosition();
                var hover = _hoverPipeline.Update(_camera, mousePos);
                _targetingSystem.HandleConfirm(hover);
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                _targetingSystem.HandleCancel();
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Scene Construction
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Creates ground plane and ambient lighting.</summary>
    private void BuildSceneGeometry()
    {
        // Ground plane (StaticBody3D on collision layer 1)
        var ground = new StaticBody3D();
        ground.Name = "Ground";
        ground.CollisionLayer = 1;
        ground.CollisionMask = 0;

        var groundShape = new CollisionShape3D();
        var box = new BoxShape3D();
        box.Size = new Vector3(40f, 0.1f, 40f);
        groundShape.Shape = box;
        groundShape.Position = new Vector3(0f, -0.05f, 0f);
        ground.AddChild(groundShape);

        var groundMesh = new MeshInstance3D();
        var planeMesh = new PlaneMesh();
        planeMesh.Size = new Vector2(40f, 40f);
        groundMesh.Mesh = planeMesh;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.25f, 0.28f, 0.22f); // dark earthy green
        groundMesh.MaterialOverride = mat;
        ground.AddChild(groundMesh);

        AddChild(ground);

        // 1-metre grid overlay for visual reference
        var gridMesh = new MeshInstance3D();
        var grid = new PlaneMesh();
        grid.Size = new Vector2(40f, 40f);
        grid.SubdivideWidth = 40;
        grid.SubdivideDepth = 40;
        gridMesh.Mesh = grid;
        gridMesh.Position = new Vector3(0f, 0.01f, 0f);

        var gridMat = new StandardMaterial3D();
        gridMat.AlbedoColor = new Color(1f, 1f, 1f, 0.08f);
        gridMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        gridMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        gridMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        gridMesh.MaterialOverride = gridMat;
        AddChild(gridMesh);

        // Directional light
        var light = new DirectionalLight3D();
        light.Rotation = new Vector3(Mathf.DegToRad(-55f), Mathf.DegToRad(30f), 0f);
        light.LightEnergy = 0.9f;
        light.ShadowEnabled = true;
        AddChild(light);

        // Ambient fill (WorldEnvironment)
        var env = new Godot.Environment();
        env.AmbientLightColor = new Color(0.35f, 0.35f, 0.4f);
        env.AmbientLightEnergy = 0.6f;
        env.BackgroundMode = Godot.Environment.BGMode.Color;
        env.BackgroundColor = new Color(0.12f, 0.12f, 0.15f);

        var worldEnv = new WorldEnvironment();
        worldEnv.Environment = env;
        AddChild(worldEnv);
    }

    /// <summary>Creates a fixed overhead camera.</summary>
    private void BuildCamera()
    {
        _camera = new Camera3D();
        _camera.Name = "ShowcaseCamera";
        _camera.Position = CameraTarget + CameraOffset;
        _camera.LookAt(CameraTarget, Vector3.Up);
        _camera.Current = true;
        AddChild(_camera);
    }

    /// <summary>Creates a CanvasLayer with a Label for mode info and key hints.</summary>
    private void BuildHUD()
    {
        var canvas = new CanvasLayer();
        canvas.Name = "ShowcaseHUD";

        _infoLabel = new Label();
        _infoLabel.Name = "InfoLabel";
        _infoLabel.Position = new Vector2(16, 16);
        _infoLabel.Size = new Vector2(700, 300);
        _infoLabel.AddThemeColorOverride("font_color", Colors.White);
        _infoLabel.AddThemeFontSizeOverride("font_size", 16);
        _infoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _infoLabel.Text = "Targeting Showcase — loading…";

        // Semi-transparent background panel behind the label
        var panel = new PanelContainer();
        panel.Position = new Vector2(8, 8);
        panel.Size = new Vector2(720, 320);
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0f, 0f, 0f, 0.6f);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(10);
        panel.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(_infoLabel);

        canvas.AddChild(panel);
        AddChild(canvas);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Targeting Infrastructure
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates the <see cref="TargetingSystem"/>, <see cref="TargetingHoverPipeline"/>,
    /// <see cref="TargetingVisualSystem"/> and all 12 mode instances.
    /// </summary>
    private void BuildTargetingInfrastructure()
    {
        // Shared services ---------------------------------------------------
        _los = new LOSService();
        _rules = new RulesEngine(seed: 42);
        _validator = new TargetValidator(_los, c => c.Position);

        // The "caster" combatant (always reused) ----------------------------
        _caster = new Combatant("caster", "Showcase Caster", Faction.Player, maxHP: 100, initiative: 20);
        _caster.Position = CasterPos;
        _combatants["caster"] = _caster;

        // Helper lambdas for mode injection ---------------------------------
        Combatant getCombatant(string id) =>
            _combatants.TryGetValue(id, out var c) ? c : null;
        List<Combatant> getAllCombatants() => new(_combatants.Values);
        Vector3 getPosition(Combatant c) => c.Position;
        PhysicsDirectSpaceState3D getSpaceState() =>
            GetWorld3D()?.DirectSpaceState;

        // Instantiate all 12 modes ------------------------------------------
        var modes = new Dictionary<TargetingModeType, ITargetingMode>
        {
            [TargetingModeType.SingleTarget] = new SingleTargetMode(
                _validator, _rules, _los, getCombatant, getAllCombatants),

            [TargetingModeType.FreeAimGround] = new FreeAimGroundMode(
                _validator, _los),

            [TargetingModeType.MultiTarget] = new MultiTargetMode(
                _validator, _rules, _los, getCombatant, getAllCombatants),

            [TargetingModeType.Chain] = new ChainMode(
                _validator, getCombatant, getAllCombatants),

            [TargetingModeType.AoECircle] = new AoECircleMode(
                _validator, _los, getCombatant, getAllCombatants, getPosition),

            [TargetingModeType.AoECone] = new AoEConeMode(
                _validator, _los, getCombatant, getAllCombatants, getPosition),

            [TargetingModeType.AoELine] = new AoELineMode(
                _validator, _los, getCombatant, getAllCombatants, getPosition),

            [TargetingModeType.AoEWall] = new AoEWallMode(
                _validator, _los, getCombatant, getAllCombatants),

            [TargetingModeType.StraightLine] = new StraightLineMode(
                _validator, _los, getSpaceState, getCombatant, getAllCombatants),

            [TargetingModeType.BallisticArc] = new BallisticArcMode(
                _validator, _los, getSpaceState, getCombatant),

            [TargetingModeType.BezierCurve] = new BezierCurveMode(
                _validator, _los, getSpaceState, getCombatant),

            [TargetingModeType.PathfindProjectile] = new PathfindProjectileMode(
                _validator, _los, getCombatant),
        };

        // Orchestrator ------------------------------------------------------
        _stateMachine = new CombatStateMachine();
        _targetingSystem = new TargetingSystem(_stateMachine, modes);

        _targetingSystem.OnTargetingConfirmed += result =>
            GD.Print($"[Showcase] Confirmed: {result.Outcome} target={result.TargetEntityId} pos={result.TargetPosition}");
        _targetingSystem.OnTargetingCancelled += () =>
            GD.Print("[Showcase] Targeting cancelled.");

        // Hover pipeline ----------------------------------------------------
        _hoverPipeline = new TargetingHoverPipeline(getCombatant);

        // Visual system (Node3D — needs to be in the tree) ------------------
        _visualSystem = new TargetingVisualSystem();
        _visualSystem.Name = "TargetingVisualSystem";
        AddChild(_visualSystem);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Scenario Table
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Builds the array of 12 canned scenarios keyed to hotkeys.</summary>
    private void BuildScenarioTable()
    {
        _scenarios = new ScenarioDef[]
        {
            new(Key.Key1, "1 — SingleTarget",
                SetupSingleTarget, ActionSingleTarget),

            new(Key.Key2, "2 — FreeAimGround",
                SetupFreeAimGround, ActionFreeAimGround),

            new(Key.Key3, "3 — MultiTarget (N=3)",
                SetupMultiTarget, ActionMultiTarget),

            new(Key.Key4, "4 — Chain (bounces=3)",
                SetupChain, ActionChain),

            new(Key.Key5, "5 — AoECircle (Fireball)",
                SetupAoECircle, ActionAoECircle),

            new(Key.Key6, "6 — AoECone (Burning Hands)",
                SetupAoECone, ActionAoECone),

            new(Key.Key7, "7 — AoELine (Lightning Bolt)",
                SetupAoELine, ActionAoELine),

            new(Key.Key8, "8 — AoEWall (Wall of Fire)",
                SetupAoEWall, ActionAoEWall),

            new(Key.Key9, "9 — StraightLine",
                SetupStraightLine, ActionStraightLine),

            new(Key.Key0, "0 — BallisticArc",
                SetupBallisticArc, ActionBallisticArc),

            new(Key.Q, "Q — BezierCurve",
                SetupBezierCurve, ActionBezierCurve),

            new(Key.W, "W — PathfindProjectile",
                SetupPathfindProjectile, ActionPathfindProjectile),
        };
    }

    // ══════════════════════════════════════════════════════════════════
    //  Scenario Activation
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Tears down the previous scenario and activates a new one.</summary>
    private void ActivateScenario(int index)
    {
        if (index < 0 || index >= _scenarios.Length) return;

        // End any current targeting
        _targetingSystem.ForceEnd();
        _visualSystem.ClearAll();

        // Clean up previous dummies and obstacles
        ClearDummies();
        ClearObstacles();

        // Ensure caster stays registered
        _combatants.Clear();
        _combatants["caster"] = _caster;

        _activeScenario = index;
        var scenario = _scenarios[index];

        // Populate scene with scenario-specific geometry and combatants
        scenario.SetupAction();

        // Place caster dummy visual
        EnsureDummyVisual("caster", CasterPos, Colors.CornflowerBlue, "Caster");

        // Build the action definition and begin targeting
        var action = scenario.ActionFactory();
        _targetingSystem.BeginTargeting(action.Id, action, _caster, CasterPos);

        UpdateLabel(
            $"[b]{scenario.Label}[/b]\n" +
            $"Action: {action.Name}  |  Range: {action.Range}m  |  TargetType: {action.TargetType}\n" +
            "LMB = confirm  |  RMB = cancel/undo  |  Esc = cancel\n\n" +
            BuildKeyHints());
    }

    /// <summary>Generates the key hint footer string.</summary>
    private string BuildKeyHints()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("MODES:");
        foreach (var s in _scenarios)
        {
            string marker = s.Label == _scenarios[_activeScenario].Label ? " ◄" : "";
            sb.AppendLine($"  [{s.Hotkey}] {s.Label}{marker}");
        }
        return sb.ToString();
    }

    private void UpdateLabel(string text)
    {
        if (_infoLabel != null)
            _infoLabel.Text = text;
    }

    // ══════════════════════════════════════════════════════════════════
    //  Dummy Management
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a colored capsule MeshInstance3D to represent a combatant in the
    /// scene. Also registers the combatant in the shared dictionaries.
    /// </summary>
    private MeshInstance3D EnsureDummyVisual(string id, Vector3 position, Color color, string label)
    {
        if (_dummyVisuals.TryGetValue(id, out var existing))
            return existing;

        // Backend data
        if (!_combatants.ContainsKey(id))
        {
            var faction = color == Colors.CornflowerBlue ? Faction.Player
                : color.R > 0.7f && color.G < 0.5f ? Faction.Hostile
                : Faction.Ally;
            var c = new Combatant(id, label, faction, maxHP: 50, initiative: 10);
            c.Position = position;
            _combatants[id] = c;
            _los.RegisterCombatant(c);
        }

        // Visual capsule
        var mesh = new MeshInstance3D();
        mesh.Name = $"Dummy_{id}";
        var capsule = new CapsuleMesh();
        capsule.Radius = 0.3f;
        capsule.Height = 1.6f;
        mesh.Mesh = capsule;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = color;
        mat.EmissionEnabled = true;
        mat.Emission = color * 0.3f;
        mesh.MaterialOverride = mat;
        mesh.Position = position + new Vector3(0f, 0.8f, 0f); // center capsule at feet

        // Floating label
        var lbl = new Label3D();
        lbl.Text = label;
        lbl.FontSize = 32;
        lbl.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        lbl.Position = new Vector3(0f, 1.2f, 0f);
        lbl.OutlineSize = 4;
        lbl.Modulate = color;
        mesh.AddChild(lbl);

        AddChild(mesh);
        _dummyVisuals[id] = mesh;
        return mesh;
    }

    /// <summary>Creates a simple box obstacle in the scene.</summary>
    private void SpawnObstacle(string name, Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D();
        body.Name = name;
        body.CollisionLayer = 1;

        var shape = new CollisionShape3D();
        var box = new BoxShape3D();
        box.Size = size;
        shape.Shape = box;
        body.AddChild(shape);

        var mesh = new MeshInstance3D();
        var boxMesh = new BoxMesh();
        boxMesh.Size = size;
        mesh.Mesh = boxMesh;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.45f, 0.4f, 0.35f);
        mesh.MaterialOverride = mat;
        body.AddChild(mesh);

        body.Position = position;
        AddChild(body);
        _obstacleNodes.Add(body);
    }

    /// <summary>Removes all dummy visuals from the scene tree.</summary>
    private void ClearDummies()
    {
        foreach (var kvp in _dummyVisuals)
        {
            kvp.Value.QueueFree();
        }
        _dummyVisuals.Clear();
    }

    /// <summary>Removes all obstacles from the scene tree.</summary>
    private void ClearObstacles()
    {
        foreach (var node in _obstacleNodes)
        {
            node.QueueFree();
        }
        _obstacleNodes.Clear();
    }

    // ══════════════════════════════════════════════════════════════════
    //  Scenario Setups — each populates the scene for a specific mode
    // ══════════════════════════════════════════════════════════════════

    // Helper color palette
    private static readonly Color EnemyRed = new(0.9f, 0.25f, 0.2f);
    private static readonly Color AllyBlue = new(0.3f, 0.6f, 0.95f);
    private static readonly Color NeutralGrey = new(0.6f, 0.6f, 0.6f);

    // ── 1. SingleTarget ──────────────────────────────────────────────

    private void SetupSingleTarget()
    {
        EnsureDummyVisual("en1", new Vector3(-3f, 0f, -3f), EnemyRed, "Goblin");
        EnsureDummyVisual("en2", new Vector3(2f, 0f, -5f), EnemyRed, "Orc (far)");
        EnsureDummyVisual("en3", new Vector3(4f, 0f, -2f), EnemyRed, "Kobold");
        EnsureDummyVisual("al1", new Vector3(-2f, 0f, -1f), AllyBlue, "Cleric");
        // Obstacle blocking LoS to en2
        SpawnObstacle("Wall_LoS", new Vector3(1f, 1f, -4f), new Vector3(3f, 2f, 0.3f));
    }

    private static ActionDefinition ActionSingleTarget() => new()
    {
        Id = "showcase_single",
        Name = "Guiding Bolt",
        TargetType = TargetType.SingleUnit,
        TargetFilter = TargetFilter.Enemies,
        Range = 18f,
    };

    // ── 2. FreeAimGround ────────────────────────────────────────────

    private void SetupFreeAimGround()
    {
        // Open ground — just some reference markers
        EnsureDummyVisual("en1", new Vector3(3f, 0f, -6f), EnemyRed, "Enemy A");
        EnsureDummyVisual("en2", new Vector3(-4f, 0f, -8f), EnemyRed, "Enemy B");
    }

    private static ActionDefinition ActionFreeAimGround() => new()
    {
        Id = "showcase_freeaim",
        Name = "Misty Step",
        TargetType = TargetType.Point,
        TargetFilter = TargetFilter.None,
        Range = 9f,
    };

    // ── 3. MultiTarget ──────────────────────────────────────────────

    private void SetupMultiTarget()
    {
        EnsureDummyVisual("en1", new Vector3(-2f, 0f, -4f), EnemyRed, "Target A");
        EnsureDummyVisual("en2", new Vector3(2f, 0f, -3f), EnemyRed, "Target B");
        EnsureDummyVisual("en3", new Vector3(0f, 0f, -6f), EnemyRed, "Target C");
        EnsureDummyVisual("en4", new Vector3(5f, 0f, -7f), EnemyRed, "Out of Range");
        EnsureDummyVisual("al1", new Vector3(-1f, 0f, -2f), AllyBlue, "Ally (invalid)");
    }

    private static ActionDefinition ActionMultiTarget() => new()
    {
        Id = "showcase_multi",
        Name = "Magic Missile (3 darts)",
        TargetType = TargetType.MultiUnit,
        TargetFilter = TargetFilter.Enemies,
        Range = 12f,
        MaxTargets = 3,
    };

    // ── 4. Chain ─────────────────────────────────────────────────────

    private void SetupChain()
    {
        // Tight cluster of enemies for chain lightning
        EnsureDummyVisual("en1", new Vector3(0f, 0f, -4f), EnemyRed, "Primary");
        EnsureDummyVisual("en2", new Vector3(1.5f, 0f, -5f), EnemyRed, "Bounce 1");
        EnsureDummyVisual("en3", new Vector3(-1f, 0f, -6f), EnemyRed, "Bounce 2");
        EnsureDummyVisual("en4", new Vector3(2f, 0f, -7f), EnemyRed, "Bounce 3");
        EnsureDummyVisual("al1", new Vector3(-2f, 0f, -5f), AllyBlue, "Ally (danger)");
    }

    private static ActionDefinition ActionChain() => new()
    {
        Id = "showcase_chain",
        Name = "Chain Lightning",
        TargetType = TargetType.SingleUnit, // Primary target click
        TargetFilter = TargetFilter.Enemies,
        Range = 15f,
        MaxTargets = 4, // 1 primary + 3 bounces
    };

    // ── 5. AoECircle ─────────────────────────────────────────────────

    private void SetupAoECircle()
    {
        EnsureDummyVisual("en1", new Vector3(-1f, 0f, -6f), EnemyRed, "Goblin A");
        EnsureDummyVisual("en2", new Vector3(1f, 0f, -7f), EnemyRed, "Goblin B");
        EnsureDummyVisual("en3", new Vector3(0f, 0f, -5.5f), EnemyRed, "Goblin C");
        // Allies near enemies → friendly fire warning
        EnsureDummyVisual("al1", new Vector3(0.5f, 0f, -6f), AllyBlue, "Ally (FF!)");
    }

    private static ActionDefinition ActionAoECircle() => new()
    {
        Id = "showcase_fireball",
        Name = "Fireball",
        TargetType = TargetType.Circle,
        TargetFilter = TargetFilter.All,
        Range = 15f,
        AreaRadius = 6f,
    };

    // ── 6. AoECone ──────────────────────────────────────────────────

    private void SetupAoECone()
    {
        // Fan-shaped arrangement of enemies
        EnsureDummyVisual("en1", new Vector3(-2f, 0f, -3f), EnemyRed, "Left");
        EnsureDummyVisual("en2", new Vector3(0f, 0f, -4f), EnemyRed, "Center");
        EnsureDummyVisual("en3", new Vector3(2f, 0f, -3f), EnemyRed, "Right");
        EnsureDummyVisual("en4", new Vector3(3f, 0f, -5f), EnemyRed, "Far Right");
    }

    private static ActionDefinition ActionAoECone() => new()
    {
        Id = "showcase_cone",
        Name = "Burning Hands",
        TargetType = TargetType.Cone,
        TargetFilter = TargetFilter.All,
        Range = 5f,
        ConeAngle = 90f,
    };

    // ── 7. AoELine ──────────────────────────────────────────────────

    private void SetupAoELine()
    {
        // Enemies in a corridor
        EnsureDummyVisual("en1", new Vector3(0f, 0f, -3f), EnemyRed, "Front");
        EnsureDummyVisual("en2", new Vector3(0f, 0f, -6f), EnemyRed, "Mid");
        EnsureDummyVisual("en3", new Vector3(0f, 0f, -9f), EnemyRed, "Back");
        EnsureDummyVisual("al1", new Vector3(0.5f, 0f, -4.5f), AllyBlue, "Ally");
    }

    private static ActionDefinition ActionAoELine() => new()
    {
        Id = "showcase_line",
        Name = "Lightning Bolt",
        TargetType = TargetType.Line,
        TargetFilter = TargetFilter.All,
        Range = 20f,
        AreaRadius = 1f, // > 0 → maps to AoELine
        LineWidth = 1.5f,
    };

    // ── 8. AoEWall ──────────────────────────────────────────────────

    private void SetupAoEWall()
    {
        // Open space for wall placement
        EnsureDummyVisual("en1", new Vector3(-3f, 0f, -6f), EnemyRed, "Enemy A");
        EnsureDummyVisual("en2", new Vector3(3f, 0f, -6f), EnemyRed, "Enemy B");
        EnsureDummyVisual("al1", new Vector3(0f, 0f, -4f), AllyBlue, "Ally");
    }

    private static ActionDefinition ActionAoEWall() => new()
    {
        Id = "showcase_wall",
        Name = "Wall of Fire",
        TargetType = TargetType.WallSegment,
        TargetFilter = TargetFilter.All,
        Range = 12f,
        MaxWallLength = 12f,
        LineWidth = 1f,
    };

    // ── 9. StraightLine ─────────────────────────────────────────────

    private void SetupStraightLine()
    {
        EnsureDummyVisual("en1", new Vector3(0f, 0f, -8f), EnemyRed, "Target");
        // Low wall obstacle partway
        SpawnObstacle("LowWall", new Vector3(0f, 0.5f, -4f), new Vector3(4f, 1f, 0.3f));
    }

    private static ActionDefinition ActionStraightLine() => new()
    {
        Id = "showcase_straight",
        Name = "Scorching Ray",
        TargetType = TargetType.Line,
        TargetFilter = TargetFilter.Enemies,
        Range = 12f,
        AreaRadius = 0f, // 0 → maps to StraightLine
        LineWidth = 0.2f,
    };

    // ── 10. BallisticArc ─────────────────────────────────────────────

    private void SetupBallisticArc()
    {
        // Distant target with height difference (on elevated platform)
        EnsureDummyVisual("en1", new Vector3(0f, 2f, -10f), EnemyRed, "High Target");
        SpawnObstacle("Platform", new Vector3(0f, 1f, -10f), new Vector3(3f, 2f, 3f));
    }

    private static ActionDefinition ActionBallisticArc() => new()
    {
        Id = "showcase_ballistic",
        Name = "Throw Alchemist Fire",
        TargetType = TargetType.Circle, // mapped manually to BallisticArc
        TargetFilter = TargetFilter.All,
        Range = 12f,
        AreaRadius = 1.5f,
        BG3SpellType = "Throw",
    };

    // ── 11. BezierCurve ─────────────────────────────────────────────

    private void SetupBezierCurve()
    {
        // Target around a corner
        EnsureDummyVisual("en1", new Vector3(5f, 0f, -6f), EnemyRed, "Hidden Target");
        SpawnObstacle("CornerWall", new Vector3(3f, 1.5f, -4f), new Vector3(0.3f, 3f, 5f));
    }

    private static ActionDefinition ActionBezierCurve() => new()
    {
        Id = "showcase_bezier",
        Name = "Guided Bolt",
        TargetType = TargetType.SingleUnit,
        TargetFilter = TargetFilter.Enemies,
        Range = 15f,
    };

    // ── 12. PathfindProjectile ──────────────────────────────────────

    private void SetupPathfindProjectile()
    {
        // Target behind obstacles
        EnsureDummyVisual("en1", new Vector3(0f, 0f, -10f), EnemyRed, "Distant Target");
        SpawnObstacle("Pillar1", new Vector3(-1f, 1.5f, -5f), new Vector3(1f, 3f, 1f));
        SpawnObstacle("Pillar2", new Vector3(1.5f, 1.5f, -7f), new Vector3(1f, 3f, 1f));
    }

    private static ActionDefinition ActionPathfindProjectile() => new()
    {
        Id = "showcase_pathfind",
        Name = "Seeking Arrow",
        TargetType = TargetType.SingleUnit,
        TargetFilter = TargetFilter.Enemies,
        Range = 18f,
    };
}
