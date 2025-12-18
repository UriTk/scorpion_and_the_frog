using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace PointClickDetective
{
    /// <summary>
    /// Simple world map UI - assign buttons and their target scene IDs.
    /// Clicking a button travels directly to that scene.
    /// </summary>
    public class WorldMapUI : MonoBehaviour
    {
        public static WorldMapUI Instance { get; private set; }
        
        [Header("Map Panel")]
        [SerializeField] private GameObject mapPanel;
        
        [Header("Location Buttons")]
        [Tooltip("Assign your location buttons and their target scenes")]
        [SerializeField] private List<MapLocationEntry> locations = new List<MapLocationEntry>();
        
        [Header("Input")]
        [SerializeField] private Key toggleKey = Key.M;
        
        [Header("Settings")]
        [SerializeField] private bool closeAfterTravel = true;
        
        [Header("Events")]
        public UnityEvent OnMapOpened;
        public UnityEvent OnMapClosed;
        public UnityEvent<string> OnTravelStarted;
        
        // State
        private bool isOpen;
        
        public bool IsOpen => isOpen;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            OnMapOpened ??= new UnityEvent();
            OnMapClosed ??= new UnityEvent();
            OnTravelStarted ??= new UnityEvent<string>();
        }
        
        private void Start()
        {
            // Hide panel initially (but keep this script's GameObject active!)
            if (mapPanel != null) mapPanel.SetActive(false);
            
            // Setup button listeners
            foreach (var location in locations)
            {
                if (location.button != null && !string.IsNullOrEmpty(location.targetSceneId))
                {
                    string sceneId = location.targetSceneId; // Capture for closure
                    location.button.onClick.AddListener(() => TravelTo(sceneId));
                }
            }
            
            Debug.Log("[WorldMapUI] Initialized. Press M to toggle map.");
        }
        
        private void Update()
        {
            // Toggle map with key
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                Debug.Log("[WorldMapUI] Toggle key pressed");
                ToggleMap();
            }
            
            // Close with Escape
            if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseMap();
            }
        }
        
        #region Public Methods
        
        public void OpenMap()
        {
            if (isOpen) return;
            
            // Don't open during scene transitions
            if (GameSceneManager.Instance?.IsTransitioning == true) return;
            
            // Don't open during dialogue
            if (DialogueManager.Instance?.IsShowing == true) return;
            
            // Close other UIs
            JournalUI.Instance?.CloseJournal();
            DeductionBoardUI.Instance?.CloseBoard();
            
            isOpen = true;
            mapPanel?.SetActive(true);
            
            RefreshButtonStates();
            
            OnMapOpened?.Invoke();
        }
        
        public void CloseMap()
        {
            if (!isOpen) return;
            
            isOpen = false;
            mapPanel?.SetActive(false);
            
            OnMapClosed?.Invoke();
        }
        
        public void ToggleMap()
        {
            if (isOpen) CloseMap();
            else OpenMap();
        }
        
        /// <summary>
        /// Travel directly to a scene by ID.
        /// </summary>
        public void TravelTo(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId)) return;
            
            // Don't travel to current scene
            var currentSceneId = GameSceneManager.Instance?.CurrentScene?.SceneId;
            if (sceneId == currentSceneId) return;
            
            // Check if locked
            var location = locations.Find(l => l.targetSceneId == sceneId);
            if (location != null && !string.IsNullOrEmpty(location.requiredFlag))
            {
                if (GameManager.Instance == null || !GameManager.Instance.HasFlag(location.requiredFlag))
                {
                    Debug.Log($"[WorldMapUI] Location locked: {sceneId} (requires flag: {location.requiredFlag})");
                    return;
                }
            }
            
            OnTravelStarted?.Invoke(sceneId);
            
            // Load the scene
            GameSceneManager.Instance?.LoadScene(sceneId);
            
            if (closeAfterTravel)
            {
                CloseMap();
            }
        }
        
        #endregion
        
        #region Internal Methods
        
        /// <summary>
        /// Disable button for current location or locked locations.
        /// </summary>
        private void RefreshButtonStates()
        {
            string currentSceneId = GameSceneManager.Instance?.CurrentScene?.SceneId;
            
            foreach (var location in locations)
            {
                if (location.button != null)
                {
                    // Check if this is the current scene
                    bool isCurrent = location.targetSceneId == currentSceneId;
                    
                    // Check if locked by flag
                    bool isLocked = false;
                    if (!string.IsNullOrEmpty(location.requiredFlag))
                    {
                        isLocked = GameManager.Instance == null || 
                                   !GameManager.Instance.HasFlag(location.requiredFlag);
                    }
                    
                    // Disable if current or locked
                    location.button.interactable = !isCurrent && !isLocked;
                }
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Simple pairing of a button to a scene ID with optional lock.
    /// </summary>
    [System.Serializable]
    public class MapLocationEntry
    {
        [Tooltip("The button to click")]
        public Button button;
        
        [Tooltip("The Scene ID to travel to (must match GameSceneContainer.sceneId)")]
        public string targetSceneId;
        
        [Tooltip("Optional: Flag required to unlock this location (leave empty for always unlocked)")]
        public string requiredFlag;
    }
}