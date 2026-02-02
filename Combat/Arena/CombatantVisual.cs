using Godot;
using System;
using QDND.Combat.Entities;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual representation of a combatant in the 3D arena.
    /// Syncs with the backend Combatant entity and handles visual feedback.
    /// </summary>
    public partial class CombatantVisual : Area3D
    {
        [Export] public Color PlayerColor = new Color(0.2f, 0.6f, 1.0f);
        [Export] public Color EnemyColor = new Color(1.0f, 0.3f, 0.3f);
        [Export] public Color SelectedColor = new Color(0.0f, 1.0f, 0.5f);
        [Export] public Color ValidTargetColor = new Color(1.0f, 1.0f, 0.0f);
        
        // Node references
        private Node3D _modelRoot;
        private MeshInstance3D _capsuleMesh;
        private MeshInstance3D _selectionRing;
        private Label3D _nameLabel;
        private ProgressBar _hpBar;
        private Control _hpBarControl;
        private Label3D _statusLabel;
        
        // State
        private string _combatantId;
        private CombatArena _arena;
        private Combatant _entity;
        private bool _isSelected;
        private bool _isActive;
        private bool _isValidTarget;
        
        // Animation
        private Tween _currentTween;
        
        private bool _nodesReady = false;
        
        public string CombatantId => _combatantId;
        public Combatant Entity => _entity;
        public bool IsSelected => _isSelected;
        public bool IsActive => _isActive;

        public override void _Ready()
        {
            // Get or create child nodes if not already done via Initialize
            if (!_nodesReady)
            {
                SetupVisualNodes();
            }
        }

        private void SetupVisualNodes()
        {
            if (_nodesReady) return;
            
            // Model root (will contain the actual character model)
            _modelRoot = GetNodeOrNull<Node3D>("ModelRoot");
            if (_modelRoot == null)
            {
                _modelRoot = new Node3D { Name = "ModelRoot" };
                AddChild(_modelRoot);
            }
            
            // Capsule mesh as fallback visual
            _capsuleMesh = GetNodeOrNull<MeshInstance3D>("CapsuleMesh");
            if (_capsuleMesh == null)
            {
                _capsuleMesh = new MeshInstance3D { Name = "CapsuleMesh" };
                var capsule = new CapsuleMesh();
                capsule.Radius = 0.4f;
                capsule.Height = 1.8f;
                _capsuleMesh.Mesh = capsule;
                _capsuleMesh.Position = new Vector3(0, 0.9f, 0);
                _modelRoot.AddChild(_capsuleMesh);
            }

            
            
            // Selection ring
            _selectionRing = GetNodeOrNull<MeshInstance3D>("SelectionRing");
            if (_selectionRing == null)
            {
                _selectionRing = new MeshInstance3D { Name = "SelectionRing" };
                var torus = new TorusMesh();
                torus.InnerRadius = 0.5f;
                torus.OuterRadius = 0.7f;
                _selectionRing.Mesh = torus;
                _selectionRing.Position = new Vector3(0, 0.05f, 0);
                _selectionRing.Visible = false;
                AddChild(_selectionRing);
            }
            
            // Name label
            _nameLabel = GetNodeOrNull<Label3D>("NameLabel");
            if (_nameLabel == null)
            {
                _nameLabel = new Label3D { Name = "NameLabel" };
                _nameLabel.Position = new Vector3(0, 2.2f, 0);
                _nameLabel.FontSize = 32;
                _nameLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                _nameLabel.NoDepthTest = true;
                AddChild(_nameLabel);
            }
            
            // Status label (for floating text)
            _statusLabel = GetNodeOrNull<Label3D>("StatusLabel");
            if (_statusLabel == null)
            {
                _statusLabel = new Label3D { Name = "StatusLabel" };
                _statusLabel.Position = new Vector3(0, 2.5f, 0);
                _statusLabel.FontSize = 24;
                _statusLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                _statusLabel.NoDepthTest = true;
                _statusLabel.Visible = false;
                AddChild(_statusLabel);
            }
            
            // HP bar using SubViewport for 2D control in 3D
            SetupHPBar();

            _nodesReady = true;
        }

        private void SetupHPBar()
        {
            var hpBarNode = GetNodeOrNull<Node3D>("HPBar");
            if (hpBarNode == null)
            {
                // Create a simple Sprite3D that shows HP percentage via modulate
                var hpSprite = new Sprite3D { Name = "HPBar" };
                
                // Create HP bar texture programmatically
                var image = Image.CreateEmpty(64, 8, false, Image.Format.Rgba8);
                image.Fill(Colors.Green);
                var texture = ImageTexture.CreateFromImage(image);
                hpSprite.Texture = texture;
                
                hpSprite.Position = new Vector3(0, 2.0f, 0);
                hpSprite.PixelSize = 0.01f;
                hpSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                hpSprite.NoDepthTest = true;
                AddChild(hpSprite);
            }
        }

        public void Initialize(Combatant entity, CombatArena arena)
        {
            _entity = entity;
            _combatantId = entity.Id;
            _arena = arena;

            // Manually setup nodes since _Ready hasn't been called yet.
            SetupVisualNodes();
            
            // Setup appearance based on faction
            UpdateAppearance();
            UpdateFromEntity();
        }

        private void UpdateAppearance()
        {
            if (_entity == null) return;
            
            // Set capsule color based on faction
            var material = new StandardMaterial3D();
            material.AlbedoColor = _entity.Faction == Faction.Player ? PlayerColor : EnemyColor;
            _capsuleMesh.MaterialOverride = material;
            
            // Set name
            _nameLabel.Text = _entity.Name;
            _nameLabel.Modulate = _entity.Faction == Faction.Player ? PlayerColor : EnemyColor;
            
            // Selection ring color
            var ringMaterial = new StandardMaterial3D();
            ringMaterial.AlbedoColor = SelectedColor;
            ringMaterial.EmissionEnabled = true;
            ringMaterial.Emission = SelectedColor;
            ringMaterial.EmissionEnergyMultiplier = 2.0f;
            _selectionRing.MaterialOverride = ringMaterial;
        }

        public void UpdateFromEntity()
        {
            if (_entity == null) return;
            
            // Update HP bar
            float hpPercent = (float)_entity.Resources.CurrentHP / _entity.Resources.MaxHP;
            UpdateHPBar(hpPercent);
            
            // Update visibility based on alive status
            _capsuleMesh.Visible = _entity.IsActive;
            _nameLabel.Visible = _entity.IsActive;
            
            // Fade out if dead
            if (!_entity.IsActive)
            {
                var material = _capsuleMesh.MaterialOverride as StandardMaterial3D;
                if (material != null)
                {
                    material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    material.AlbedoColor = new Color(material.AlbedoColor, 0.3f);
                }
            }
        }

        private void UpdateHPBar(float percent)
        {
            var hpSprite = GetNodeOrNull<Sprite3D>("HPBar");
            if (hpSprite != null)
            {
                // Color based on HP
                Color hpColor = Colors.Green;
                if (percent < 0.3f) hpColor = Colors.Red;
                else if (percent < 0.6f) hpColor = Colors.Yellow;
                
                hpSprite.Modulate = hpColor;
                hpSprite.Scale = new Vector3(percent, 1, 1);
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            _selectionRing.Visible = selected;
            
            if (selected)
            {
                // Pulse animation
                AnimateScale(1.1f, 0.2f);
            }
            else
            {
                AnimateScale(1.0f, 0.2f);
            }
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            
            // Visual indication of active turn
            if (active)
            {
                // Glow effect or bounce
                AnimateBounce();
            }
        }

        public void SetValidTarget(bool valid)
        {
            _isValidTarget = valid;
            
            if (valid && !_isSelected)
            {
                var ringMaterial = _selectionRing.MaterialOverride as StandardMaterial3D;
                if (ringMaterial != null)
                {
                    ringMaterial.AlbedoColor = ValidTargetColor;
                    ringMaterial.Emission = ValidTargetColor;
                }
                _selectionRing.Visible = true;
            }
            else if (!valid && !_isSelected)
            {
                _selectionRing.Visible = false;
            }
        }

        public void ShowDamage(int amount)
        {
            ShowFloatingText($"-{amount}", Colors.Red);
            AnimateHit();
        }

        public void ShowHealing(int amount)
        {
            ShowFloatingText($"+{amount}", Colors.Green);
        }

        public void ShowStatusApplied(string statusName)
        {
            ShowFloatingText($"[{statusName}]", Colors.Purple);
        }

        public void ShowStatusRemoved(string statusName)
        {
            ShowFloatingText($"[-{statusName}]", Colors.Gray);
        }

        private void ShowFloatingText(string text, Color color)
        {
            _statusLabel.Text = text;
            _statusLabel.Modulate = color;
            _statusLabel.Visible = true;
            
            // Animate floating up and fading
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(_statusLabel, "position:y", 3.5f, 1.0f).From(2.5f);
            tween.TweenProperty(_statusLabel, "modulate:a", 0.0f, 1.0f).From(1.0f);
            tween.SetParallel(false);
            tween.TweenCallback(Callable.From(() => _statusLabel.Visible = false));
        }

        public void PlayAttackAnimation()
        {
            // Simple lunge forward animation
            var tween = CreateTween();
            var originalPos = _modelRoot.Position;
            var forwardPos = originalPos + new Vector3(0, 0, -0.5f);
            
            tween.TweenProperty(_modelRoot, "position", forwardPos, 0.15f);
            tween.TweenProperty(_modelRoot, "position", originalPos, 0.15f);
        }

        private void AnimateHit()
        {
            // Flash red and shake
            var originalColor = (_capsuleMesh.MaterialOverride as StandardMaterial3D)?.AlbedoColor ?? Colors.White;
            var material = _capsuleMesh.MaterialOverride as StandardMaterial3D;
            
            if (material != null)
            {
                var tween = CreateTween();
                tween.TweenProperty(material, "albedo_color", Colors.White, 0.05f);
                tween.TweenProperty(material, "albedo_color", originalColor, 0.15f);
            }
            
            // Shake
            var shakeAmount = 0.1f;
            var originalPos = _modelRoot.Position;
            var shakeTween = CreateTween();
            shakeTween.TweenProperty(_modelRoot, "position", originalPos + new Vector3(shakeAmount, 0, 0), 0.05f);
            shakeTween.TweenProperty(_modelRoot, "position", originalPos - new Vector3(shakeAmount, 0, 0), 0.05f);
            shakeTween.TweenProperty(_modelRoot, "position", originalPos, 0.05f);
        }

        private void AnimateScale(float targetScale, float duration)
        {
            var tween = CreateTween();
            tween.TweenProperty(_modelRoot, "scale", Vector3.One * targetScale, duration);
        }

        private void AnimateBounce()
        {
            var tween = CreateTween();
            tween.TweenProperty(_modelRoot, "position:y", 0.3f, 0.15f);
            tween.TweenProperty(_modelRoot, "position:y", 0.0f, 0.15f);
        }

        /// <summary>
        /// Handle click on this combatant. Called by CombatInputHandler via raycast.
        /// </summary>
        public void OnClicked()
        {
            if (_arena != null)
            {
                if (!string.IsNullOrEmpty(_arena.SelectedAbilityId))
                {
                    // Targeting mode - execute ability on this target
                    if (_isValidTarget)
                    {
                        _arena.ExecuteAbility(_arena.SelectedCombatantId, _arena.SelectedAbilityId, _combatantId);
                    }
                }
                else
                {
                    // Selection mode - select this combatant
                    _arena.SelectCombatant(_combatantId);
                }
            }
        }
    }
}
