using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using PixelProject.Core;

namespace PixelProject.Audio
{
    /// <summary>
    /// Central audio manager handling music, sound effects, and audio settings.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private int sfxSourcePoolSize = 10;

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterVolumeParam = "MasterVolume";
        [SerializeField] private string musicVolumeParam = "MusicVolume";
        [SerializeField] private string sfxVolumeParam = "SFXVolume";

        [Header("Music Tracks")]
        [SerializeField] private AudioClip mainMenuMusic;
        [SerializeField] private AudioClip gameplayMusic;
        [SerializeField] private AudioClip bossMusic;
        [SerializeField] private AudioClip victoryMusic;
        [SerializeField] private AudioClip gameOverMusic;

        [Header("Common SFX")]
        [SerializeField] private AudioClip buttonClickSFX;
        [SerializeField] private AudioClip levelUpSFX;
        [SerializeField] private AudioClip pickupSFX;
        [SerializeField] private AudioClip playerHurtSFX;
        [SerializeField] private AudioClip playerDeathSFX;

        [Header("Settings")]
        [SerializeField] private float musicFadeDuration = 1f;
        [SerializeField] private float defaultPitch = 1f;

        private List<AudioSource> sfxSources = new List<AudioSource>();
        private int currentSfxSourceIndex = 0;

        private float musicVolume = 1f;
        private float sfxVolume = 1f;
        private float masterVolume = 1f;

        private Coroutine musicFadeCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            LoadVolumeSettings();
        }

        private void Start()
        {
            // Subscribe to game events
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
            }

            EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            EventBus.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<ItemCollectedEvent>(OnItemCollected);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }

            EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            EventBus.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<ItemCollectedEvent>(OnItemCollected);
        }

        private void InitializeAudioSources()
        {
            // Create music source if not assigned
            if (musicSource == null)
            {
                GameObject musicObj = new GameObject("MusicSource");
                musicObj.transform.SetParent(transform);
                musicSource = musicObj.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }

            // Create ambient source if not assigned
            if (ambientSource == null)
            {
                GameObject ambientObj = new GameObject("AmbientSource");
                ambientObj.transform.SetParent(transform);
                ambientSource = ambientObj.AddComponent<AudioSource>();
                ambientSource.loop = true;
                ambientSource.playOnAwake = false;
            }

            // Create SFX source pool
            for (int i = 0; i < sfxSourcePoolSize; i++)
            {
                GameObject sfxObj = new GameObject($"SFXSource_{i}");
                sfxObj.transform.SetParent(transform);
                AudioSource source = sfxObj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sfxSources.Add(source);
            }
        }

        private void OnGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.MainMenu:
                    PlayMusic(mainMenuMusic);
                    break;

                case GameState.Playing:
                    PlayMusic(gameplayMusic);
                    break;

                case GameState.GameOver:
                    PlayMusic(gameOverMusic, false);
                    break;

                case GameState.Victory:
                    PlayMusic(victoryMusic, false);
                    break;
            }
        }

        // Music Methods
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (clip == null) return;

            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            musicFadeCoroutine = StartCoroutine(FadeToNewMusic(clip, loop));
        }

        private System.Collections.IEnumerator FadeToNewMusic(AudioClip newClip, bool loop)
        {
            // Fade out current music
            if (musicSource.isPlaying)
            {
                float startVolume = musicSource.volume;
                float timer = 0f;

                while (timer < musicFadeDuration / 2f)
                {
                    timer += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(startVolume, 0f, timer / (musicFadeDuration / 2f));
                    yield return null;
                }

                musicSource.Stop();
            }

            // Set new clip and fade in
            musicSource.clip = newClip;
            musicSource.loop = loop;
            musicSource.Play();

            float fadeTimer = 0f;
            while (fadeTimer < musicFadeDuration / 2f)
            {
                fadeTimer += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(0f, musicVolume, fadeTimer / (musicFadeDuration / 2f));
                yield return null;
            }

            musicSource.volume = musicVolume;
        }

        public void StopMusic()
        {
            if (musicFadeCoroutine != null)
            {
                StopCoroutine(musicFadeCoroutine);
            }

            StartCoroutine(FadeOutMusic());
        }

        private System.Collections.IEnumerator FadeOutMusic()
        {
            float startVolume = musicSource.volume;
            float timer = 0f;

            while (timer < musicFadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, timer / musicFadeDuration);
                yield return null;
            }

            musicSource.Stop();
            musicSource.volume = musicVolume;
        }

        public void PlayBossMusic()
        {
            if (bossMusic != null)
            {
                PlayMusic(bossMusic);
            }
        }

        // SFX Methods
        public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f, float pitchVariation = 0f)
        {
            if (clip == null) return;

            AudioSource source = GetAvailableSfxSource();
            source.clip = clip;
            source.volume = sfxVolume * volumeMultiplier;
            source.pitch = defaultPitch + Random.Range(-pitchVariation, pitchVariation);
            source.Play();
        }

        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            AudioSource.PlayClipAtPoint(clip, position, sfxVolume * volumeMultiplier);
        }

        private AudioSource GetAvailableSfxSource()
        {
            // Find a source that's not playing
            for (int i = 0; i < sfxSources.Count; i++)
            {
                int index = (currentSfxSourceIndex + i) % sfxSources.Count;
                if (!sfxSources[index].isPlaying)
                {
                    currentSfxSourceIndex = (index + 1) % sfxSources.Count;
                    return sfxSources[index];
                }
            }

            // All sources are playing, use the oldest one
            currentSfxSourceIndex = (currentSfxSourceIndex + 1) % sfxSources.Count;
            return sfxSources[currentSfxSourceIndex];
        }

        // Ambient Methods
        public void PlayAmbient(AudioClip clip)
        {
            if (clip == null) return;

            ambientSource.clip = clip;
            ambientSource.Play();
        }

        public void StopAmbient()
        {
            ambientSource.Stop();
        }

        // Volume Control
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);

            if (audioMixer != null)
            {
                float dbVolume = volume > 0 ? Mathf.Log10(volume) * 20f : -80f;
                audioMixer.SetFloat(masterVolumeParam, dbVolume);
            }

            SaveVolumeSettings();
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            musicSource.volume = musicVolume;

            if (audioMixer != null)
            {
                float dbVolume = volume > 0 ? Mathf.Log10(volume) * 20f : -80f;
                audioMixer.SetFloat(musicVolumeParam, dbVolume);
            }

            SaveVolumeSettings();
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);

            if (audioMixer != null)
            {
                float dbVolume = volume > 0 ? Mathf.Log10(volume) * 20f : -80f;
                audioMixer.SetFloat(sfxVolumeParam, dbVolume);
            }

            SaveVolumeSettings();
        }

        public float GetMasterVolume() => masterVolume;
        public float GetMusicVolume() => musicVolume;
        public float GetSFXVolume() => sfxVolume;

        // Settings Persistence
        private void SaveVolumeSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            PlayerPrefs.SetFloat("MusicVolume", musicVolume);
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            PlayerPrefs.Save();
        }

        private void LoadVolumeSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

            SetMasterVolume(masterVolume);
            SetMusicVolume(musicVolume);
            SetSFXVolume(sfxVolume);
        }

        // Event Handlers
        private void OnPlayerDamaged(PlayerDamagedEvent evt)
        {
            PlaySFX(playerHurtSFX, 1f, 0.1f);
        }

        private void OnPlayerDeath(PlayerDeathEvent evt)
        {
            PlaySFX(playerDeathSFX);
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            PlaySFX(levelUpSFX);
        }

        private void OnItemCollected(ItemCollectedEvent evt)
        {
            PlaySFX(pickupSFX, 0.8f, 0.2f);
        }

        public void PlayButtonClick()
        {
            PlaySFX(buttonClickSFX, 0.5f);
        }
    }
}
