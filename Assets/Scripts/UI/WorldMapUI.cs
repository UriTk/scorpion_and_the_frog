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
        
        /// <summary>
        /// Flag name that globally locks/unlocks the map. Set flag = locked, clear flag = unlocked.
        /// </summary>
        public const string MAP_LOCKED_FLAG = "map_globally_locked";
        
        [Header("Map Panel")]
        [SerializeField] private GameObject mapPanel;
        
        [Header("Location Buttons")]
        [Tooltip("Assign your location buttons and their target scenes")]
        [SerializeField] private List<MapLocationEntry> locations = new List<MapLocationEntry>();
        
        [Header("Input")]
        [SerializeField] private Key toggleKey = Key.M;
        
        [Header("Settings")]
        [SerializeField] private bool closeAfterTravel = true;
        [Tooltip("If true, locked locations are completely hidden. If false, they're disabled but visible.")]
        [SerializeField] private bool hideLockedLocations = true;
        
        [Header("Events")]
        public UnityEvent OnMapOpened;
        public UnityEvent OnMapClosed;
        public UnityEvent<string> OnTravelStarted;
        public UnityEvent OnMapBlocked; // Fired when trying to open but map is globally locked
        
        // State
        private bool isOpen;
        
        public bool IsOpen => isOpen;
        
        /// <summary>
        /// Returns true if the map is globally locked via flag.
        /// </summary>
        public bool IsGloballyLocked => GameManager.Instance?.HasFlag(MAP_LOCKED_FLAG) ?? false;
        
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
            OnMapBlocked ??= new UnityEvent();
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
        
        /// <summary>
        /// Lock the map globally (prevents opening).
        /// </summary>
        public static void LockMap()
        {
            GameManager.Instance?.SetFlag(MAP_LOCKED_FLAG);
            
            // Close if currently open
            if (Instance != null && Instance.isOpen)
            {
                Instance.CloseMap();
            }
        }
        
        /// <summary>
        /// Unlock the map globally.
        /// </summary>
        public static void UnlockMap()
        {
            GameManager.Instance?.RemoveFlag(MAP_LOCKED_FLAG);
        }
        
        /// <summary>
        /// Toggle the global map lock.
        /// </summary>
        public static void ToggleMapLock()
        {
            if (GameManager.Instance?.HasFlag(MAP_LOCKED_FLAG) ?? false)
            {
                UnlockMap();
            }
            else
            {
                LockMap();
            }
        }
        
        public void OpenMap()
        {
            if (isOpen) return;
            
            // Check global lock
            if (IsGloballyLocked)
            {
                Debug.Log("[WorldMapUI] Map is globally locked");
                OnMapBlocked?.Invoke();
                return;
            }
            
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
            
            // Check global lock
            if (IsGloballyLocked) return;
            
            // Don't travel to current scene
            var currentSceneId = GameSceneManager.Instance?.CurrentScene?.SceneId;
            if (sceneId == currentSceneId) return;
            
            // Check if locked
            var location = locations.Find(l => l.targetSceneId == sceneId);
            if (location != null && !IsLocationUnlocked(location))
            {
                Debug.Log($"[WorldMapUI] Location locked: {sceneId}");
                return;
            }
            
            OnTravelStarted?.Invoke(sceneId);
            
            // Load the scene
            GameSceneManager.Instance?.LoadScene(sceneId);
            
            if (closeAfterTravel)
            {
                CloseMap();
            }
        }
        
        /// <summary>
        /// Force refresh the button states (call after unlocking a location).
        /// </summary>
        public void RefreshMap()
        {
            if (isOpen)
            {
                RefreshButtonStates();
            }
        }
        
        #endregion
        
        #region Internal Methods
        
        private bool IsLocationUnlocked(MapLocationEntry location)
        {
            if (string.IsNullOrEmpty(location.requiredFlag)) return true;
            return GameManager.Instance?.HasFlag(location.requiredFlag) ?? false;
        }
        
        /// <summary>
        /// Update button states - hide or disable locked locations.
        /// </summary>
        private void RefreshButtonStates()
        {
            string currentSceneId = GameSceneManager.Instance?.CurrentScene?.SceneId;
            
            foreach (var location in locations)
            {
                if (location.button == null) continue;
                
                // Check if this is the current scene
                bool isCurrent = location.targetSceneId == currentSceneId;
                
                // Check if locked by flag
                bool isUnlocked = IsLocationUnlocked(location);
                
                if (hideLockedLocations)
                {
                    // Completely hide locked locations
                    location.button.gameObject.SetActive(isUnlocked);
                    
                    // If visible, set interactable based on current scene
                    if (isUnlocked)
                    {
                        location.button.interactable = !isCurrent;
                    }
                }
                else
                {
                    // Show all, but disable locked or current
                    location.button.gameObject.SetActive(true);
                    location.button.interactable = !isCurrent && isUnlocked;
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