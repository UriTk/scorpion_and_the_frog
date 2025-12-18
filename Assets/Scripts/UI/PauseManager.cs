using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PointClickDetective
{
    /// <summary>
    /// Handles pause functionality and options menu.
    /// </summary>
    public class PauseManager : MonoBehaviour
    {
        public static PauseManager Instance { get; private set; }
        
        [Header("Pause Menu Panel")]
        [SerializeField] private GameObject pauseMenuPanel;
        
        [Header("Options Panel")]
        [SerializeField] private GameObject optionsPanel;
        
        [Header("Buttons - Assign your custom buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;
        
        [Header("Options - Back Button")]
        [SerializeField] private Button optionsBackButton;
        
        [Header("Options - Volume Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        
        [Header("Options - Volume Labels (Optional)")]
        [SerializeField] private TMPro.TextMeshProUGUI masterVolumeLabel;
        [SerializeField] private TMPro.TextMeshProUGUI musicVolumeLabel;
        [SerializeField] private TMPro.TextMeshProUGUI sfxVolumeLabel;
        
        [Header("Input")]
        [SerializeField] private Key pauseKey = Key.Escape;
        
        [Header("Settings")]
        [Tooltip("Name of the main menu scene to load")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool pauseTimeWhenOpen = true;
        
        [Header("Events")]
        public UnityEvent OnPaused;
        public UnityEvent OnResumed;
        public UnityEvent OnOptionsOpened;
        public UnityEvent OnOptionsClosed;
        
        // State
        private bool isPaused;
        private bool isOptionsOpen;
        private float previousTimeScale;
        
        public bool IsPaused => isPaused;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            OnPaused ??= new UnityEvent();
            OnResumed ??= new UnityEvent();
            OnOptionsOpened ??= new UnityEvent();
            OnOptionsClosed ??= new UnityEvent();
        }
        
        private void Start()
        {
            // Setup buttons
            if (resumeButton != null)
                resumeButton.onClick.AddListener(Resume);
            
            if (optionsButton != null)
                optionsButton.onClick.AddListener(OpenOptions);
            
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(GoToMainMenu);
            
            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
            
            if (optionsBackButton != null)
                optionsBackButton.onClick.AddListener(CloseOptions);
            
            // Setup volume sliders
            SetupVolumeSliders();
            
            // Hide panels initially
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }
        
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[pauseKey].wasPressedThisFrame)
            {
                if (isOptionsOpen)
                {
                    CloseOptions();
                }
                else if (isPaused)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }
        }
        
        #region Public Methods
        
        public void Pause()
        {
            if (isPaused) return;
            
            // Don't pause during scene transitions
            if (GameSceneManager.Instance?.IsTransitioning == true) return;
            
            isPaused = true;
            
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(true);
            
            if (pauseTimeWhenOpen)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            
            OnPaused?.Invoke();
        }
        
        public void Resume()
        {
            if (!isPaused) return;
            
            isPaused = false;
            isOptionsOpen = false;
            
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);
            
            if (optionsPanel != null)
                optionsPanel.SetActive(false);
            
            if (pauseTimeWhenOpen)
            {
                Time.timeScale = previousTimeScale > 0 ? previousTimeScale : 1f;
            }
            
            OnResumed?.Invoke();
        }
        
        public void OpenOptions()
        {
            isOptionsOpen = true;
            
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);
            
            if (optionsPanel != null)
                optionsPanel.SetActive(true);
            
            // Refresh slider values
            RefreshVolumeSliders();
            
            OnOptionsOpened?.Invoke();
        }
        
        public void CloseOptions()
        {
            isOptionsOpen = false;
            
            if (optionsPanel != null)
                optionsPanel.SetActive(false);
            
            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(true);
            
            // Save settings
            SaveSettings();
            
            OnOptionsClosed?.Invoke();
        }
        
        public void GoToMainMenu()
        {
            // Resume time before loading
            Time.timeScale = 1f;
            isPaused = false;
            
            // Load main menu scene
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }
        
        public void QuitGame()
        {
            SaveSettings();
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        
        #endregion
        
        #region Volume Control
        
        private void SetupVolumeSliders()
        {
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.minValue = 0f;
                masterVolumeSlider.maxValue = 1f;
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }
            
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.minValue = 0f;
                musicVolumeSlider.maxValue = 1f;
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }
            
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.minValue = 0f;
                sfxVolumeSlider.maxValue = 1f;
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }
            
            // Load saved values
            LoadSettings();
        }
        
        private void RefreshVolumeSliders()
        {
            if (masterVolumeSlider != null && MusicManager.Instance != null)
                masterVolumeSlider.value = MusicManager.Instance.MasterVolume;
            
            if (musicVolumeSlider != null && MusicManager.Instance != null)
                musicVolumeSlider.value = MusicManager.Instance.MusicVolume;
            
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
            
            UpdateVolumeLabels();
        }
        
        private void OnMasterVolumeChanged(float value)
        {
            if (MusicManager.Instance != null)
                MusicManager.Instance.MasterVolume = value;
            
            AudioListener.volume = value;
            UpdateVolumeLabels();
        }
        
        private void OnMusicVolumeChanged(float value)
        {
            if (MusicManager.Instance != null)
                MusicManager.Instance.MusicVolume = value;
            
            UpdateVolumeLabels();
        }
        
        private void OnSFXVolumeChanged(float value)
        {
            // SFX volume is stored and can be used by your SFX system
            PlayerPrefs.SetFloat("SFXVolume", value);
            UpdateVolumeLabels();
        }
        
        private void UpdateVolumeLabels()
        {
            if (masterVolumeLabel != null && masterVolumeSlider != null)
                masterVolumeLabel.text = $"{Mathf.RoundToInt(masterVolumeSlider.value * 100)}%";
            
            if (musicVolumeLabel != null && musicVolumeSlider != null)
                musicVolumeLabel.text = $"{Mathf.RoundToInt(musicVolumeSlider.value * 100)}%";
            
            if (sfxVolumeLabel != null && sfxVolumeSlider != null)
                sfxVolumeLabel.text = $"{Mathf.RoundToInt(sfxVolumeSlider.value * 100)}%";
        }
        
        #endregion
        
        #region Settings Persistence
        
        private void SaveSettings()
        {
            if (MusicManager.Instance != null)
            {
                PlayerPrefs.SetFloat("MasterVolume", MusicManager.Instance.MasterVolume);
                PlayerPrefs.SetFloat("MusicVolume", MusicManager.Instance.MusicVolume);
            }
            PlayerPrefs.Save();
        }
        
        private void LoadSettings()
        {
            float masterVol = PlayerPrefs.GetFloat("MasterVolume", 1f);
            float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
            float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);
            
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.MasterVolume = masterVol;
                MusicManager.Instance.MusicVolume = musicVol;
            }
            
            AudioListener.volume = masterVol;
            
            // Update sliders
            if (masterVolumeSlider != null) masterVolumeSlider.value = masterVol;
            if (musicVolumeSlider != null) musicVolumeSlider.value = musicVol;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVol;
        }
        
        #endregion
    }
}
