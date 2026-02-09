using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual representation of a combatant in the 3D arena.
    /// Syncs with the backend Combatant entity and handles visual feedback.
    /// </summary>
    public partial class CombatantVisual : Area3D
    {
        [Export] public Color PlayerColor = new Color(0.302f, 0.671f, 0.969f);     // Blue #4DABF7 for player
        [Export] public Color EnemyColor = new Color(1.0f, 0.42f, 0.42f);           // Red #FF6B6B for enemy
        [Export] public Color SelectedAllyColor = new Color(0.318f, 0.812f, 0.4f);  // Green #51CF66 for selected ally
        [Export] public Color SelectedEnemyColor = new Color(1.0f, 0.42f, 0.42f);   // Red #FF6B6B for selected enemy
        [Export] public Color ValidTargetColor = new Color(1.0f, 0.843f, 0.0f);     // Gold #FFD700 for valid target
        [Export] public Color ActiveRingColor = new Color(1.0f, 0.92f, 0.35f);      // Bright gold for active combatant
        [Export] public Color HoverColor = new Color(1.0f, 1.0f, 1.0f, 0.6f);       // White 60% for hover
        [Export] public float MovementSpeed = 7.0f;                                  // Units per second for movement animation

        // Node references
        private Node3D _modelRoot;
        private MeshInstance3D _capsuleMesh;
        private MeshInstance3D _selectionRing;
        private Label3D _nameLabel;
        private ProgressBar _hpBar;
        private Control _hpBarControl;
        private Label3D _statusLabel;
        private Label3D _activeStatusLabel;

        // State
        private string _combatantId;
        private CombatArena _arena;
        private Combatant _entity;
        private bool _isSelected;
        private bool _isActive;
        private bool _isValidTarget;

        // Animation
        private Tween _currentTween;
        private Tween _ringPulseTween;
        private const float HP_BAR_PIXEL_SIZE = 0.015f;
        private const float HP_BAR_TEXTURE_WIDTH = 116f;

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



            // Selection ring - thinner and more elegant
            _selectionRing = GetNodeOrNull<MeshInstance3D>("SelectionRing");
            if (_selectionRing == null)
            {
                _selectionRing = new MeshInstance3D { Name = "SelectionRing" };
                var torus = new TorusMesh();
                torus.InnerRadius = 0.52f;
                torus.OuterRadius = 0.78f;
                _selectionRing.Mesh = torus;
                _selectionRing.Position = new Vector3(0, 0.03f, 0);
                _selectionRing.Rotation = Vector3.Zero;
                _selectionRing.Visible = false;
                AddChild(_selectionRing);
            }
            else
            {
                _selectionRing.Position = new Vector3(0, 0.03f, 0);
                _selectionRing.Rotation = Vector3.Zero;
                if (_selectionRing.Mesh is TorusMesh torus)
                {
                    torus.InnerRadius = 0.52f;
                    torus.OuterRadius = 0.78f;
                }
            }

            // Background for name label - dark semi-transparent panel
            var nameBg = new Sprite3D();
            nameBg.Name = "NameLabelBg";
            var bgImage = Image.CreateEmpty(160, 24, false, Image.Format.Rgba8);
            bgImage.Fill(new Color(0.05f, 0.03f, 0.08f, 0.6f)); // Very dark purple, 60% opacity
            var bgTexture = ImageTexture.CreateFromImage(bgImage);
            nameBg.Texture = bgTexture;
            nameBg.PixelSize = 0.01f;
            nameBg.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            nameBg.NoDepthTest = true;
            nameBg.RenderPriority = 4;
            nameBg.Position = new Vector3(0, 2.85f, 0.001f); // Same as name label, slightly behind
            AddChild(nameBg);

            // Name label with outline for readability
            _nameLabel = GetNodeOrNull<Label3D>("NameLabel");
            if (_nameLabel == null)
            {
                _nameLabel = new Label3D { Name = "NameLabel" };
                _nameLabel.Position = new Vector3(0, 2.85f, 0);
                _nameLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                _nameLabel.NoDepthTest = true;
                _nameLabel.RenderPriority = 5;
                _nameLabel.OutlineModulate = Colors.Black;
                AddChild(_nameLabel);
            }
            _nameLabel.Position = new Vector3(0, 2.85f, 0);
            _nameLabel.FontSize = 16;
            _nameLabel.PixelSize = 0.001f;
            _nameLabel.OutlineSize = 3;
            _nameLabel.FixedSize = true;

            // Active status display (persistent, above HP bar)
            _activeStatusLabel = GetNodeOrNull<Label3D>("ActiveStatusLabel");
            if (_activeStatusLabel == null)
            {
                _activeStatusLabel = new Label3D { Name = "ActiveStatusLabel" };
                _activeStatusLabel.Position = new Vector3(0, 2.55f, 0);
                _activeStatusLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                _activeStatusLabel.NoDepthTest = true;
                _activeStatusLabel.Visible = false;
                _activeStatusLabel.Modulate = new Color(0.9f, 0.7f, 1.0f); // Light purple
                AddChild(_activeStatusLabel);
            }
            _activeStatusLabel.Position = new Vector3(0, 2.55f, 0);
            _activeStatusLabel.FontSize = 12;
            _activeStatusLabel.PixelSize = 0.001f;
            _activeStatusLabel.OutlineSize = 2;
            _activeStatusLabel.FixedSize = true;

            // Status label (for floating text)
            _statusLabel = GetNodeOrNull<Label3D>("StatusLabel");
            if (_statusLabel == null)
            {
                _statusLabel = new Label3D { Name = "StatusLabel" };
                _statusLabel.Position = new Vector3(0, 2.55f, 0);
                _statusLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                _statusLabel.NoDepthTest = true;
                _statusLabel.Visible = false;
                AddChild(_statusLabel);
            }
            _statusLabel.Position = new Vector3(0, 2.55f, 0);
            _statusLabel.FontSize = 14;
            _statusLabel.PixelSize = 0.001f;
            _statusLabel.OutlineSize = 3;
            _statusLabel.FixedSize = true;

            // HP bar using SubViewport for 2D control in 3D
            SetupHPBar();

            _nodesReady = true;
        }

        private void SetupHPBar()
        {
            var hpBarNode = GetNodeOrNull<Node3D>("HPBarGroup");
            if (hpBarNode == null)
            {
                var group = new Node3D { Name = "HPBarGroup" };
                group.Position = new Vector3(0, 2.2f, 0);
                AddChild(group);
                
                // Outline bar (border effect)
                var outlineSprite = new Sprite3D { Name = "HPBarOutline" };
                var outlineImage = Image.CreateEmpty(124, 20, false, Image.Format.Rgba8);
                outlineImage.Fill(new Color(0.05f, 0.05f, 0.05f, 0.9f));
                var outlineTexture = ImageTexture.CreateFromImage(outlineImage);
                outlineSprite.Texture = outlineTexture;
                outlineSprite.PixelSize = 0.015f;
                outlineSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                outlineSprite.NoDepthTest = true;
                outlineSprite.RenderPriority = 9;
                group.AddChild(outlineSprite);
                
                // Background bar (dark)
                var bgSprite = new Sprite3D { Name = "HPBarBg" };
                var bgImage = Image.CreateEmpty(120, 16, false, Image.Format.Rgba8);
                bgImage.Fill(new Color(0.1f, 0.1f, 0.1f, 0.8f));
                var bgTexture = ImageTexture.CreateFromImage(bgImage);
                bgSprite.Texture = bgTexture;
                bgSprite.PixelSize = 0.015f;
                bgSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                bgSprite.NoDepthTest = true;
                bgSprite.RenderPriority = 10;
                bgSprite.Position = new Vector3(0, 0, -0.001f);
                group.AddChild(bgSprite);
                
                // Foreground HP bar
                var hpSprite = new Sprite3D { Name = "HPBar" };
                var image = Image.CreateEmpty(116, 12, false, Image.Format.Rgba8);
                image.Fill(new Color(0.318f, 0.812f, 0.4f)); // #51CF66
                var texture = ImageTexture.CreateFromImage(image);
                hpSprite.Texture = texture;
                hpSprite.PixelSize = HP_BAR_PIXEL_SIZE;
                hpSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                hpSprite.NoDepthTest = true;
                hpSprite.RenderPriority = 11;
                hpSprite.Position = new Vector3(0, 0, -0.002f); // Slightly in front of bg
                group.AddChild(hpSprite);
                
                // HP text overlay
                var hpText = new Label3D { Name = "HPText" };
                hpText.Position = new Vector3(0, 0, -0.003f);
                hpText.FontSize = 12;
                hpText.PixelSize = 0.001f;
                hpText.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                hpText.NoDepthTest = true;
                hpText.RenderPriority = 12;
                hpText.OutlineSize = 2;
                hpText.OutlineModulate = Colors.Black;
                hpText.Modulate = Colors.White;
                hpText.Text = "HP";
                group.AddChild(hpText);
            }
        }

        public void Initialize(Combatant entity, CombatArena arena)
        {
            _entity = entity;
            _combatantId = entity.Id;
            _arena = arena;

            // Manually setup nodes since _Ready hasn't been called yet.
            SetupVisualNodes();

            // Ensure collision is set up correctly for raycasting
            CollisionLayer = 2; // Layer 2 for combatants
            CollisionMask = 0;  // Don't detect collisions with anything
            InputRayPickable = true;

            // Verify collision shape exists
            var collisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (collisionShape == null)
            {
                GD.Print($"[CombatantVisual] WARNING: No CollisionShape3D found for {entity.Name}, creating one");
                collisionShape = new CollisionShape3D { Name = "CollisionShape3D" };
                var capsule = new CapsuleShape3D();
                capsule.Radius = 0.4f;
                capsule.Height = 1.8f;
                collisionShape.Shape = capsule;
                collisionShape.Position = new Vector3(0, 0.9f, 0);
                AddChild(collisionShape);
            }
            else
            {
                GD.Print($"[CombatantVisual] CollisionShape3D found for {entity.Name}");
            }

            GD.Print($"[CombatantVisual] {entity.Name} initialized - Layer: {CollisionLayer}, Mask: {CollisionMask}, Pickable: {InputRayPickable}, HasShape: {collisionShape != null}");

            // Setup appearance based on faction
            UpdateAppearance();
            UpdateFromEntity();
        }

        private void UpdateAppearance()
        {
            if (_entity == null) return;

            // Set capsule color based on faction with emission glow
            var material = new StandardMaterial3D();
            var baseColor = _entity.Faction == Faction.Player ? PlayerColor : EnemyColor;
            material.AlbedoColor = baseColor;
            material.EmissionEnabled = true;
            material.Emission = baseColor;
            material.EmissionEnergyMultiplier = 0.3f;
            _capsuleMesh.MaterialOverride = material;

            // Set name
            _nameLabel.Text = _entity.Name;
            _nameLabel.Modulate = _entity.Faction == Faction.Player ? PlayerColor : EnemyColor;

            // Selection ring color - use faction-specific color per layout spec
            UpdateSelectionRingAppearance();
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

            // Fade out if dead - reduce emission and increase transparency
            if (!_entity.IsActive)
            {
                var material = _capsuleMesh.MaterialOverride as StandardMaterial3D;
                if (material != null)
                {
                    material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    material.AlbedoColor = new Color(material.AlbedoColor, 0.3f);
                    material.EmissionEnergyMultiplier = 0.05f;
                }
            }
        }

        private void UpdateHPBar(float percent)
        {
            var hpSprite = GetNodeOrNull<Sprite3D>("HPBarGroup/HPBar");
            // fallback to old path
            if (hpSprite == null) hpSprite = GetNodeOrNull<Sprite3D>("HPBar");
            
            if (hpSprite != null)
            {
                // Better colors: green > 50%, orange 30-50%, red < 30%
                Color hpColor = new Color(0.318f, 0.812f, 0.4f); // #51CF66 green
                if (percent < 0.3f) hpColor = new Color(1.0f, 0.267f, 0.267f); // #FF4444 red
                else if (percent <= 0.5f) hpColor = new Color(1.0f, 0.663f, 0.302f); // #FFA94D orange

                hpSprite.Modulate = hpColor;
                float visiblePercent = Mathf.Clamp(percent, 0f, 1f);
                float scaledPercent = Mathf.Max(visiblePercent, 0.01f);
                hpSprite.Scale = new Vector3(scaledPercent, 1, 1);

                // Keep the left edge anchored so the bar drains to the left.
                float fullWidthWorld = HP_BAR_TEXTURE_WIDTH * HP_BAR_PIXEL_SIZE;
                float baseHalfWidth = fullWidthWorld * 0.5f;
                float anchoredX = baseHalfWidth * (scaledPercent - 1f);
                hpSprite.Position = new Vector3(anchoredX, 0, -0.002f);
            }
            
            // Update HP text overlay
            var hpText = GetNodeOrNull<Label3D>("HPBarGroup/HPText");
            if (hpText != null && _entity != null)
            {
                hpText.Text = $"{_entity.Resources.CurrentHP}/{_entity.Resources.MaxHP}";
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateSelectionRingAppearance();

            if (selected)
            {
                // Pulse animation with pulsing scale oscillation
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
            UpdateSelectionRingAppearance();

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
            UpdateSelectionRingAppearance();
        }

        public void ShowDamage(int amount, bool isCritical = false)
        {
            if (isCritical)
            {
                ShowFloatingText($"CRITICAL! -{amount}", new Color(1.0f, 0.84f, 0.0f), fontSize: 18, outlineSize: 4, critical: true);
            }
            else
            {
                ShowFloatingText($"-{amount}", Colors.Red, fontSize: 15, outlineSize: 3);
            }
            AnimateHit();
        }

        public void ShowMiss()
        {
            ShowFloatingText("MISS", Colors.Gray, fontSize: 15, outlineSize: 3);
        }

        public void ShowHealing(int amount)
        {
            ShowFloatingText($"+{amount}", Colors.Green, fontSize: 15, outlineSize: 3);
        }

        public void ShowStatusApplied(string statusName)
        {
            ShowFloatingText($"[{statusName}]", Colors.Purple, fontSize: 12, outlineSize: 2);
        }

        public void ShowStatusRemoved(string statusName)
        {
            ShowFloatingText($"[-{statusName}]", Colors.Gray);
        }

        private void ShowFloatingText(string text, Color color, int fontSize = 14, int outlineSize = 3, bool critical = false)
        {
            _statusLabel.Text = text;
            _statusLabel.FontSize = fontSize;
            _statusLabel.Modulate = color;
            _statusLabel.OutlineSize = outlineSize;
            _statusLabel.OutlineModulate = Colors.Black;
            _statusLabel.Visible = true;

            // Keep popups readable but compact in world space.
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(_statusLabel, "position:y", 3.35f, 0.9f).From(2.55f);
            tween.TweenProperty(_statusLabel, "modulate:a", 0.0f, 0.9f).From(1.0f);
            
            // Add scale animation for critical hits
            if (critical)
            {
                tween.TweenProperty(_statusLabel, "scale", Vector3.One * 1.2f, 0.16f).From(Vector3.One);
                tween.TweenProperty(_statusLabel, "scale", Vector3.One, 0.24f).SetDelay(0.16f);
            }
            
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
            // Flash white brighter and longer, plus red emission flash
            var originalColor = (_capsuleMesh.MaterialOverride as StandardMaterial3D)?.AlbedoColor ?? Colors.White;
            var material = _capsuleMesh.MaterialOverride as StandardMaterial3D;

            if (material != null)
            {
                var originalEmission = material.EmissionEnergyMultiplier;
                var tween = CreateTween();
                tween.SetParallel(true);
                tween.TweenProperty(material, "albedo_color", Colors.White, 0.08f);
                tween.TweenProperty(material, "emission", Colors.Red, 0.08f);
                tween.TweenProperty(material, "emission_energy_multiplier", 1.5f, 0.08f);
                tween.SetParallel(false);
                tween.TweenProperty(material, "albedo_color", originalColor, 0.15f);
                tween.TweenCallback(Callable.From(() => {
                    material.Emission = originalColor;
                    material.EmissionEnergyMultiplier = originalEmission;
                }));
            }

            // Shake with increased amount (0.15f from 0.1f)
            var shakeAmount = 0.15f;
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
        /// Show hit chance percentage when targeting.
        /// </summary>
        public void ShowHitChance(int percentage)
        {
            _nameLabel.Text = $"{_entity?.Name ?? "Unknown"} ({percentage}%)";
        }

        /// <summary>
        /// Clear hit chance display and restore normal name.
        /// </summary>
        public void ClearHitChance()
        {
            if (_entity != null)
            {
                _nameLabel.Text = _entity.Name;
            }
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

        /// <summary>
        /// Update persistent status display showing active effects.
        /// </summary>
        public void SetActiveStatuses(IEnumerable<string> statusNames)
        {
            if (_activeStatusLabel == null) return;
            var names = statusNames?.ToList();
            if (names == null || names.Count == 0)
            {
                _activeStatusLabel.Visible = false;
                return;
            }
            _activeStatusLabel.Text = string.Join(" | ", names);
            _activeStatusLabel.Visible = true;
        }

        /// <summary>
        /// Animate this visual moving to a new position. Calls onComplete when done.
        /// </summary>
        /// <param name="targetWorldPos">Target position in world space</param>
        /// <param name="speed">Movement speed in units per second (uses MovementSpeed export if not specified)</param>
        /// <param name="onComplete">Callback to invoke when animation completes</param>
        public void AnimateMoveTo(Vector3 targetWorldPos, float? speed = null, Action onComplete = null)
        {
            float actualSpeed = speed ?? MovementSpeed;
            float distance = Position.DistanceTo(targetWorldPos);
            float duration = distance / actualSpeed;
            duration = Mathf.Clamp(duration, 0.1f, 5.0f); // Min 0.1s, max 5s

            _currentTween?.Kill(); // Cancel any existing tween
            _currentTween = CreateTween();
            _currentTween.SetEase(Tween.EaseType.InOut);
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.TweenProperty(this, "position", targetWorldPos, duration);
            _currentTween.TweenCallback(Callable.From(() => onComplete?.Invoke()));
        }

        private void UpdateSelectionRingAppearance()
        {
            if (_selectionRing == null || _entity == null) return;

            bool shouldShow = _isSelected || _isValidTarget || _isActive;
            if (!shouldShow)
            {
                _selectionRing.Visible = false;
                StopRingPulse();
                return;
            }

            Color ringColor;
            float emissionStrength;
            float targetScale;

            if (_isSelected)
            {
                ringColor = _entity.Faction == Faction.Player ? SelectedAllyColor : SelectedEnemyColor;
                emissionStrength = 4.0f;
                targetScale = 1.1f;
            }
            else if (_isValidTarget)
            {
                ringColor = ValidTargetColor;
                emissionStrength = 3.5f;
                targetScale = 1.06f;
            }
            else
            {
                ringColor = ActiveRingColor;
                emissionStrength = 5.0f;
                targetScale = 1.2f;
            }

            if (_selectionRing.MaterialOverride is not StandardMaterial3D ringMaterial)
            {
                ringMaterial = new StandardMaterial3D();
                _selectionRing.MaterialOverride = ringMaterial;
            }

            ringMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            ringMaterial.AlbedoColor = new Color(ringColor, 0.95f);
            ringMaterial.EmissionEnabled = true;
            ringMaterial.Emission = ringColor;
            ringMaterial.EmissionEnergyMultiplier = emissionStrength;
            ringMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;

            _selectionRing.Visible = true;
            _selectionRing.Scale = Vector3.One * targetScale;

            if (_isSelected || _isActive)
            {
                StartRingPulse(targetScale);
            }
            else
            {
                StopRingPulse();
            }
        }

        private void StartRingPulse(float baseScale)
        {
            if (_selectionRing == null) return;

            StopRingPulse();
            _ringPulseTween = CreateTween();
            _ringPulseTween.SetLoops();
            _ringPulseTween.SetTrans(Tween.TransitionType.Sine);
            _ringPulseTween.SetEase(Tween.EaseType.InOut);
            _ringPulseTween.TweenProperty(_selectionRing, "scale", Vector3.One * (baseScale * 1.08f), 0.36f);
            _ringPulseTween.TweenProperty(_selectionRing, "scale", Vector3.One * baseScale, 0.36f);
        }

        private void StopRingPulse()
        {
            _ringPulseTween?.Kill();
            _ringPulseTween = null;
        }
    }
}
