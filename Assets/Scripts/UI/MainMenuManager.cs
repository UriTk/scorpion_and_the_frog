using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace PointClickDetective
{
    /// <summary>
    /// Handles main menu functionality.
    /// Place this in your main menu scene.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        public static MainMenuManager Instance { get; private set; }
        
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private GameObject loadGamePanel;
        
        [Header("Main Menu Buttons - Assign your custom buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button loadGameButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;
        
        [Header("Sub-Panel Back Buttons")]
        [SerializeField] private Button optionsBackButton;
        [SerializeField] private Button creditsBackButton;
        [SerializeField] private Button loadGameBackButton;
        
        [Header("Options - Volume Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        
        [Header("Options - Volume Labels (Optional)")]
        [SerializeField] private TMPro.TextMeshProUGUI masterVolumeLabel;
        [SerializeField] private TMPro.TextMeshProUGUI musicVolumeLabel;
        [SerializeField] private TMPro.TextMeshProUGUI sfxVolumeLabel;
        
        [Header("Scene Settings")]
        [Tooltip("Name of the game scene to load for new game")]
        [SerializeField] private string gameSceneName = "Game";
        
        [Header("Save System")]
        [Tooltip("Key used to store save data in PlayerPrefs")]
        [SerializeField] private string saveKey = "GameSave";
        
        [Header("Events")]
        public UnityEvent OnNewGameStarted;
        public UnityEvent OnGameContinued;
        public UnityEvent OnOptionsOpened;
        public UnityEvent OnOptionsClosed;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            OnNewGameStarted ??= new UnityEvent();
            OnGameContinued ??= new UnityEvent();
            OnOptionsOpened ??= new UnityEvent();
            OnOptionsClosed ??= new UnityEvent();
        }
        
        private void Start()
        {
            // Ensure time is running (in case we came from paused game)
            Time.timeScale = 1f;
            
            // Setup main menu buttons
            if (newGameButton != null)
                newGameButton.onClick.AddListener(StartNewGame);
            
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(ContinueGame);
                // Disable if no save exists
                continueButton.interactable = HasSaveData();
            }
            
            if (loadGameButton != null)
            {
                loadGameButton.onClick.AddListener(OpenLoadGame);
                loadGameButton.interactable = HasSaveData();
            }
            
            if (optionsButton != null)
                optionsButton.onClick.AddListener(OpenOptions);
            
            if (creditsButton != null)
                creditsButton.onClick.AddListener(OpenCredits);
            
            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);
            
            // Setup back buttons
            if (optionsBackButton != null)
                optionsBackButton.onClick.AddListener(CloseOptions);
            
            if (creditsBackButton != null)
                creditsBackButton.onClick.AddListener(CloseCredits);
            
            if (loadGameBackButton != null)
                loadGameBackButton.onClick.AddListener(CloseLoadGame);
            
            // Setup volume sliders
            SetupVolumeSliders();
            
            // Show main menu, hide others
            ShowMainMenu();
        }
        
        #region Navigation
        
        private void ShowMainMenu()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (loadGamePanel != null) loadGamePanel.SetActive(false);
        }
        
        public void OpenOptions()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(true);
            
            RefreshVolumeSliders();
            OnOptionsOpened?.Invoke();
        }
        
        public void CloseOptions()
        {
            SaveSettings();
            ShowMainMenu();
            OnOptionsClosed?.Invoke();
        }
        
        public void OpenCredits()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(true);
        }
        
        public void CloseCredits()
        {
            ShowMainMenu();
        }
        
        public void OpenLoadGame()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (loadGamePanel != null) loadGamePanel.SetActive(true);
        }
        
        public void CloseLoadGame()
        {
            ShowMainMenu();
        }
        
        #endregion
        
        #region Game Management
        
        public void StartNewGame()
        {
            // Clear any existing save (optional - you might want a confirmation dialog)
            // PlayerPrefs.DeleteKey(saveKey);
            
            OnNewGameStarted?.Invoke();
            
            if (!string.IsNullOrEmpty(gameSceneName))
            {
                SceneManager.LoadScene(gameSceneName);
            }
        }
        
        public void ContinueGame()
        {
            if (!HasSaveData()) return;
            
            OnGameContinued?.Invoke();
            
            // Load the game scene - GameManager will handle loading save data
            if (!string.IsNullOrEmpty(gameSceneName))
            {
                // Store that we want to load save data
                PlayerPrefs.SetInt("LoadSaveOnStart", 1);
                SceneManager.LoadScene(gameSceneName);
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
        
        #region Save System
        
        public bool HasSaveData()
        {
            return PlayerPrefs.HasKey(saveKey);
        }
        
        public void DeleteSaveData()
        {
            PlayerPrefs.DeleteKey(saveKey);
            
            // Update button states
            if (continueButton != null)
                continueButton.interactable = false;
            
            if (loadGameButton != null)
                loadGameButton.interactable = false;
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
