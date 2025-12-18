using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PointClickDetective
{
    public class GameSceneManager : MonoBehaviour
    {
        public static GameSceneManager Instance { get; private set; }
        
        [Header("Scene Containers")]
        [Tooltip("Drag all your GameSceneContainer objects here")]
        [SerializeField] private GameSceneContainer[] sceneContainers;
        
        [Header("Starting Scene")]
        [Tooltip("The scene ID to start in (must match a GameSceneContainer's Scene Id)")]
        [SerializeField] private string startingSceneId;
        
        [Header("Transition Settings")]
        [SerializeField] private float transitionDuration = 0.5f;
        [Tooltip("A full-screen Image used for fade transitions (optional)")]
        [SerializeField] private Image transitionOverlay;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Events")]
        public UnityEvent<string> OnSceneLoadStarted;
        public UnityEvent<string> OnSceneLoadComplete;
        
        // State
        private GameSceneContainer currentScene;
        private Dictionary<string, GameSceneContainer> sceneLookup;
        private Coroutine transitionCoroutine;
        private bool isTransitioning;
        
        public GameSceneContainer CurrentScene => currentScene;
        public bool IsTransitioning => isTransitioning;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            // Build lookup dictionary
            sceneLookup = new Dictionary<string, GameSceneContainer>();
            if (sceneContainers != null)
            {
                foreach (var container in sceneContainers)
                {
                    if (container != null && !string.IsNullOrEmpty(container.SceneId))
                    {
                        sceneLookup[container.SceneId] = container;
                        Debug.Log($"[GameSceneManager] Registered scene: {container.SceneId}");
                    }
                }
            }
            
            // Setup transition overlay
            if (transitionOverlay != null)
            {
                transitionOverlay.color = new Color(0, 0, 0, 0);
                transitionOverlay.raycastTarget = false;
            }
            
            OnSceneLoadStarted ??= new UnityEvent<string>();
            OnSceneLoadComplete ??= new UnityEvent<string>();
        }
        
        private void Start()
        {
            // Determine starting scene
            string initialSceneId = !string.IsNullOrEmpty(startingSceneId) 
                ? startingSceneId 
                : GameManager.Instance?.CurrentSceneId;
            
            // IMPORTANT: Update GameManager FIRST before activating scenes
            // This ensures Interactables see the correct scene ID
            if (GameManager.Instance != null && !string.IsNullOrEmpty(initialSceneId))
            {
                GameManager.Instance.SetSceneIdDirect(initialSceneId);
            }
            
            // Hide all scenes except the starting one
            foreach (var container in sceneContainers)
            {
                if (container == null) continue;
                
                bool isStartingScene = container.SceneId == initialSceneId;
                container.SetActive(isStartingScene);
                
                if (isStartingScene)
                {
                    currentScene = container;
                }
            }
            
            // Enter the starting scene
            if (currentScene != null)
            {
                currentScene.OnEnter();
                Debug.Log($"[GameSceneManager] Started in scene: {currentScene.SceneId}");
            }
            else
            {
                Debug.LogWarning($"[GameSceneManager] No starting scene found with ID: {initialSceneId}");
            }
        }
        
        #region Public Methods
        
        /// <summary>
        /// Load a scene by ID with a fade transition.
        /// </summary>
        public void LoadScene(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId))
            {
                Debug.LogWarning("[GameSceneManager] Cannot load scene with empty ID");
                return;
            }
            
            if (!sceneLookup.TryGetValue(sceneId, out GameSceneContainer newScene))
            {
                Debug.LogWarning($"[GameSceneManager] Scene not found: {sceneId}");
                return;
            }
            
            if (currentScene == newScene)
            {
                Debug.Log($"[GameSceneManager] Already in scene: {sceneId}");
                return;
            }
            
            // Check if scene is locked
            if (!newScene.IsUnlocked())
            {
                Debug.Log($"[GameSceneManager] Scene is locked: {sceneId}");
                return;
            }
            
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            
            transitionCoroutine = StartCoroutine(TransitionToScene(newScene));
        }
        
        /// <summary>
        /// Load a scene instantly without transition.
        /// </summary>
        public void LoadSceneInstant(string sceneId)
        {
            if (!sceneLookup.TryGetValue(sceneId, out GameSceneContainer newScene))
            {
                Debug.LogWarning($"[GameSceneManager] Scene not found: {sceneId}");
                return;
            }
            
            if (!newScene.IsUnlocked())
            {
                Debug.Log($"[GameSceneManager] Scene is locked: {sceneId}");
                return;
            }
            
            // Instant switch
            if (currentScene != null)
            {
                currentScene.OnExit();
                currentScene.SetActive(false);
            }
            
            currentScene = newScene;
            currentScene.SetActive(true);
            currentScene.OnEnter();
            
            // Update GameManager
            GameManager.Instance?.ChangeScene(sceneId);
            
            OnSceneLoadComplete?.Invoke(sceneId);
        }
        
        /// <summary>
        /// Get a scene container by ID.
        /// </summary>
        public GameSceneContainer GetScene(string sceneId)
        {
            return sceneLookup.TryGetValue(sceneId, out var scene) ? scene : null;
        }
        
        /// <summary>
        /// Get all registered scene IDs.
        /// </summary>
        public List<string> GetAllSceneIds()
        {
            return sceneLookup.Keys.ToList();
        }
        
        /// <summary>
        /// Get all scene containers.
        /// </summary>
        public List<GameSceneContainer> GetAllScenes()
        {
            return sceneContainers?.ToList() ?? new List<GameSceneContainer>();
        }
        
        /// <summary>
        /// Get only unlocked scenes.
        /// </summary>
        public List<GameSceneContainer> GetUnlockedScenes()
        {
            return sceneContainers?.Where(s => s != null && s.IsUnlocked()).ToList() 
                ?? new List<GameSceneContainer>();
        }
        
        #endregion
        
        #region Transitions
        
        private IEnumerator TransitionToScene(GameSceneContainer newScene)
        {
            isTransitioning = true;
            OnSceneLoadStarted?.Invoke(newScene.SceneId);
            
            // Fade to black
            if (transitionOverlay != null)
            {
                transitionOverlay.raycastTarget = true; // Block input during transition
                yield return StartCoroutine(FadeOverlay(0, 1, transitionDuration / 2));
            }
            
            // Exit current scene
            if (currentScene != null)
            {
                currentScene.OnExit();
                currentScene.SetActive(false);
            }
            
            // Update GameManager BEFORE entering new scene
            // This ensures Interactables see the correct scene ID
            GameManager.Instance?.SetSceneIdDirect(newScene.SceneId);
            
            // Enter new scene
            currentScene = newScene;
            currentScene.SetActive(true);
            
            // Wait a frame for Unity to fully update activeInHierarchy on all children
            yield return null;
            
            currentScene.OnEnter();
            
            // Small pause at black
            yield return new WaitForSeconds(0.1f);
            
            // Fade from black
            if (transitionOverlay != null)
            {
                yield return StartCoroutine(FadeOverlay(1, 0, transitionDuration / 2));
                transitionOverlay.raycastTarget = false;
            }
            
            isTransitioning = false;
            transitionCoroutine = null;
            
            OnSceneLoadComplete?.Invoke(newScene.SceneId);
            
            Debug.Log($"[GameSceneManager] Transitioned to: {newScene.SceneId}");
        }
        
        private IEnumerator FadeOverlay(float fromAlpha, float toAlpha, float duration)
        {
            float elapsed = 0f;
            Color color = transitionOverlay.color;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = fadeCurve.Evaluate(elapsed / duration);
                color.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                transitionOverlay.color = color;
                yield return null;
            }
            
            color.a = toAlpha;
            transitionOverlay.color = color;
        }
        
        #endregion
    }
}