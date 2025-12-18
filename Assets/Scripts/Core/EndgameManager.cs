using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using TMPro;

namespace PointClickDetective
{
    /// <summary>
    /// Manages final sequence and credits. Triggered by setting specific flags.
    /// </summary>
    public class EndgameManager : MonoBehaviour
    {
        public static EndgameManager Instance { get; private set; }
        
        /// <summary>
        /// Set this flag to trigger the final sequence.
        /// </summary>
        public const string TRIGGER_FINAL_SEQUENCE = "trigger_final_sequence";
        
        /// <summary>
        /// Set this flag to roll credits immediately.
        /// </summary>
        public const string TRIGGER_CREDITS = "trigger_credits";
        
        /// <summary>
        /// Set this flag to trigger final sequence, then credits.
        /// </summary>
        public const string TRIGGER_FINALE_AND_CREDITS = "trigger_finale_and_credits";
        
        [Header("Final Sequence")]
        [Tooltip("Dialogue sequence to play for the finale")]
        [SerializeField] private DialogueSequenceSO finalSequence;
        [Tooltip("Scene to load for the finale (optional)")]
        [SerializeField] private string finaleSceneId;
        
        [Header("Credits UI")]
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private TextMeshProUGUI creditsText;
        [SerializeField] private ScrollRect creditsScrollRect;
        [SerializeField] private float creditsScrollSpeed = 30f;
        [SerializeField] private float creditsEndDelay = 3f;
        
        [Header("Credits Content")]
        [TextArea(10, 30)]
        [SerializeField] private string creditsContent = @"A Game By
[Your Name]

Programming
[Your Name]

Art
[Your Name]

Music
[Your Name]

Special Thanks
[Names]

Thank you for playing!";
        
        [Header("Post-Credits")]
        [Tooltip("Scene to load after credits (leave empty to return to main menu)")]
        [SerializeField] private string postCreditsSceneId;
        [Tooltip("If true, loads Unity scene. If false, loads in-game scene via GameSceneManager.")]
        [SerializeField] private bool postCreditsIsUnityScene = true;
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        
        [Header("Audio")]
        [SerializeField] private AudioClip creditsMusic;
        [SerializeField] private bool fadeOutCurrentMusic = true;
        
        [Header("Events")]
        public UnityEvent OnFinalSequenceStarted;
        public UnityEvent OnFinalSequenceEnded;
        public UnityEvent OnCreditsStarted;
        public UnityEvent OnCreditsEnded;
        
        // State
        private bool isPlayingFinalSequence;
        private bool isShowingCredits;
        private bool creditsScrolling;
        private Coroutine creditsCoroutine;
        
        public bool IsPlayingFinalSequence => isPlayingFinalSequence;
        public bool IsShowingCredits => isShowingCredits;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            OnFinalSequenceStarted ??= new UnityEvent();
            OnFinalSequenceEnded ??= new UnityEvent();
            OnCreditsStarted ??= new UnityEvent();
            OnCreditsEnded ??= new UnityEvent();
            
            if (creditsPanel != null)
            {
                creditsPanel.SetActive(false);
            }
        }
        
        private void Start()
        {
            // Subscribe to flag changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnFlagSet.AddListener(OnFlagSet);
            }
        }
        
        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnFlagSet.RemoveListener(OnFlagSet);
            }
        }
        
        private void OnFlagSet(string flagName)
        {
            switch (flagName)
            {
                case TRIGGER_FINAL_SEQUENCE:
                    StartFinalSequence(rollCreditsAfter: false);
                    break;
                    
                case TRIGGER_CREDITS:
                    StartCredits();
                    break;
                    
                case TRIGGER_FINALE_AND_CREDITS:
                    StartFinalSequence(rollCreditsAfter: true);
                    break;
            }
        }
        
        #region Public Methods
        
        /// <summary>
        /// Start the final sequence manually.
        /// </summary>
        public void StartFinalSequence(bool rollCreditsAfter = false)
        {
            if (isPlayingFinalSequence) return;
            
            StartCoroutine(PlayFinalSequenceRoutine(rollCreditsAfter));
        }
        
        /// <summary>
        /// Start credits manually.
        /// </summary>
        public void StartCredits()
        {
            if (isShowingCredits) return;
            
            StartCoroutine(PlayCreditsRoutine());
        }
        
        /// <summary>
        /// Skip credits and go to post-credits scene.
        /// </summary>
        public void SkipCredits()
        {
            if (!isShowingCredits) return;
            
            if (creditsCoroutine != null)
            {
                StopCoroutine(creditsCoroutine);
            }
            
            EndCredits();
        }
        
        #endregion
        
        #region Routines
        
        private IEnumerator PlayFinalSequenceRoutine(bool rollCreditsAfter)
        {
            isPlayingFinalSequence = true;
            OnFinalSequenceStarted?.Invoke();
            
            // Lock the map during finale
            WorldMapUI.LockMap();
            
            // Load finale scene if specified
            if (!string.IsNullOrEmpty(finaleSceneId))
            {
                GameSceneManager.Instance?.LoadScene(finaleSceneId);
                yield return new WaitForSeconds(1f); // Wait for scene transition
            }
            
            // Play final dialogue sequence
            if (finalSequence != null && DialogueManager.Instance != null)
            {
                DialogueManager.Instance.ShowDialogueSequence(finalSequence);
                
                // Wait for dialogue to finish
                yield return new WaitUntil(() => !DialogueManager.Instance.IsShowing);
            }
            
            isPlayingFinalSequence = false;
            OnFinalSequenceEnded?.Invoke();
            
            // Clear the trigger flag
            GameManager.Instance?.RemoveFlag(TRIGGER_FINAL_SEQUENCE);
            GameManager.Instance?.RemoveFlag(TRIGGER_FINALE_AND_CREDITS);
            
            if (rollCreditsAfter)
            {
                yield return new WaitForSeconds(1f);
                StartCredits();
            }
        }
        
        private IEnumerator PlayCreditsRoutine()
        {
            isShowingCredits = true;
            OnCreditsStarted?.Invoke();
            
            // Setup credits text
            if (creditsText != null)
            {
                creditsText.text = creditsContent;
            }
            
            // Fade out current music and play credits music
            if (fadeOutCurrentMusic && MusicManager.Instance != null)
            {
                MusicManager.Instance.StopMusic(1f);
            }
            
            yield return new WaitForSeconds(1f);
            
            if (creditsMusic != null && MusicManager.Instance != null)
            {
                // Play credits music
                MusicManager.Instance.PlayOneShotTrack(creditsMusic, 1f);
            }
            
            // Show credits panel
            if (creditsPanel != null)
            {
                creditsPanel.SetActive(true);
            }
            
            // Scroll credits
            if (creditsScrollRect != null)
            {
                creditsScrolling = true;
                creditsScrollRect.verticalNormalizedPosition = 1f; // Start at top
                
                while (creditsScrollRect.verticalNormalizedPosition > 0f)
                {
                    creditsScrollRect.verticalNormalizedPosition -= 
                        (creditsScrollSpeed / creditsScrollRect.content.rect.height) * Time.deltaTime;
                    
                    // Allow skip with any key
                    if (UnityEngine.InputSystem.Keyboard.current != null && 
                        UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame)
                    {
                        // Skip to end
                        creditsScrollRect.verticalNormalizedPosition = 0f;
                        break;
                    }
                    
                    yield return null;
                }
                
                creditsScrolling = false;
            }
            
            // Wait at end
            yield return new WaitForSeconds(creditsEndDelay);
            
            EndCredits();
        }
        
        private void EndCredits()
        {
            isShowingCredits = false;
            
            if (creditsPanel != null)
            {
                creditsPanel.SetActive(false);
            }
            
            // Clear the trigger flag
            GameManager.Instance?.RemoveFlag(TRIGGER_CREDITS);
            
            OnCreditsEnded?.Invoke();
            
            // Go to post-credits destination
            if (!string.IsNullOrEmpty(postCreditsSceneId))
            {
                if (postCreditsIsUnityScene)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(postCreditsSceneId);
                }
                else
                {
                    GameSceneManager.Instance?.LoadScene(postCreditsSceneId);
                }
            }
            else if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
            }
        }
        
        #endregion
        
        private void Update()
        {
            // Allow Escape to skip credits
            if (isShowingCredits && UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    SkipCredits();
                }
            }
        }
    }
}