using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace PointClickDetective
{
    /// <summary>
    /// Shows a popup notification when a clue is discovered.
    /// Automatically appears and disappears, can queue multiple clues.
    /// </summary>
    public class CluePopupUI : MonoBehaviour
    {
        public static CluePopupUI Instance { get; private set; }
        
        [Header("Popup Panel")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private CanvasGroup popupCanvasGroup;
        
        [Header("Content")]
        [SerializeField] private Image clueIcon;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI clueNameText;
        [SerializeField] private TextMeshProUGUI clueDescriptionText;
        [SerializeField] private TextMeshProUGUI foundByText;
        
        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Slide Animation (Optional)")]
        [SerializeField] private bool useSlideAnimation = true;
        [SerializeField] private Vector2 slideStartOffset = new Vector2(300, 0);
        [SerializeField] private RectTransform popupRectTransform;
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip scorpionDiscoverySound;
        [SerializeField] private AudioClip frogDiscoverySound;
        [SerializeField] private AudioClip defaultDiscoverySound;
        
        [Header("Settings")]
        [SerializeField] private bool pauseGameDuringPopup = false;
        [SerializeField] private string headerTextFormat = "CLUE DISCOVERED!";
        
        // Queue for multiple clues
        private Queue<ClueSO> clueQueue = new Queue<ClueSO>();
        private bool isShowingPopup = false;
        private Vector2 originalPosition;
        private Coroutine popupCoroutine;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Store original position
            if (popupRectTransform != null)
            {
                originalPosition = popupRectTransform.anchoredPosition;
            }
            
            // Hide initially
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
            
            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = 0;
            }
        }
        
        private void Start()
        {
            // Subscribe to clue discovery
            if (ClueManager.Instance != null)
            {
                ClueManager.Instance.OnClueFound.AddListener(OnClueDiscovered);
            }
        }
        
        private void OnDestroy()
        {
            if (ClueManager.Instance != null)
            {
                ClueManager.Instance.OnClueFound.RemoveListener(OnClueDiscovered);
            }
        }
        
        /// <summary>
        /// Called when a clue is discovered.
        /// </summary>
        private void OnClueDiscovered(ClueSO clue)
        {
            if (clue == null) return;
            
            // Add to queue
            clueQueue.Enqueue(clue);
            
            // Start showing if not already
            if (!isShowingPopup)
            {
                ShowNextClue();
            }
        }
        
        /// <summary>
        /// Manually show a clue popup.
        /// </summary>
        public void ShowClue(ClueSO clue)
        {
            if (clue == null) return;
            
            clueQueue.Enqueue(clue);
            
            if (!isShowingPopup)
            {
                ShowNextClue();
            }
        }
        
        private void ShowNextClue()
        {
            if (clueQueue.Count == 0)
            {
                isShowingPopup = false;
                return;
            }
            
            isShowingPopup = true;
            var clue = clueQueue.Dequeue();
            
            if (popupCoroutine != null)
            {
                StopCoroutine(popupCoroutine);
            }
            
            popupCoroutine = StartCoroutine(ShowPopupCoroutine(clue));
        }
        
        private IEnumerator ShowPopupCoroutine(ClueSO clue)
        {
            // Setup content
            SetupPopupContent(clue);
            
            // Pause game if needed
            float originalTimeScale = Time.timeScale;
            if (pauseGameDuringPopup)
            {
                Time.timeScale = 0;
            }
            
            // Show panel
            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
            }
            
            // Play character-specific sound
            if (audioSource != null)
            {
                AudioClip soundToPlay = GetDiscoverySoundForClue(clue);
                if (soundToPlay != null)
                {
                    audioSource.PlayOneShot(soundToPlay);
                }
            }
            
            // Initial state for animation
            if (popupCanvasGroup != null)
            {
                popupCanvasGroup.alpha = 0;
            }
            
            if (useSlideAnimation && popupRectTransform != null)
            {
                popupRectTransform.anchoredPosition = originalPosition + slideStartOffset;
            }
            
            // Fade in + slide
            float elapsed = 0;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeCurve.Evaluate(elapsed / fadeInDuration);
                
                if (popupCanvasGroup != null)
                {
                    popupCanvasGroup.alpha = t;
                }
                
                if (useSlideAnimation && popupRectTransform != null)
                {
                    popupRectTransform.anchoredPosition = Vector2.Lerp(
                        originalPosition + slideStartOffset,
                        originalPosition,
                        t
                    );
                }
                
                yield return null;
            }
            
            // Ensure final state
            if (popupCanvasGroup != null) popupCanvasGroup.alpha = 1;
            if (useSlideAnimation && popupRectTransform != null)
            {
                popupRectTransform.anchoredPosition = originalPosition;
            }
            
            // Display duration
            yield return new WaitForSecondsRealtime(displayDuration);
            
            // Fade out
            elapsed = 0;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeCurve.Evaluate(elapsed / fadeOutDuration);
                
                if (popupCanvasGroup != null)
                {
                    popupCanvasGroup.alpha = 1 - t;
                }
                
                yield return null;
            }
            
            // Hide panel
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
            
            // Restore time scale
            if (pauseGameDuringPopup)
            {
                Time.timeScale = originalTimeScale;
            }
            
            // Show next clue if queued
            popupCoroutine = null;
            ShowNextClue();
        }
        
        private void SetupPopupContent(ClueSO clue)
        {
            if (headerText != null)
            {
                headerText.text = headerTextFormat;
            }
            
            if (clueNameText != null)
            {
                clueNameText.text = clue.clueName;
            }
            
            if (clueDescriptionText != null)
            {
                clueDescriptionText.text = clue.description;
            }
            
            if (clueIcon != null)
            {
                if (clue.icon != null)
                {
                    clueIcon.sprite = clue.icon;
                    clueIcon.gameObject.SetActive(true);
                }
                else
                {
                    clueIcon.gameObject.SetActive(false);
                }
            }
            
            if (foundByText != null)
            {
                string foundBy = clue.visibleTo switch
                {
                    ClueVisibility.ScorpionOnly => "Scorpion",
                    ClueVisibility.FrogOnly => "Frog",
                    _ => "Both"
                };
                foundByText.text = $"Found by: {foundBy}";
            }
        }
        
        /// <summary>
        /// Get the appropriate discovery sound based on who found the clue.
        /// </summary>
        private AudioClip GetDiscoverySoundForClue(ClueSO clue)
        {
            // Determine who found the clue based on current character and clue visibility
            CharacterType currentCharacter = GameManager.Instance?.CurrentCharacter ?? CharacterType.Scorpion;
            
            // If clue is character-specific, use that character's sound
            // Otherwise, use current character's sound
            CharacterType finder = clue.visibleTo switch
            {
                ClueVisibility.ScorpionOnly => CharacterType.Scorpion,
                ClueVisibility.FrogOnly => CharacterType.Frog,
                _ => currentCharacter // Both can find it, so whoever is active found it
            };
            
            // Return appropriate sound
            AudioClip characterSound = finder == CharacterType.Scorpion 
                ? scorpionDiscoverySound 
                : frogDiscoverySound;
            
            // Fall back to default if character sound not set
            return characterSound != null ? characterSound : defaultDiscoverySound;
        }
        
        /// <summary>
        /// Skip the current popup immediately.
        /// </summary>
        public void SkipCurrentPopup()
        {
            if (popupCoroutine != null)
            {
                StopCoroutine(popupCoroutine);
                popupCoroutine = null;
            }
            
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
            
            if (pauseGameDuringPopup)
            {
                Time.timeScale = 1;
            }
            
            ShowNextClue();
        }
    }
}