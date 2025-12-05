using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using PixelProject.Player;
using AnimState = PixelProject.Player.AnimationState;

namespace PixelProject.UI
{
    /// <summary>
    /// UI controller for the character selection screen.
    /// </summary>
    public class CharacterSelectUI : MonoBehaviour
    {
        [Header("Character Display")]
        [SerializeField] private Image characterPortrait;
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text characterClassText;
        [SerializeField] private TMP_Text characterDescriptionText;

        [Header("Stats Display")]
        [SerializeField] private StatBar healthBar;
        [SerializeField] private StatBar damageBar;
        [SerializeField] private StatBar speedBar;
        [SerializeField] private StatBar attackSpeedBar;

        [Header("Navigation")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text selectButtonText;

        [Header("Character Grid")]
        [SerializeField] private Transform characterGridParent;
        [SerializeField] private GameObject characterSlotPrefab;
        [SerializeField] private bool useGrid = false;

        [Header("Preview")]
        [SerializeField] private EightDirectionalAnimator previewAnimator;
        [SerializeField] private AnimState previewAnimation = AnimState.Idle;

        private int currentDisplayIndex = 0;
        private List<CharacterSlotUI> characterSlots = new List<CharacterSlotUI>();

        private void Start()
        {
            SetupButtons();

            if (useGrid)
            {
                CreateCharacterGrid();
            }

            RefreshDisplay();
        }

        private void OnEnable()
        {
            if (CharacterSelector.Instance != null)
            {
                currentDisplayIndex = CharacterSelector.Instance.CurrentCharacterIndex;
                RefreshDisplay();
            }
        }

        private void SetupButtons()
        {
            if (prevButton != null)
            {
                prevButton.onClick.AddListener(OnPrevClicked);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(OnNextClicked);
            }

            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectClicked);
            }
        }

        private void CreateCharacterGrid()
        {
            if (characterGridParent == null || characterSlotPrefab == null) return;

            var selector = CharacterSelector.Instance;
            if (selector == null) return;

            // Clear existing slots
            foreach (var slot in characterSlots)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }
            characterSlots.Clear();

            // Create slots for each character
            for (int i = 0; i < selector.AvailableCharacters.Count; i++)
            {
                var character = selector.AvailableCharacters[i];
                if (character == null) continue;

                GameObject slotObj = Instantiate(characterSlotPrefab, characterGridParent);
                CharacterSlotUI slot = slotObj.GetComponent<CharacterSlotUI>();

                if (slot != null)
                {
                    int index = i; // Capture for lambda
                    slot.Setup(character, selector.IsCharacterUnlocked(i), () => OnCharacterSlotClicked(index));
                    characterSlots.Add(slot);
                }
            }
        }

        private void OnCharacterSlotClicked(int index)
        {
            currentDisplayIndex = index;
            RefreshDisplay();
        }

        public void OnPrevClicked()
        {
            var selector = CharacterSelector.Instance;
            if (selector == null) return;

            currentDisplayIndex--;
            if (currentDisplayIndex < 0)
            {
                currentDisplayIndex = selector.AvailableCharacters.Count - 1;
            }

            RefreshDisplay();
        }

        public void OnNextClicked()
        {
            var selector = CharacterSelector.Instance;
            if (selector == null) return;

            currentDisplayIndex++;
            if (currentDisplayIndex >= selector.AvailableCharacters.Count)
            {
                currentDisplayIndex = 0;
            }

            RefreshDisplay();
        }

        public void OnSelectClicked()
        {
            var selector = CharacterSelector.Instance;
            if (selector == null) return;

            if (selector.SelectCharacter(currentDisplayIndex))
            {
                // Success - could trigger transition or close screen
                RefreshDisplay();
            }
        }

        private void RefreshDisplay()
        {
            var selector = CharacterSelector.Instance;
            if (selector == null) return;

            if (currentDisplayIndex < 0 || currentDisplayIndex >= selector.AvailableCharacters.Count)
            {
                return;
            }

            var character = selector.AvailableCharacters[currentDisplayIndex];
            if (character == null) return;

            bool isUnlocked = selector.IsCharacterUnlocked(currentDisplayIndex);
            bool isSelected = currentDisplayIndex == selector.CurrentCharacterIndex;

            // Update portrait
            if (characterPortrait != null)
            {
                characterPortrait.sprite = character.portrait;
                characterPortrait.color = isUnlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);
            }

            // Update name
            if (characterNameText != null)
            {
                characterNameText.text = isUnlocked ? character.characterName : "???";
            }

            // Update class
            if (characterClassText != null)
            {
                characterClassText.text = isUnlocked ? character.characterClass : "Locked";
            }

            // Update description
            if (characterDescriptionText != null)
            {
                var preset = selector.GetCharacterPreset(character);
                characterDescriptionText.text = isUnlocked && preset != null ?
                    preset.classDescription : "Unlock this character to see their abilities.";
            }

            // Update stat bars
            var statPreset = selector.GetCharacterPreset(character);
            if (statPreset != null && isUnlocked)
            {
                UpdateStatBar(healthBar, statPreset.healthModifier);
                UpdateStatBar(damageBar, statPreset.damageModifier);
                UpdateStatBar(speedBar, statPreset.moveSpeedModifier);
                UpdateStatBar(attackSpeedBar, statPreset.attackSpeedModifier);
            }
            else
            {
                // Hide or grey out stat bars for locked characters
                UpdateStatBar(healthBar, 0f);
                UpdateStatBar(damageBar, 0f);
                UpdateStatBar(speedBar, 0f);
                UpdateStatBar(attackSpeedBar, 0f);
            }

            // Update select button
            if (selectButton != null)
            {
                selectButton.interactable = isUnlocked && !isSelected;
            }

            if (selectButtonText != null)
            {
                if (isSelected)
                {
                    selectButtonText.text = "Selected";
                }
                else if (isUnlocked)
                {
                    selectButtonText.text = "Select";
                }
                else
                {
                    selectButtonText.text = "Locked";
                }
            }

            // Update preview animator
            if (previewAnimator != null && isUnlocked)
            {
                previewAnimator.SetCharacterData(character);
                previewAnimator.PlayAnimation(previewAnimation, true);
            }

            // Update grid selection
            UpdateGridSelection();
        }

        private void UpdateStatBar(StatBar bar, float value)
        {
            if (bar == null) return;
            bar.SetValue(value);
        }

        private void UpdateGridSelection()
        {
            for (int i = 0; i < characterSlots.Count; i++)
            {
                if (characterSlots[i] != null)
                {
                    characterSlots[i].SetSelected(i == currentDisplayIndex);
                }
            }
        }

        private void OnDestroy()
        {
            if (prevButton != null) prevButton.onClick.RemoveListener(OnPrevClicked);
            if (nextButton != null) nextButton.onClick.RemoveListener(OnNextClicked);
            if (selectButton != null) selectButton.onClick.RemoveListener(OnSelectClicked);
        }
    }

    /// <summary>
    /// Simple stat bar UI element.
    /// </summary>
    [System.Serializable]
    public class StatBar
    {
        public Image fillImage;
        public TMP_Text valueText;
        public float minValue = 0.5f;
        public float maxValue = 2f;

        public void SetValue(float value)
        {
            float normalized = Mathf.InverseLerp(minValue, maxValue, value);

            if (fillImage != null)
            {
                fillImage.fillAmount = normalized;
            }

            if (valueText != null)
            {
                if (value <= 0)
                {
                    valueText.text = "???";
                }
                else
                {
                    valueText.text = $"{value:P0}";
                }
            }
        }
    }

    /// <summary>
    /// Individual character slot in the selection grid.
    /// </summary>
    public class CharacterSlotUI : MonoBehaviour
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image selectionBorder;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Button selectButton;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.yellow;
        [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        private bool isUnlocked;
        private bool isSelected;

        public void Setup(CharacterSpriteData character, bool unlocked, System.Action onClick)
        {
            isUnlocked = unlocked;

            if (portraitImage != null && character.portrait != null)
            {
                portraitImage.sprite = character.portrait;
                portraitImage.color = unlocked ? Color.white : lockedColor;
            }

            if (lockIcon != null)
            {
                lockIcon.gameObject.SetActive(!unlocked);
            }

            if (selectButton != null)
            {
                selectButton.interactable = unlocked;
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onClick?.Invoke());
            }

            UpdateVisuals();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? selectedColor : normalColor;
            }

            if (selectionBorder != null)
            {
                selectionBorder.gameObject.SetActive(isSelected);
            }
        }
    }
}
