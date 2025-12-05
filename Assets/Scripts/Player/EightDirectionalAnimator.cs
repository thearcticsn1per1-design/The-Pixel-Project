using UnityEngine;
using System;
using System.Collections.Generic;

namespace PixelProject.Player
{
    /// <summary>
    /// Handles 8-directional sprite animations for top-down characters.
    /// Supports the SmallScale Interactive Top-Down Pixel Characters format.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class EightDirectionalAnimator : MonoBehaviour
    {
        [Header("Character Data")]
        [SerializeField] private CharacterSpriteData characterData;

        [Header("Animation Settings")]
        [SerializeField] private float frameRate = 12f;
        [SerializeField] private bool animateOnStart = true;

        [Header("Direction Settings")]
        [SerializeField] private DirectionMode directionMode = DirectionMode.EightWay;
        [SerializeField] private bool flipForLeftDirections = true;

        private SpriteRenderer spriteRenderer;
        private AnimationState currentState = AnimationState.Idle;
        private Direction8 currentDirection = Direction8.South;
        private int currentFrame = 0;
        private float frameTimer = 0f;
        private bool isPlaying = true;
        private bool isLooping = true;
        private Action onAnimationComplete;

        // Cached sprite arrays for current animation
        private Sprite[] currentSprites;
        private bool currentFlipX;

        public AnimationState CurrentState => currentState;
        public Direction8 CurrentDirection => currentDirection;
        public bool IsPlaying => isPlaying;
        public CharacterSpriteData CharacterData => characterData;

        public event Action<AnimationState> OnAnimationChanged;
        public event Action OnAnimationFinished;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (animateOnStart && characterData != null)
            {
                PlayAnimation(AnimationState.Idle, true);
            }
        }

        private void Update()
        {
            if (!isPlaying || currentSprites == null || currentSprites.Length == 0) return;

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / frameRate;

            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                AdvanceFrame();
            }
        }

        private void AdvanceFrame()
        {
            currentFrame++;

            if (currentFrame >= currentSprites.Length)
            {
                if (isLooping)
                {
                    currentFrame = 0;
                }
                else
                {
                    currentFrame = currentSprites.Length - 1;
                    isPlaying = false;
                    OnAnimationFinished?.Invoke();
                    onAnimationComplete?.Invoke();
                    onAnimationComplete = null;
                    return;
                }
            }

            UpdateSprite();
        }

        private void UpdateSprite()
        {
            if (currentSprites != null && currentFrame < currentSprites.Length)
            {
                spriteRenderer.sprite = currentSprites[currentFrame];
                spriteRenderer.flipX = currentFlipX;
            }
        }

        /// <summary>
        /// Sets the facing direction based on a movement or aim vector.
        /// </summary>
        public void SetDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.01f) return;

            Direction8 newDirection = VectorToDirection8(direction);
            if (newDirection != currentDirection)
            {
                currentDirection = newDirection;
                RefreshCurrentAnimation();
            }
        }

        /// <summary>
        /// Sets the facing direction directly.
        /// </summary>
        public void SetDirection(Direction8 direction)
        {
            if (direction != currentDirection)
            {
                currentDirection = direction;
                RefreshCurrentAnimation();
            }
        }

        /// <summary>
        /// Plays an animation state.
        /// </summary>
        public void PlayAnimation(AnimationState state, bool loop = true, Action onComplete = null)
        {
            if (characterData == null) return;

            // Don't restart if same animation is playing
            if (state == currentState && isPlaying && isLooping == loop) return;

            currentState = state;
            isLooping = loop;
            onAnimationComplete = onComplete;
            currentFrame = 0;
            frameTimer = 0f;
            isPlaying = true;

            RefreshCurrentAnimation();
            OnAnimationChanged?.Invoke(state);
        }

        /// <summary>
        /// Plays a one-shot animation then returns to the previous state.
        /// </summary>
        public void PlayOneShotAnimation(AnimationState state, AnimationState returnState = AnimationState.Idle)
        {
            PlayAnimation(state, false, () => PlayAnimation(returnState, true));
        }

        private void RefreshCurrentAnimation()
        {
            if (characterData == null) return;

            var animData = characterData.GetAnimation(currentState);
            if (animData == null || animData.DirectionalSprites == null) return;

            // Get the appropriate direction sprites
            (currentSprites, currentFlipX) = GetSpritesForDirection(animData, currentDirection);

            // Update frame rate if animation has custom rate
            if (animData.CustomFrameRate > 0)
            {
                frameRate = animData.CustomFrameRate;
            }

            // Ensure frame is valid
            if (currentSprites != null && currentFrame >= currentSprites.Length)
            {
                currentFrame = 0;
            }

            UpdateSprite();
        }

        private (Sprite[], bool) GetSpritesForDirection(CharacterAnimationData animData, Direction8 direction)
        {
            bool flipX = false;
            Direction8 spriteDirection = direction;

            // Handle flipping for left-side directions
            if (flipForLeftDirections)
            {
                switch (direction)
                {
                    case Direction8.West:
                        spriteDirection = Direction8.East;
                        flipX = true;
                        break;
                    case Direction8.NorthWest:
                        spriteDirection = Direction8.NorthEast;
                        flipX = true;
                        break;
                    case Direction8.SouthWest:
                        spriteDirection = Direction8.SouthEast;
                        flipX = true;
                        break;
                }
            }

            // Get sprites for the direction
            var dirSprites = animData.GetSpritesForDirection(spriteDirection);
            if (dirSprites != null && dirSprites.Length > 0)
            {
                return (dirSprites, flipX);
            }

            // Fallback to South direction
            return (animData.GetSpritesForDirection(Direction8.South), false);
        }

        private Direction8 VectorToDirection8(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // Normalize angle to 0-360
            if (angle < 0) angle += 360f;

            // 8-way direction with 45 degree segments
            if (directionMode == DirectionMode.EightWay)
            {
                if (angle >= 337.5f || angle < 22.5f) return Direction8.East;
                if (angle >= 22.5f && angle < 67.5f) return Direction8.NorthEast;
                if (angle >= 67.5f && angle < 112.5f) return Direction8.North;
                if (angle >= 112.5f && angle < 157.5f) return Direction8.NorthWest;
                if (angle >= 157.5f && angle < 202.5f) return Direction8.West;
                if (angle >= 202.5f && angle < 247.5f) return Direction8.SouthWest;
                if (angle >= 247.5f && angle < 292.5f) return Direction8.South;
                if (angle >= 292.5f && angle < 337.5f) return Direction8.SouthEast;
            }
            else // 4-way
            {
                if (angle >= 315f || angle < 45f) return Direction8.East;
                if (angle >= 45f && angle < 135f) return Direction8.North;
                if (angle >= 135f && angle < 225f) return Direction8.West;
                if (angle >= 225f && angle < 315f) return Direction8.South;
            }

            return Direction8.South;
        }

        public void Pause()
        {
            isPlaying = false;
        }

        public void Resume()
        {
            isPlaying = true;
        }

        public void Stop()
        {
            isPlaying = false;
            currentFrame = 0;
            frameTimer = 0f;
        }

        public void SetCharacterData(CharacterSpriteData data)
        {
            characterData = data;
            if (data != null)
            {
                RefreshCurrentAnimation();
            }
        }

        /// <summary>
        /// Triggers damage flash effect.
        /// </summary>
        public void TriggerDamageFlash(float duration = 0.1f, Color? flashColor = null)
        {
            StartCoroutine(DamageFlashCoroutine(duration, flashColor ?? Color.red));
        }

        private System.Collections.IEnumerator DamageFlashCoroutine(float duration, Color flashColor)
        {
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = flashColor;
            yield return new WaitForSeconds(duration);
            spriteRenderer.color = originalColor;
        }
    }

    public enum Direction8
    {
        North,      // Up
        NorthEast,
        East,       // Right
        SouthEast,
        South,      // Down
        SouthWest,
        West,       // Left
        NorthWest
    }

    public enum DirectionMode
    {
        FourWay,
        EightWay
    }

    public enum AnimationState
    {
        Idle,
        Idle2,
        Walk,
        Run,
        RunBackwards,
        StrafeLeft,
        StrafeRight,
        CrouchIdle,
        CrouchRun,
        Rolling,
        Slide,
        SlideStart,
        SlideEnd,
        Melee,
        Melee2,
        MeleeRun,
        MeleeSpin,
        Kick,
        Pummel,
        CastSpell,
        ShieldBlockStart,
        ShieldBlockMid,
        Special1,
        Special2,
        TakeDamage,
        Die,
        FrontFlip,
        Turn180,
        UnSheathSword
    }
}
