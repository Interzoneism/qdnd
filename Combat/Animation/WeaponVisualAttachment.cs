using Godot;
using System.Collections.Generic;
using QDND.Data;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Animation
{
    /// <summary>
    /// Manages 3D weapon models attached to a character's hand bones.
    ///
    /// Workflow:
    ///   1. Call AttachWeapon(modelRoot, weapon, settings) after the character's
    ///      Skeleton3D is already in the scene tree.
    ///   2. The method finds the Skeleton3D, creates a BoneAttachment3D on hand_r,
    ///      loads the correct FBX mesh and parents it underneath.
    ///   3. Call RemoveWeapon(modelRoot) to clean up before detaching or swapping.
    ///
    /// Weapon models are authored vertically (Y-up, pivot at the bottom of the
    /// handle). WeaponAttachmentSettings.RotationDegrees rotates them into the
    /// character's grip pose.
    /// </summary>
    public static class WeaponVisualAttachment
    {
        // ---------------------------------------------------------------
        // Constants
        // ---------------------------------------------------------------
        private const string WeaponBasePath =
            "res://assets/3d models/Equipment/Low Poly Medieval Weapons/";

        private const string RightHandBoneName = "hand_r";
        private static readonly string[] RightHandBoneCandidates =
        {
            RightHandBoneName,
            "Hand_R",
            "hand.R",
            "Hand.R",
            "r_hand",
            "RightHand",
            "Right_Hand",
            "mixamorig:RightHand"
        };
        private const string MainHandAttachmentName = "WeaponAttachment_MainHand";
        private const string OffHandAttachmentName  = "WeaponAttachment_OffHand";

        // ---------------------------------------------------------------
        // WeaponType → relative FBX path mapping
        // One representative model per category; covers all D&D 5e weapon types.
        // ---------------------------------------------------------------
        private static readonly Dictionary<WeaponType, string> WeaponFbxPaths =
            new Dictionary<WeaponType, string>
        {
            // Swords
            { WeaponType.Dagger,        "Swords/Dagger.fbx"            },
            { WeaponType.Shortsword,    "Swords/Sword.fbx"             },
            { WeaponType.Scimitar,      "Swords/Falchion Cleaver.fbx"  },
            { WeaponType.Rapier,        "Swords/Messer.fbx"            },
            { WeaponType.Longsword,     "Swords/Falchion.fbx"          },
            { WeaponType.Greatsword,    "Swords/Greatsword.fbx"        },
            { WeaponType.Whip,          "Swords/War Cleaver.fbx"       },

            // Axes
            { WeaponType.Handaxe,       "Axes/Axe.fbx"                 },
            { WeaponType.Battleaxe,     "Axes/Battle Axe.fbx"          },
            { WeaponType.Greataxe,      "Axes/Two handed Axe.fbx"      },
            { WeaponType.Halberd,       "Axes/Halberd.fbx"             },
            { WeaponType.Glaive,        "Axes/Halberd.fbx"             },
            { WeaponType.Pike,          "Axes/Halberd.fbx"             },

            // Spears & polearms
            { WeaponType.Spear,         "Spears/Spear.fbx"             },
            { WeaponType.Javelin,       "Spears/Javelin.fbx"           },
            { WeaponType.Lance,         "Spears/Lance.fbx"             },
            { WeaponType.Trident,       "Spears/Spear.fbx"             },

            // Clubs / staffs
            { WeaponType.Club,          "Spears/Polearm.fbx"           },
            { WeaponType.Greatclub,     "Spears/Polearm.fbx"           },
            { WeaponType.Quarterstaff,  "Spears/Polearm.fbx"           },

            // Maces
            { WeaponType.Mace,          "Maces/Mace.fbx"               },
            { WeaponType.Morningstar,   "Maces/Spiked Mace.fbx"        },
            { WeaponType.Flail,         "Maces/Flail.fbx"              },
            { WeaponType.WarPick,       "Maces/Spiked Club.fbx"        },

            // Hammers
            { WeaponType.LightHammer,   "Hammers/Hammer.fbx"           },
            { WeaponType.Warhammer,     "Hammers/Warhammer.fbx"        },
            { WeaponType.Maul,          "Hammers/Two-Handed Hammer.fbx"},

            // Bows & crossbows
            { WeaponType.Shortbow,      "Bows/Recurve Bow.fbx"         },
            { WeaponType.Longbow,       "Bows/Long Bow.fbx"            },
            { WeaponType.LightCrossbow, "Bows/Crossbow.fbx"            },
            { WeaponType.HeavyCrossbow, "Bows/Crossbow.fbx"            },
            { WeaponType.HandCrossbow,  "Bows/Crossbow.fbx"            },

            // Misc
            { WeaponType.Sickle,        "Farming/Sickle.fbx"           },
            { WeaponType.Dart,          "Spears/Javelin.fbx"           },
        };

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Attach a weapon mesh to the character's right hand bone.
        /// Any previously attached weapon is removed first.
        /// Returns the created BoneAttachment3D, or null on failure.
        /// </summary>
        /// <param name="modelRoot">The Node3D that is the root of the character model.</param>
        /// <param name="weapon">Weapon definition to visualise (must not be null).</param>
        /// <param name="settings">Rotation / offset / scale tweaks (use default if null).</param>
        public static BoneAttachment3D AttachMainHandWeapon(
            Node3D modelRoot,
            WeaponDefinition weapon,
            WeaponAttachmentSettings settings = null)
        {
            if (modelRoot == null || weapon == null)
                return null;

            settings ??= WeaponAttachmentSettings.Default;

            // Clean up existing attachment first
            RemoveMainHandWeapon(modelRoot);

            if (!WeaponFbxPaths.TryGetValue(weapon.WeaponType, out string relativePath))
            {
                RuntimeSafety.Log($"[WeaponVisual] No FBX mapping for WeaponType '{weapon.WeaponType}' - weapon hidden.");
                return null;
            }

            string fullPath = WeaponBasePath + relativePath;

            var skeleton = FindSkeleton(modelRoot);
            if (skeleton == null)
            {
                RuntimeSafety.Log($"[WeaponVisual][WARN] No Skeleton3D found under modelRoot '{modelRoot.Name}'.");
                return null;
            }

            if (!TryResolveRightHandBone(skeleton, out var resolvedBoneName))
            {
                RuntimeSafety.Log($"[WeaponVisual][WARN] No right-hand bone found in skeleton '{skeleton.Name}'. Weapon hidden.");
                return null;
            }

            // Load the weapon scene
            Node3D weaponNode = LoadWeaponScene(fullPath, weapon, settings);
            if (weaponNode == null)
                return null;

            // Create BoneAttachment3D on the skeleton.
            // BoneName must be assigned AFTER AddChild so the skeleton can resolve it.
            var attachment = new BoneAttachment3D();
            attachment.Name = MainHandAttachmentName;
            skeleton.AddChild(attachment);
            attachment.BoneName = resolvedBoneName;
            attachment.AddChild(weaponNode);

            RuntimeSafety.Log($"[WeaponVisual] Attached '{weapon.Name}' ({weapon.WeaponType}) -> {relativePath}");
            return attachment;
        }

        /// <summary>
        /// Remove the main-hand weapon attachment from the model, if any.
        /// </summary>
        public static void RemoveMainHandWeapon(Node3D modelRoot)
        {
            if (modelRoot == null) return;
            var skeleton = FindSkeleton(modelRoot);
            skeleton?.GetNodeOrNull<BoneAttachment3D>(MainHandAttachmentName)?.QueueFree();
        }

        // ---------------------------------------------------------------
        // Private helpers
        // ---------------------------------------------------------------

        /// <summary>Depth-first search for the first Skeleton3D under root.</summary>
        private static Skeleton3D FindSkeleton(Node3D root)
        {
            foreach (var node in root.FindChildren("*", "Skeleton3D", owned: false))
            {
                if (node is Skeleton3D sk)
                    return sk;
            }
            return null;
        }

        private static bool TryResolveRightHandBone(Skeleton3D skeleton, out string boneName)
        {
            boneName = null;
            if (skeleton == null)
                return false;

            foreach (var candidate in RightHandBoneCandidates)
            {
                int idx = skeleton.FindBone(candidate);
                if (idx >= 0)
                {
                    boneName = skeleton.GetBoneName(idx);
                    return true;
                }
            }

            // Heuristic fallback for rigs with custom naming.
            for (int i = 0; i < skeleton.GetBoneCount(); i++)
            {
                string name = skeleton.GetBoneName(i);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string normalized = name.ToLowerInvariant();
                bool containsHand = normalized.Contains("hand");
                bool containsRight = normalized.Contains("right") ||
                                     normalized.Contains("_r") ||
                                     normalized.EndsWith("r");
                bool containsLeft = normalized.Contains("left") || normalized.Contains("_l");

                if (containsHand && containsRight && !containsLeft)
                {
                    boneName = name;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Load and configure the weapon node from the FBX PackedScene.
        /// Returns null on any failure (missing resource, import not ready, etc.).
        /// </summary>
        private static Node3D LoadWeaponScene(
            string resourcePath,
            WeaponDefinition weapon,
            WeaponAttachmentSettings settings)
        {
            if (!ResourceLoader.Exists(resourcePath))
            {
                RuntimeSafety.Log($"[WeaponVisual][WARN] Resource not found (FBX not yet imported?): {resourcePath}");
                return null;
            }

            var scene = ResourceLoader.Load<PackedScene>(resourcePath);
            if (scene == null)
            {
                RuntimeSafety.Log($"[WeaponVisual][WARN] Failed to load PackedScene: {resourcePath}");
                return null;
            }

            var instance = scene.Instantiate() as Node3D;
            if (instance == null)
            {
                RuntimeSafety.Log($"[WeaponVisual][WARN] Instantiated node is not a Node3D: {resourcePath}");
                return null;
            }

            // ----------------------------------------------------------------
            // The FBX weapon models are authored vertically:
            //   • Y-up (tip of weapon toward +Y)
            //   • Pivot at the bottom of the handle
            //
            // We rotate them into the character's grip pose and shift the pivot
            // so the grip centre sits at the hand_r bone origin.
            //
            // Default (WeaponAttachmentSettings.Default):
            //   Rotation: X=-90°  →  weapon points along +Z (character's forward)
            //   Position: shifted so the handle mid-point is at the palm.
            // ----------------------------------------------------------------
            instance.RotationDegrees = settings.RotationDegrees;
            instance.Position        = settings.PositionOffset;

            // Two-handed weapons are typically a bit larger; keep their authored scale.
            bool isTwoHanded = weapon.Properties.HasFlag(WeaponProperty.TwoHanded);
            float scaleMult  = isTwoHanded ? settings.TwoHandedScaleMultiplier : settings.OneHandedScaleMultiplier;
            instance.Scale   = Vector3.One * scaleMult;

            return instance;
        }
    }

    // ===================================================================
    // Settings struct — exported on CombatantVisual so designers can tweak
    // per-scene without touching code.
    // ===================================================================

    /// <summary>
    /// Tweakable parameters that control how a weapon mesh is oriented
    /// once it is parented to the hand_r BoneAttachment3D.
    ///
    /// The defaults assume a UE-origin skeleton (hand_r bone Y-axis toward
    /// fingers) with vertically-authored weapon meshes.
    /// </summary>
    public class WeaponAttachmentSettings
    {
        /// <summary>Euler rotation in degrees applied to the weapon Node3D.</summary>
        public Vector3 RotationDegrees { get; set; } = new Vector3(-90f, 0f, 0f);

        /// <summary>
        /// Local-space position offset that slides the weapon handle into the
        /// character's palm. Positive Y moves toward the weapon tip.
        /// </summary>
        public Vector3 PositionOffset { get; set; } = new Vector3(0f, 0.06f, 0.0f);

        /// <summary>Uniform scale for one-handed weapons.</summary>
        public float OneHandedScaleMultiplier { get; set; } = 1.0f;

        /// <summary>Uniform scale for two-handed weapons.</summary>
        public float TwoHandedScaleMultiplier { get; set; } = 1.0f;

        /// <summary>Sensible defaults matching the character's hand_r bone orientation.</summary>
        public static WeaponAttachmentSettings Default { get; } = new WeaponAttachmentSettings();
    }
}
