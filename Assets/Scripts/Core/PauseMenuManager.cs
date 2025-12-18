using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PointClickDetective
{
    /// <summary>
    /// In-game pause menu with options.
    /// </summary>
    public class PauseMenuManager : MonoBehaviour
    {
        public static PauseMenuManager Instance { get; private set; }
        
        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject optionsPanel;
        
        [Header("Pause Menu Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;
        
        [Header("Options Back Button")]
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
        
        [Header("Scene Settings")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        
        [Header("Events")]
        public UnityEvent OnPaused;
        public UnityEvent OnResumed;
        
        private bool isPaused;
        private bool inOptionsPanel;
        
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
        }
        
        private void Start()
        {
            // Setup pause menu buttons
            if (resumeButton != null)
                resumeButton.onClick.AddListener(Resume);
            
            if (optionsButton != null)
                optionsButton.onClick.AddListener(OpenOptions);
            
            if (saveButton != null)
                saveButton.onClick.AddListener(SaveGame);
            
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            
            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
            
            if (optionsBackButton != null)
                optionsBackButton.onClick.AddListener(CloseOptions);
            
            // Setup volume sliders
            SetupVolumeSliders();
            
            // Hide panels initially
            if (pausePanel != null) pausePanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }
        
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[pauseKey].wasPressedThisFrame)
            {
                if (isPaused)
                {
                    if (inOptionsPanel)
                        CloseOptions();
                    else
                        Resume();
                }
                else
                {
                    Pause();
                }
            }
        }
        
        #region Pause Control
        
        public void Pause()
        {
            if (isPaused) return;
            
            // Don't pause during dialogue
            if (DialogueManager.Instance?.IsShowing == true) return;
            
            // Close other UIs
            JournalUI.Instance?.CloseJournal();
            DeductionBoardUI.Instance?.CloseBoard();
            WorldMapUI.Instance?.CloseMap();
            
            isPaused = true;
            Time.timeScale = 0f;
            
            if (pausePanel != null) pausePanel.SetActive(true);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            inOptionsPanel = false;
            
            OnPaused?.Invoke();
        }
        
        public void Resume()
        {
            if (!isPaused) return;
            
            isPaused = false;
            Time.timeScale = 1f;
            
            if (pausePanel != null) pausePanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            inOptionsPanel = false;
            
            SaveSettings();
            OnResumed?.Invoke();
        }
        
        #endregion
        
        #region Options
        
        public void OpenOptions()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(true);
            inOptionsPanel = true;
            
            RefreshVolumeSliders();
        }
        
        public void CloseOptions()
        {
            if (pausePanel != null) pausePanel.SetActive(true);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            inOptionsPanel = false;
            
            SaveSettings();
        }
        
        #endregion
        
        #region Game Actions
        
        public void SaveGame()
        {
            // Get save data from GameManager and store in PlayerPrefs
            if (GameManager.Instance != null)
            {
                string saveData = GameManager.Instance.GetSaveData();
                PlayerPrefs.SetString("GameSave", saveData);
                PlayerPrefs.Save();
                Debug.Log("[PauseMenu] Game saved");
            }
        }
        
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            isPaused = false;
            
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
            
            LoadSettings();
        }
        
        private void RefreshVolumeSliders()
        {
            float masterVol = PlayerPrefs.GetFloat("MasterVolume", 1f);
            float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
            float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);
            
            if (masterVolumeSlider != null) masterVolumeSlider.value = masterVol;
            if (musicVolumeSlider != null) musicVolumeSlider.value = musicVol;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVol;
            
            UpdateVolumeLabels();
        }
        
        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat("MasterVolume", value);
            UpdateVolumeLabels();
        }
        
        private void OnMusicVolumeChanged(float value)
        {
            PlayerPrefs.SetFloat("MusicVolume", value);
            
            // Update MusicManager if available
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.MusicVolume = value;
            }
            
            UpdateVolumeLabels();
        }
        
        private void OnSFXVolumeChanged(float value)
        {
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
        
        private void SaveSettings()
        {
            PlayerPrefs.Save();
        }
        
        private void LoadSettings()
        {
            float masterVol = PlayerPrefs.GetFloat("MasterVolume", 1f);
            float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
            float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);
            
            AudioListener.volume = masterVol;
            
            if (masterVolumeSlider != null) masterVolumeSlider.value = masterVol;
            if (musicVolumeSlider != null) musicVolumeSlider.value = musicVol;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVol;
        }
        
        #endregion
    }
}