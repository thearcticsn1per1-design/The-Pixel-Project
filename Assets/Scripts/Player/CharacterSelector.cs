using UnityEngine;
using System;
using System.Collections.Generic;
using PixelProject.Core;

namespace PixelProject.Player
{
    /// <summary>
    /// Manages character selection and switching during gameplay.
    /// Works with CharacterSpriteData to provide character options.
    /// </summary>
    public class CharacterSelector : MonoBehaviour
    {
        public static CharacterSelector Instance { get; private set; }

        [Header("Available Characters")]
        [SerializeField] private List<CharacterSpriteData> availableCharacters = new List<CharacterSpriteData>();
        [SerializeField] private int defaultCharacterIndex = 0;

        [Header("Character Stats Modifiers")]
        [SerializeField] private List<CharacterStatPreset> characterPresets = new List<CharacterStatPreset>();

        private int currentCharacterIndex;
        private CharacterSpriteData currentCharacter;

        public CharacterSpriteData CurrentCharacter => currentCharacter;
        public int CurrentCharacterIndex => currentCharacterIndex;
        public IReadOnlyList<CharacterSpriteData> AvailableCharacters => availableCharacters;

        public event Action<CharacterSpriteData> OnCharacterChanged;
        public event Action<int> OnCharacterUnlocked;

        private const string SELECTED_CHARACTER_KEY = "SelectedCharacter";
        private const string UNLOCKED_CHARACTERS_KEY = "UnlockedCharacters";

        private HashSet<string> unlockedCharacters = new HashSet<string>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadUnlockedCharacters();
            LoadSelectedCharacter();
        }

        private void Start()
        {
            // Apply character to player if available
            ApplyCharacterToPlayer();
        }

        /// <summary>
        /// Selects a character by index.
        /// </summary>
        public bool SelectCharacter(int index)
        {
            if (index < 0 || index >= availableCharacters.Count) return false;

            var character = availableCharacters[index];
            if (character == null) return false;

            // Check if character is unlocked
            if (!IsCharacterUnlocked(character))
            {
                Debug.Log($"Character '{character.characterName}' is locked!");
                return false;
            }

            currentCharacterIndex = index;
            currentCharacter = character;

            SaveSelectedCharacter();
            ApplyCharacterToPlayer();

            OnCharacterChanged?.Invoke(currentCharacter);
            return true;
        }

        /// <summary>
        /// Selects a character by name.
        /// </summary>
        public bool SelectCharacter(string characterName)
        {
            int index = availableCharacters.FindIndex(c => c != null && c.characterName == characterName);
            if (index >= 0)
            {
                return SelectCharacter(index);
            }
            return false;
        }

        /// <summary>
        /// Cycles to the next available character.
        /// </summary>
        public void NextCharacter()
        {
            int startIndex = currentCharacterIndex;
            int nextIndex = (currentCharacterIndex + 1) % availableCharacters.Count;

            // Find next unlocked character
            while (nextIndex != startIndex)
            {
                if (availableCharacters[nextIndex] != null && IsCharacterUnlocked(availableCharacters[nextIndex]))
                {
                    SelectCharacter(nextIndex);
                    return;
                }
                nextIndex = (nextIndex + 1) % availableCharacters.Count;
            }
        }

        /// <summary>
        /// Cycles to the previous available character.
        /// </summary>
        public void PreviousCharacter()
        {
            int startIndex = currentCharacterIndex;
            int prevIndex = (currentCharacterIndex - 1 + availableCharacters.Count) % availableCharacters.Count;

            // Find previous unlocked character
            while (prevIndex != startIndex)
            {
                if (availableCharacters[prevIndex] != null && IsCharacterUnlocked(availableCharacters[prevIndex]))
                {
                    SelectCharacter(prevIndex);
                    return;
                }
                prevIndex = (prevIndex - 1 + availableCharacters.Count) % availableCharacters.Count;
            }
        }

        /// <summary>
        /// Checks if a character is unlocked.
        /// </summary>
        public bool IsCharacterUnlocked(CharacterSpriteData character)
        {
            if (character == null) return false;

            // First character is always unlocked
            int index = availableCharacters.IndexOf(character);
            if (index == 0) return true;

            return unlockedCharacters.Contains(character.characterName);
        }

        /// <summary>
        /// Checks if a character is unlocked by index.
        /// </summary>
        public bool IsCharacterUnlocked(int index)
        {
            if (index < 0 || index >= availableCharacters.Count) return false;
            return IsCharacterUnlocked(availableCharacters[index]);
        }

        /// <summary>
        /// Unlocks a character.
        /// </summary>
        public void UnlockCharacter(CharacterSpriteData character)
        {
            if (character == null) return;

            if (unlockedCharacters.Add(character.characterName))
            {
                SaveUnlockedCharacters();
                int index = availableCharacters.IndexOf(character);
                OnCharacterUnlocked?.Invoke(index);
            }
        }

        /// <summary>
        /// Unlocks a character by name.
        /// </summary>
        public void UnlockCharacter(string characterName)
        {
            var character = availableCharacters.Find(c => c != null && c.characterName == characterName);
            if (character != null)
            {
                UnlockCharacter(character);
            }
        }

        /// <summary>
        /// Gets the stat preset for a character.
        /// </summary>
        public CharacterStatPreset GetCharacterPreset(CharacterSpriteData character)
        {
            if (character == null) return null;

            return characterPresets.Find(p => p.characterName == character.characterName);
        }

        /// <summary>
        /// Gets the stat preset for the current character.
        /// </summary>
        public CharacterStatPreset GetCurrentCharacterPreset()
        {
            return GetCharacterPreset(currentCharacter);
        }

        private void ApplyCharacterToPlayer()
        {
            if (currentCharacter == null) return;

            var player = PlayerController.Instance;
            if (player == null) return;

            var animator = player.Animator;
            if (animator != null)
            {
                animator.SetCharacterData(currentCharacter);
            }

            // Apply stat modifiers if player has stats component
            var playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                var preset = GetCurrentCharacterPreset();
                if (preset != null)
                {
                    ApplyCharacterStats(playerStats, preset);
                }
            }
        }

        private void ApplyCharacterStats(PlayerStats stats, CharacterStatPreset preset)
        {
            // Apply character-specific stat modifiers
            // These are base modifiers that define the character's playstyle

            if (preset.healthModifier != 1f)
            {
                stats.AddModifier(StatType.MaxHealth, new StatModifier(
                    preset.healthModifier - 1f, // Convert multiplier to additive bonus
                    ModifierType.Multiplicative,
                    "CharacterClass"
                ));
            }

            if (preset.damageModifier != 1f)
            {
                stats.AddModifier(StatType.Damage, new StatModifier(
                    preset.damageModifier - 1f,
                    ModifierType.Multiplicative,
                    "CharacterClass"
                ));
            }

            if (preset.moveSpeedModifier != 1f)
            {
                stats.AddModifier(StatType.MoveSpeed, new StatModifier(
                    preset.moveSpeedModifier - 1f,
                    ModifierType.Multiplicative,
                    "CharacterClass"
                ));
            }

            if (preset.attackSpeedModifier != 1f)
            {
                stats.AddModifier(StatType.AttackSpeed, new StatModifier(
                    preset.attackSpeedModifier - 1f,
                    ModifierType.Multiplicative,
                    "CharacterClass"
                ));
            }
        }

        private void LoadSelectedCharacter()
        {
            string savedName = PlayerPrefs.GetString(SELECTED_CHARACTER_KEY, "");

            if (!string.IsNullOrEmpty(savedName))
            {
                int index = availableCharacters.FindIndex(c => c != null && c.characterName == savedName);
                if (index >= 0 && IsCharacterUnlocked(index))
                {
                    currentCharacterIndex = index;
                    currentCharacter = availableCharacters[index];
                    return;
                }
            }

            // Default to first character
            if (availableCharacters.Count > 0 && availableCharacters[defaultCharacterIndex] != null)
            {
                currentCharacterIndex = defaultCharacterIndex;
                currentCharacter = availableCharacters[defaultCharacterIndex];
            }
        }

        private void SaveSelectedCharacter()
        {
            if (currentCharacter != null)
            {
                PlayerPrefs.SetString(SELECTED_CHARACTER_KEY, currentCharacter.characterName);
                PlayerPrefs.Save();
            }
        }

        private void LoadUnlockedCharacters()
        {
            string data = PlayerPrefs.GetString(UNLOCKED_CHARACTERS_KEY, "");
            if (!string.IsNullOrEmpty(data))
            {
                string[] names = data.Split(',');
                foreach (string name in names)
                {
                    if (!string.IsNullOrEmpty(name))
                    {
                        unlockedCharacters.Add(name);
                    }
                }
            }
        }

        private void SaveUnlockedCharacters()
        {
            string data = string.Join(",", unlockedCharacters);
            PlayerPrefs.SetString(UNLOCKED_CHARACTERS_KEY, data);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Resets all character unlocks (for debugging).
        /// </summary>
        public void ResetUnlocks()
        {
            unlockedCharacters.Clear();
            SaveUnlockedCharacters();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    /// <summary>
    /// Stat preset for a character class, defining their base stat modifiers.
    /// </summary>
    [Serializable]
    public class CharacterStatPreset
    {
        public string characterName;

        [Header("Base Stats")]
        [Range(0.5f, 2f)] public float healthModifier = 1f;
        [Range(0.5f, 2f)] public float damageModifier = 1f;
        [Range(0.5f, 2f)] public float moveSpeedModifier = 1f;
        [Range(0.5f, 2f)] public float attackSpeedModifier = 1f;

        [Header("Special Bonuses")]
        public float critChanceBonus = 0f;
        public float critDamageBonus = 0f;
        public float armorBonus = 0f;
        public float dodgeChanceBonus = 0f;

        [Header("Starting Equipment")]
        public string startingWeaponId;
        public string[] startingItemIds;

        [Header("Description")]
        [TextArea(2, 4)] public string classDescription;
    }
}
