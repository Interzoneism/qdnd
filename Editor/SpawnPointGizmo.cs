#if TOOLS
#nullable enable
using Godot;
using System;

namespace QDND.Editor;

/// <summary>
/// 3D gizmo for spawn point placement in the scenario editor.
/// Provides visual representation and drag handling for spawn positions.
/// </summary>
[Tool]
public partial class SpawnPointGizmo : Node3D
{
    // Visual elements
    private MeshInstance3D? _meshInstance;
    private Label3D? _label;
    private Area3D? _clickArea;

    // Properties
    private string _combatantId = "";
    private int _team = 1;
    private bool _isSelected;
    private bool _isDragging;

    // Events
    public event Action<SpawnPointGizmo>? OnSelected;
    public event Action<SpawnPointGizmo, Vector3>? OnPositionChanged;

    // Team colors
    private static readonly Color Team1Color = new(0.2f, 0.6f, 1f); // Blue
    private static readonly Color Team2Color = new(1f, 0.3f, 0.3f); // Red
    private static readonly Color Team3Color = new(0.3f, 1f, 0.3f); // Green
    private static readonly Color Team4Color = new(1f, 1f, 0.3f);   // Yellow
    private static readonly Color SelectedColor = new(1f, 0.8f, 0f); // Gold highlight

    /// <summary>
    /// Gets or sets the combatant ID this gizmo represents.
    /// </summary>
    public string CombatantId
    {
        get => _combatantId;
        set
        {
            _combatantId = value;
            UpdateLabel();
        }
    }

    /// <summary>
    /// Gets or sets the team number (1-4).
    /// </summary>
    public int Team
    {
        get => _team;
        set
        {
            _team = Math.Clamp(value, 1, 4);
            UpdateColor();
        }
    }

    /// <summary>
    /// Gets or sets whether this gizmo is selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            UpdateColor();
        }
    }

    public override void _Ready()
    {
        CreateVisuals();
        CreateClickArea();
    }

    private void CreateVisuals()
    {
        // Create the main mesh (cylinder with sphere on top)
        _meshInstance = new MeshInstance3D();

        var capsule = new CapsuleMesh();
        capsule.Radius = 0.3f;
        capsule.Height = 1.5f;
        _meshInstance.Mesh = capsule;

        // Create material
        var material = new StandardMaterial3D();
        material.AlbedoColor = GetTeamColor();
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.AlbedoColor = new Color(material.AlbedoColor, 0.8f);
        _meshInstance.MaterialOverride = material;

        // Offset mesh so base is at origin
        _meshInstance.Position = new Vector3(0, 0.75f, 0);
        AddChild(_meshInstance);

        // Create label above the mesh
        _label = new Label3D();
        _label.Text = _combatantId;
        _label.Position = new Vector3(0, 1.8f, 0);
        _label.FontSize = 32;
        _label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _label.NoDepthTest = true;
        AddChild(_label);
    }

    private void CreateClickArea()
    {
        _clickArea = new Area3D();

        var collision = new CollisionShape3D();
        var shape = new CapsuleShape3D();
        shape.Radius = 0.4f;
        shape.Height = 1.6f;
        collision.Shape = shape;
        collision.Position = new Vector3(0, 0.8f, 0);

        _clickArea.AddChild(collision);
        _clickArea.Connect("input_event", Callable.From<Node, InputEvent, Vector3, Vector3, int>(OnInputEvent));
        AddChild(_clickArea);
    }

    private void OnInputEvent(Node camera, InputEvent inputEvent, Vector3 position, Vector3 normal, int shapeIdx)
    {
        if (inputEvent is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    IsSelected = true;
                    _isDragging = true;
                    OnSelected?.Invoke(this);
                }
                else
                {
                    _isDragging = false;
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_isDragging && IsSelected)
        {
            // Dragging logic would be handled by editor viewport
            // This is a placeholder for the actual 3D picking implementation
        }
    }

    /// <summary>
    /// Sets the position of this spawn point.
    /// </summary>
    public void SetSpawnPosition(Vector3 position)
    {
        Position = position;
        OnPositionChanged?.Invoke(this, position);
    }

    /// <summary>
    /// Gets the spawn position (same as Position).
    /// </summary>
    public Vector3 GetSpawnPosition() => Position;

    private void UpdateLabel()
    {
        if (_label != null)
        {
            _label.Text = _combatantId;
        }
    }

    private void UpdateColor()
    {
        if (_meshInstance?.MaterialOverride is StandardMaterial3D material)
        {
            var baseColor = _isSelected ? SelectedColor : GetTeamColor();
            material.AlbedoColor = new Color(baseColor, 0.8f);

            // Add emission when selected
            material.EmissionEnabled = _isSelected;
            if (_isSelected)
            {
                material.Emission = SelectedColor;
                material.EmissionEnergyMultiplier = 0.5f;
            }
        }
    }

    private Color GetTeamColor()
    {
        return _team switch
        {
            1 => Team1Color,
            2 => Team2Color,
            3 => Team3Color,
            4 => Team4Color,
            _ => Team1Color
        };
    }

    /// <summary>
    /// Creates a spawn point gizmo from combatant data.
    /// </summary>
    public static SpawnPointGizmo CreateFromData(CombatantSpawnData data)
    {
        var gizmo = new SpawnPointGizmo();
        gizmo.CombatantId = data.Id;
        gizmo.Team = data.Team;
        gizmo.Position = new Vector3(data.PositionX, data.PositionY, data.PositionZ);
        return gizmo;
    }

    /// <summary>
    /// Updates the combatant data with this gizmo's position.
    /// </summary>
    public void ApplyToData(CombatantSpawnData data)
    {
        data.PositionX = Position.X;
        data.PositionY = Position.Y;
        data.PositionZ = Position.Z;
    }
}
#endif
