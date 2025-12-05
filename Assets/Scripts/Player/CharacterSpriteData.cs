using UnityEngine;
using System;
using System.Collections.Generic;

namespace PixelProject.Player
{
    /// <summary>
    /// ScriptableObject containing all sprite and animation data for a character.
    /// Designed to work with SmallScale Interactive Top-Down Pixel Characters format.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "Pixel Project/Character Sprite Data")]
    public class CharacterSpriteData : ScriptableObject
    {
        [Header("Character Info")]
        public string characterName;
        public string characterClass;
        [TextArea(2, 4)]
        public string description;
        public Sprite portrait;

        [Header("Default Settings")]
        public float defaultFrameRate = 12f;
        public Direction8 defaultDirection = Direction8.South;
        public AnimationState defaultState = AnimationState.Idle;

        [Header("Animations")]
        [SerializeField] private List<CharacterAnimationData> animations = new List<CharacterAnimationData>();

        // Cached lookup for fast animation retrieval
        private Dictionary<AnimationState, CharacterAnimationData> animationLookup;

        private void OnEnable()
        {
            BuildAnimationLookup();
        }

        private void OnValidate()
        {
            BuildAnimationLookup();
        }

        private void BuildAnimationLookup()
        {
            animationLookup = new Dictionary<AnimationState, CharacterAnimationData>();
            foreach (var anim in animations)
            {
                if (anim != null && !animationLookup.ContainsKey(anim.State))
                {
                    animationLookup[anim.State] = anim;
                }
            }
        }

        /// <summary>
        /// Gets the animation data for a specific state.
        /// </summary>
        public CharacterAnimationData GetAnimation(AnimationState state)
        {
            if (animationLookup == null)
            {
                BuildAnimationLookup();
            }

            if (animationLookup.TryGetValue(state, out var anim))
            {
                return anim;
            }

            // Fallback to Idle if requested animation doesn't exist
            if (state != AnimationState.Idle && animationLookup.TryGetValue(AnimationState.Idle, out anim))
            {
                return anim;
            }

            return null;
        }

        /// <summary>
        /// Checks if the character has a specific animation.
        /// </summary>
        public bool HasAnimation(AnimationState state)
        {
            if (animationLookup == null)
            {
                BuildAnimationLookup();
            }
            return animationLookup.ContainsKey(state);
        }

        /// <summary>
        /// Gets all available animation states for this character.
        /// </summary>
        public IEnumerable<AnimationState> GetAvailableAnimations()
        {
            if (animationLookup == null)
            {
                BuildAnimationLookup();
            }
            return animationLookup.Keys;
        }

        /// <summary>
        /// Adds or updates an animation for this character.
        /// </summary>
        public void SetAnimation(CharacterAnimationData animationData)
        {
            if (animationData == null) return;

            // Remove existing animation with same state
            animations.RemoveAll(a => a != null && a.State == animationData.State);
            animations.Add(animationData);
            BuildAnimationLookup();
        }
    }

    /// <summary>
    /// Contains animation data for a single animation state with 8-directional support.
    /// </summary>
    [Serializable]
    public class CharacterAnimationData
    {
        [SerializeField] private AnimationState state;
        [SerializeField] private float customFrameRate = 0f; // 0 means use default
        [SerializeField] private bool loop = true;

        [Header("Directional Sprites")]
        [SerializeField] private DirectionalSpriteSet directionalSprites;

        public AnimationState State => state;
        public float CustomFrameRate => customFrameRate;
        public bool Loop => loop;
        public DirectionalSpriteSet DirectionalSprites => directionalSprites;

        public CharacterAnimationData(AnimationState animState)
        {
            state = animState;
            directionalSprites = new DirectionalSpriteSet();
        }

        /// <summary>
        /// Gets sprites for a specific direction.
        /// </summary>
        public Sprite[] GetSpritesForDirection(Direction8 direction)
        {
            if (directionalSprites == null) return null;
            return directionalSprites.GetSprites(direction);
        }

        /// <summary>
        /// Sets sprites for a specific direction.
        /// </summary>
        public void SetSpritesForDirection(Direction8 direction, Sprite[] sprites)
        {
            if (directionalSprites == null)
            {
                directionalSprites = new DirectionalSpriteSet();
            }
            directionalSprites.SetSprites(direction, sprites);
        }
    }

    /// <summary>
    /// Container for 8-directional sprite arrays.
    /// </summary>
    [Serializable]
    public class DirectionalSpriteSet
    {
        [SerializeField] private Sprite[] north;
        [SerializeField] private Sprite[] northEast;
        [SerializeField] private Sprite[] east;
        [SerializeField] private Sprite[] southEast;
        [SerializeField] private Sprite[] south;
        [SerializeField] private Sprite[] southWest;
        [SerializeField] private Sprite[] west;
        [SerializeField] private Sprite[] northWest;

        public Sprite[] GetSprites(Direction8 direction)
        {
            return direction switch
            {
                Direction8.North => north,
                Direction8.NorthEast => northEast,
                Direction8.East => east,
                Direction8.SouthEast => southEast,
                Direction8.South => south,
                Direction8.SouthWest => southWest,
                Direction8.West => west,
                Direction8.NorthWest => northWest,
                _ => south
            };
        }

        public void SetSprites(Direction8 direction, Sprite[] sprites)
        {
            switch (direction)
            {
                case Direction8.North:
                    north = sprites;
                    break;
                case Direction8.NorthEast:
                    northEast = sprites;
                    break;
                case Direction8.East:
                    east = sprites;
                    break;
                case Direction8.SouthEast:
                    southEast = sprites;
                    break;
                case Direction8.South:
                    south = sprites;
                    break;
                case Direction8.SouthWest:
                    southWest = sprites;
                    break;
                case Direction8.West:
                    west = sprites;
                    break;
                case Direction8.NorthWest:
                    northWest = sprites;
                    break;
            }
        }

        /// <summary>
        /// Checks if any direction has sprites assigned.
        /// </summary>
        public bool HasAnySprites()
        {
            return (north != null && north.Length > 0) ||
                   (northEast != null && northEast.Length > 0) ||
                   (east != null && east.Length > 0) ||
                   (southEast != null && southEast.Length > 0) ||
                   (south != null && south.Length > 0) ||
                   (southWest != null && southWest.Length > 0) ||
                   (west != null && west.Length > 0) ||
                   (northWest != null && northWest.Length > 0);
        }

        /// <summary>
        /// Gets the frame count for a direction (assumes all directions have same frame count).
        /// </summary>
        public int GetFrameCount(Direction8 direction)
        {
            var sprites = GetSprites(direction);
            return sprites?.Length ?? 0;
        }
    }

    /// <summary>
    /// Predefined character class configurations matching the asset pack.
    /// </summary>
    public static class CharacterClassPresets
    {
        public static readonly string[] AvailableClasses = new[]
        {
            "Knight",
            "Archer",
            "Wizard",
            "Paladin",
            "CamoArcher",
            "Mage",
            "DeathKnight",
            "DarkLord",
            "Samurai"
        };

        /// <summary>
        /// Gets the expected animation states for the Top-Down Pixel Characters asset pack.
        /// </summary>
        public static AnimationState[] GetAssetPackAnimations()
        {
            return new[]
            {
                AnimationState.Idle,
                AnimationState.Idle2,
                AnimationState.Walk,
                AnimationState.Run,
                AnimationState.RunBackwards,
                AnimationState.StrafeLeft,
                AnimationState.StrafeRight,
                AnimationState.CrouchIdle,
                AnimationState.CrouchRun,
                AnimationState.Rolling,
                AnimationState.Slide,
                AnimationState.SlideStart,
                AnimationState.SlideEnd,
                AnimationState.Melee,
                AnimationState.Melee2,
                AnimationState.MeleeRun,
                AnimationState.MeleeSpin,
                AnimationState.Kick,
                AnimationState.Pummel,
                AnimationState.CastSpell,
                AnimationState.ShieldBlockStart,
                AnimationState.ShieldBlockMid,
                AnimationState.Special1,
                AnimationState.Special2,
                AnimationState.TakeDamage,
                AnimationState.Die,
                AnimationState.FrontFlip,
                AnimationState.Turn180,
                AnimationState.UnSheathSword
            };
        }

        /// <summary>
        /// Maps animation folder names from the asset pack to AnimationState enum.
        /// </summary>
        public static Dictionary<string, AnimationState> GetAnimationNameMapping()
        {
            return new Dictionary<string, AnimationState>(StringComparer.OrdinalIgnoreCase)
            {
                { "Idle", AnimationState.Idle },
                { "Idle2", AnimationState.Idle2 },
                { "Walk", AnimationState.Walk },
                { "Run", AnimationState.Run },
                { "RunBackwards", AnimationState.RunBackwards },
                { "StrafeLeft", AnimationState.StrafeLeft },
                { "StrafeRight", AnimationState.StrafeRight },
                { "CrouchIdle", AnimationState.CrouchIdle },
                { "CrouchRun", AnimationState.CrouchRun },
                { "Rolling", AnimationState.Rolling },
                { "Slide", AnimationState.Slide },
                { "SlideStart", AnimationState.SlideStart },
                { "SlideEnd", AnimationState.SlideEnd },
                { "Melee", AnimationState.Melee },
                { "Melee2", AnimationState.Melee2 },
                { "MeleeRun", AnimationState.MeleeRun },
                { "MeleeSpin", AnimationState.MeleeSpin },
                { "Kick", AnimationState.Kick },
                { "Pummel", AnimationState.Pummel },
                { "CastSpell", AnimationState.CastSpell },
                { "ShieldBlockStart", AnimationState.ShieldBlockStart },
                { "ShieldBlockMid", AnimationState.ShieldBlockMid },
                { "Special1", AnimationState.Special1 },
                { "Special2", AnimationState.Special2 },
                { "TakeDamage", AnimationState.TakeDamage },
                { "Die", AnimationState.Die },
                { "FrontFlip", AnimationState.FrontFlip },
                { "Turn180", AnimationState.Turn180 },
                { "UnSheathSword", AnimationState.UnSheathSword }
            };
        }

        /// <summary>
        /// Maps direction folder names from the asset pack to Direction8 enum.
        /// </summary>
        public static Dictionary<string, Direction8> GetDirectionNameMapping()
        {
            return new Dictionary<string, Direction8>(StringComparer.OrdinalIgnoreCase)
            {
                { "N", Direction8.North },
                { "NE", Direction8.NorthEast },
                { "E", Direction8.East },
                { "SE", Direction8.SouthEast },
                { "S", Direction8.South },
                { "SW", Direction8.SouthWest },
                { "W", Direction8.West },
                { "NW", Direction8.NorthWest },
                // Alternative naming conventions
                { "North", Direction8.North },
                { "NorthEast", Direction8.NorthEast },
                { "East", Direction8.East },
                { "SouthEast", Direction8.SouthEast },
                { "South", Direction8.South },
                { "SouthWest", Direction8.SouthWest },
                { "West", Direction8.West },
                { "NorthWest", Direction8.NorthWest }
            };
        }
    }
}
