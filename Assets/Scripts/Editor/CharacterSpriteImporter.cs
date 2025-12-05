using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelProject.Player;

namespace PixelProject.Editor
{
    /// <summary>
    /// Editor window for importing character sprites from the SmallScale Interactive
    /// Top-Down Pixel Characters asset pack format.
    /// </summary>
    public class CharacterSpriteImporter : EditorWindow
    {
        private string characterName = "Knight";
        private string assetRootPath = "Assets/Top-Down Pixel Characters 1";
        private CharacterSpriteData targetData;
        private Vector2 scrollPosition;
        private bool showAdvancedSettings;
        private float defaultFrameRate = 12f;

        // Import settings
        private bool importIdle = true;
        private bool importWalk = true;
        private bool importRun = true;
        private bool importMelee = true;
        private bool importDie = true;
        private bool importTakeDamage = true;
        private bool importAllAnimations = true;

        [MenuItem("Tools/Pixel Project/Character Sprite Importer")]
        public static void ShowWindow()
        {
            var window = GetWindow<CharacterSpriteImporter>("Character Importer");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Character Sprite Importer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool imports sprites from the SmallScale Interactive " +
                "Top-Down Pixel Characters asset pack into a CharacterSpriteData asset.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Asset path
            EditorGUILayout.LabelField("Asset Pack Location", EditorStyles.boldLabel);
            assetRootPath = EditorGUILayout.TextField("Root Path", assetRootPath);

            if (GUILayout.Button("Browse..."))
            {
                string path = EditorUtility.OpenFolderPanel("Select Asset Pack Root", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    assetRootPath = "Assets" + path.Replace(Application.dataPath, "");
                }
            }

            EditorGUILayout.Space(10);

            // Character selection
            EditorGUILayout.LabelField("Character Settings", EditorStyles.boldLabel);

            // Dropdown for available classes
            int currentIndex = System.Array.IndexOf(CharacterClassPresets.AvailableClasses, characterName);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup("Character Class", currentIndex, CharacterClassPresets.AvailableClasses);
            characterName = CharacterClassPresets.AvailableClasses[newIndex];

            defaultFrameRate = EditorGUILayout.FloatField("Default Frame Rate", defaultFrameRate);

            EditorGUILayout.Space(10);

            // Target asset
            EditorGUILayout.LabelField("Target Asset", EditorStyles.boldLabel);
            targetData = (CharacterSpriteData)EditorGUILayout.ObjectField(
                "Character Data", targetData, typeof(CharacterSpriteData), false);

            if (targetData == null)
            {
                EditorGUILayout.HelpBox(
                    "Select an existing CharacterSpriteData asset or create a new one.",
                    MessageType.Warning);

                if (GUILayout.Button("Create New Character Data"))
                {
                    CreateNewCharacterData();
                }
            }

            EditorGUILayout.Space(10);

            // Animation selection
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Animation Selection");
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;

                importAllAnimations = EditorGUILayout.Toggle("Import All Animations", importAllAnimations);

                EditorGUI.BeginDisabledGroup(importAllAnimations);
                importIdle = EditorGUILayout.Toggle("Idle", importIdle);
                importWalk = EditorGUILayout.Toggle("Walk", importWalk);
                importRun = EditorGUILayout.Toggle("Run", importRun);
                importMelee = EditorGUILayout.Toggle("Melee", importMelee);
                importDie = EditorGUILayout.Toggle("Die", importDie);
                importTakeDamage = EditorGUILayout.Toggle("Take Damage", importTakeDamage);
                EditorGUI.EndDisabledGroup();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(20);

            // Import button
            EditorGUI.BeginDisabledGroup(targetData == null);
            if (GUILayout.Button("Import Sprites", GUILayout.Height(40)))
            {
                ImportCharacterSprites();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            // Utility buttons
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

            if (GUILayout.Button("Scan Asset Pack Structure"))
            {
                ScanAssetPackStructure();
            }

            if (GUILayout.Button("Auto-Setup All Characters"))
            {
                AutoSetupAllCharacters();
            }

            EditorGUILayout.EndScrollView();
        }

        private void CreateNewCharacterData()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Character Data",
                characterName + "Data",
                "asset",
                "Create a new CharacterSpriteData asset");

            if (!string.IsNullOrEmpty(path))
            {
                targetData = ScriptableObject.CreateInstance<CharacterSpriteData>();
                targetData.characterName = characterName;
                targetData.characterClass = characterName;

                AssetDatabase.CreateAsset(targetData, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Created new CharacterSpriteData: {path}");
            }
        }

        private void ImportCharacterSprites()
        {
            if (targetData == null)
            {
                Debug.LogError("No target CharacterSpriteData selected!");
                return;
            }

            string characterPath = Path.Combine(assetRootPath, characterName);

            if (!AssetDatabase.IsValidFolder(characterPath))
            {
                Debug.LogError($"Character folder not found: {characterPath}");
                return;
            }

            var animationMapping = CharacterClassPresets.GetAnimationNameMapping();
            var directionMapping = CharacterClassPresets.GetDirectionNameMapping();
            int importedCount = 0;

            // Get all animation folders
            string[] animationFolders = AssetDatabase.GetSubFolders(characterPath);

            foreach (string animFolder in animationFolders)
            {
                string animName = Path.GetFileName(animFolder);

                if (!animationMapping.TryGetValue(animName, out AnimationState animState))
                {
                    Debug.LogWarning($"Unknown animation: {animName}, skipping.");
                    continue;
                }

                // Check if we should import this animation
                if (!ShouldImportAnimation(animState))
                {
                    continue;
                }

                CharacterAnimationData animData = new CharacterAnimationData(animState);

                // Get all direction folders within this animation
                string[] directionFolders = AssetDatabase.GetSubFolders(animFolder);

                foreach (string dirFolder in directionFolders)
                {
                    string dirName = Path.GetFileName(dirFolder);

                    if (!directionMapping.TryGetValue(dirName, out Direction8 direction))
                    {
                        Debug.LogWarning($"Unknown direction: {dirName} in {animName}, skipping.");
                        continue;
                    }

                    // Load all sprites in this direction folder
                    Sprite[] sprites = LoadSpritesFromFolder(dirFolder);

                    if (sprites.Length > 0)
                    {
                        animData.SetSpritesForDirection(direction, sprites);
                    }
                }

                // Also check for sprite sheets directly in the animation folder
                // Some asset packs use sprite sheets instead of individual sprites
                Sprite[] sheetSprites = LoadSpriteSheetsFromFolder(animFolder);
                if (sheetSprites.Length > 0 && !animData.DirectionalSprites.HasAnySprites())
                {
                    // Assume south-facing if no direction folders
                    animData.SetSpritesForDirection(Direction8.South, sheetSprites);
                }

                if (animData.DirectionalSprites.HasAnySprites())
                {
                    targetData.SetAnimation(animData);
                    importedCount++;
                }
            }

            // Update character info
            targetData.characterName = characterName;
            targetData.characterClass = characterName;

            EditorUtility.SetDirty(targetData);
            AssetDatabase.SaveAssets();

            Debug.Log($"Imported {importedCount} animations for {characterName}");
        }

        private bool ShouldImportAnimation(AnimationState state)
        {
            if (importAllAnimations) return true;

            return state switch
            {
                AnimationState.Idle or AnimationState.Idle2 => importIdle,
                AnimationState.Walk => importWalk,
                AnimationState.Run or AnimationState.RunBackwards => importRun,
                AnimationState.Melee or AnimationState.Melee2 or AnimationState.MeleeRun or AnimationState.MeleeSpin => importMelee,
                AnimationState.Die => importDie,
                AnimationState.TakeDamage => importTakeDamage,
                _ => true
            };
        }

        private Sprite[] LoadSpritesFromFolder(string folderPath)
        {
            List<Sprite> sprites = new List<Sprite>();

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Only include sprites directly in this folder, not subfolders
                if (Path.GetDirectoryName(path).Replace("\\", "/") == folderPath.Replace("\\", "/"))
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null)
                    {
                        sprites.Add(sprite);
                    }
                }
            }

            // Sort by name to ensure correct frame order
            sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            return sprites.ToArray();
        }

        private Sprite[] LoadSpriteSheetsFromFolder(string folderPath)
        {
            List<Sprite> allSprites = new List<Sprite>();

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Only include textures directly in this folder
                if (Path.GetDirectoryName(path).Replace("\\", "/") == folderPath.Replace("\\", "/"))
                {
                    // Load all sprites from the texture (for sprite sheets)
                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

                    foreach (Object asset in assets)
                    {
                        if (asset is Sprite sprite)
                        {
                            allSprites.Add(sprite);
                        }
                    }
                }
            }

            // Sort by name
            allSprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            return allSprites.ToArray();
        }

        private void ScanAssetPackStructure()
        {
            Debug.Log($"Scanning asset pack at: {assetRootPath}");

            if (!AssetDatabase.IsValidFolder(assetRootPath))
            {
                Debug.LogError($"Invalid path: {assetRootPath}");
                return;
            }

            string[] characterFolders = AssetDatabase.GetSubFolders(assetRootPath);

            Debug.Log($"Found {characterFolders.Length} character folders:");

            foreach (string charFolder in characterFolders)
            {
                string charName = Path.GetFileName(charFolder);
                string[] animFolders = AssetDatabase.GetSubFolders(charFolder);

                Debug.Log($"  - {charName} ({animFolders.Length} animations)");

                foreach (string animFolder in animFolders)
                {
                    string animName = Path.GetFileName(animFolder);
                    string[] dirFolders = AssetDatabase.GetSubFolders(animFolder);

                    if (dirFolders.Length > 0)
                    {
                        Debug.Log($"      - {animName} ({dirFolders.Length} directions)");
                    }
                    else
                    {
                        int spriteCount = AssetDatabase.FindAssets("t:Sprite", new[] { animFolder }).Length;
                        Debug.Log($"      - {animName} ({spriteCount} sprites)");
                    }
                }
            }
        }

        private void AutoSetupAllCharacters()
        {
            if (!AssetDatabase.IsValidFolder(assetRootPath))
            {
                Debug.LogError($"Invalid asset pack path: {assetRootPath}");
                return;
            }

            // Create output folder
            string outputPath = "Assets/ScriptableObjects/Characters";
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Characters");
            }

            string[] characterFolders = AssetDatabase.GetSubFolders(assetRootPath);
            int createdCount = 0;

            foreach (string charFolder in characterFolders)
            {
                string charName = Path.GetFileName(charFolder);

                // Create new CharacterSpriteData
                string assetPath = Path.Combine(outputPath, $"{charName}Data.asset");

                CharacterSpriteData data = ScriptableObject.CreateInstance<CharacterSpriteData>();
                data.characterName = charName;
                data.characterClass = charName;

                AssetDatabase.CreateAsset(data, assetPath);

                // Store current values and import
                var prevName = characterName;
                var prevTarget = targetData;

                characterName = charName;
                targetData = data;
                ImportCharacterSprites();

                characterName = prevName;
                targetData = prevTarget;

                createdCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created {createdCount} character data assets in {outputPath}");
        }
    }

    /// <summary>
    /// Custom inspector for CharacterSpriteData to show animation previews.
    /// </summary>
    [CustomEditor(typeof(CharacterSpriteData))]
    public class CharacterSpriteDataEditor : UnityEditor.Editor
    {
        private Direction8 previewDirection = Direction8.South;
        private AnimationState previewState = AnimationState.Idle;
        private int previewFrame = 0;
        private float lastFrameTime;
        private bool isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Animation Preview", EditorStyles.boldLabel);

            CharacterSpriteData data = (CharacterSpriteData)target;

            // Direction and state selection
            previewDirection = (Direction8)EditorGUILayout.EnumPopup("Preview Direction", previewDirection);
            previewState = (AnimationState)EditorGUILayout.EnumPopup("Preview State", previewState);

            EditorGUILayout.BeginHorizontal();
            isPlaying = GUILayout.Toggle(isPlaying, isPlaying ? "Stop" : "Play", "Button");
            if (GUILayout.Button("Reset"))
            {
                previewFrame = 0;
            }
            EditorGUILayout.EndHorizontal();

            // Get animation data
            var animData = data.GetAnimation(previewState);
            if (animData != null)
            {
                var sprites = animData.GetSpritesForDirection(previewDirection);
                if (sprites != null && sprites.Length > 0)
                {
                    // Update frame if playing
                    if (isPlaying)
                    {
                        float frameRate = animData.CustomFrameRate > 0 ? animData.CustomFrameRate : data.defaultFrameRate;
                        if (EditorApplication.timeSinceStartup - lastFrameTime > 1f / frameRate)
                        {
                            previewFrame = (previewFrame + 1) % sprites.Length;
                            lastFrameTime = (float)EditorApplication.timeSinceStartup;
                            Repaint();
                        }
                    }

                    previewFrame = Mathf.Clamp(previewFrame, 0, sprites.Length - 1);

                    // Draw preview
                    EditorGUILayout.LabelField($"Frame: {previewFrame + 1} / {sprites.Length}");
                    previewFrame = EditorGUILayout.IntSlider(previewFrame, 0, sprites.Length - 1);

                    Sprite sprite = sprites[previewFrame];
                    if (sprite != null)
                    {
                        Rect rect = GUILayoutUtility.GetRect(128, 128);
                        rect.x = (EditorGUIUtility.currentViewWidth - 128) / 2;
                        GUI.DrawTextureWithTexCoords(rect, sprite.texture, GetSpriteUVRect(sprite));
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"No sprites found for {previewDirection}", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"Animation '{previewState}' not found", MessageType.Warning);
            }

            if (isPlaying)
            {
                Repaint();
            }
        }

        private Rect GetSpriteUVRect(Sprite sprite)
        {
            Rect rect = sprite.textureRect;
            return new Rect(
                rect.x / sprite.texture.width,
                rect.y / sprite.texture.height,
                rect.width / sprite.texture.width,
                rect.height / sprite.texture.height
            );
        }
    }
}
