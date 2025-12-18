using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;

namespace PointClickDetective
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }
        
        [Header("UI References")]
        [SerializeField] private GameObject dialoguePanel;
        
        [Tooltip("The UI Image component where character portraits will be displayed.")]
        [SerializeField] private Image portraitDisplay;
        
        [Tooltip("TextMeshPro for dialogue text. Should be a child of the chatbox.")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        
        [Header("Animated Chatbox (Sprite Sheet)")]
        [Tooltip("SpriteSheetAnimator for the animated chatbox. Replaces VideoPlayer for WebGL compatibility.")]
        [SerializeField] private SpriteSheetAnimator chatboxAnimator;
        
        [Tooltip("RawImage that displays the sprite sheet animation.")]
        [SerializeField] private RawImage chatboxDisplay;
        
        [Header("Auto-Scaling")]
        [Tooltip("RectTransform of the chatbox to auto-scale.")]
        [SerializeField] private RectTransform chatboxRectTransform;
        
        [Tooltip("Minimum height of the chatbox.")]
        [SerializeField] private float minChatboxHeight = 150f;
        
        [Tooltip("Maximum height of the chatbox.")]
        [SerializeField] private float maxChatboxHeight = 400f;
        
        [Tooltip("Padding to add to calculated text height.")]
        [SerializeField] private float chatboxPadding = 40f;
        
        [Header("Typewriter Settings")]
        [SerializeField] private bool useTypewriter = true;
        [SerializeField] private AudioSource typingAudioSource;
        
        [Header("Fallback Typewriter (used when DialogueLine has no Speaker)")]
        [SerializeField] private float fallbackCharsPerSecond = 30f;
        [SerializeField] private AudioClip[] fallbackTypingSounds;
        [SerializeField] private float fallbackSoundInterval = 0.05f;
        
        [Header("Panel Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.2f;
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        [Header("Portrait Settings")]
        [SerializeField] private bool animatePortrait = true;
        [SerializeField] private float portraitBobAmount = 5f;
        [SerializeField] private float portraitBobSpeed = 2f;
        
        [Header("Input")]
        [SerializeField] private Key skipKey = Key.Space;
        [SerializeField] private Key advanceKey = Key.Enter;
        [SerializeField] private bool clickToAdvance = true;
        
        [Header("Events")]
        public UnityEvent OnDialogueStarted;
        public UnityEvent OnDialogueEnded;
        public UnityEvent OnLineStarted;
        public UnityEvent OnLineFinished;
        public UnityEvent<ClueSO> OnClueDiscovered;
        
        // State
        private bool isShowing;
        private bool isTyping;
        private string currentFullText;
        private DialogueLine currentLine;
        private Coroutine typewriterCoroutine;
        private Coroutine panelFadeCoroutine;
        private Coroutine animationFinishCoroutine;
        private CanvasGroup panelCanvasGroup;
        private Vector3 originalPortraitPosition;
        private float originalChatboxHeight;
        
        // Current sequence being played
        private DialogueSequenceSO currentSequence;
        private int currentLineIndex;
        
        // Legacy support for simple dialogue calls
        private Sprite legacyPortraitOverride;
        
        // Legacy queue for simple dialogue
        private Queue<DialogueLine> dialogueQueue = new Queue<DialogueLine>();
        
        public bool IsShowing => isShowing;
        public bool IsTyping => isTyping;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            // Setup canvas group
            if (dialoguePanel != null)
            {
                panelCanvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
                if (panelCanvasGroup == null)
                {
                    panelCanvasGroup = dialoguePanel.AddComponent<CanvasGroup>();
                }
                
                dialoguePanel.SetActive(false);
            }
            
            if (portraitDisplay != null)
            {
                originalPortraitPosition = portraitDisplay.rectTransform.localPosition;
            }
            
            // Store original chatbox height
            if (chatboxRectTransform != null)
            {
                originalChatboxHeight = chatboxRectTransform.sizeDelta.y;
            }
            
            // Setup sprite sheet animator
            SetupAnimator();
            
            // Ensure typing audio source doesn't play on awake
            if (typingAudioSource != null)
            {
                typingAudioSource.playOnAwake = false;
                typingAudioSource.Stop();
            }
            
            OnDialogueStarted ??= new UnityEvent();
            OnDialogueEnded ??= new UnityEvent();
            OnLineStarted ??= new UnityEvent();
            OnLineFinished ??= new UnityEvent();
            OnClueDiscovered ??= new UnityEvent<ClueSO>();
        }
        
        private void SetupAnimator()
        {
            if (chatboxAnimator == null) return;
            
            // Subscribe to loop complete event
            chatboxAnimator.OnLoopComplete.AddListener(OnAnimationLoopComplete);
            
            // Prepare the animation
            chatboxAnimator.Prepare();
        }
        
        private void OnDestroy()
        {
            if (chatboxAnimator != null)
            {
                chatboxAnimator.OnLoopComplete.RemoveListener(OnAnimationLoopComplete);
            }
        }
        
        private void Update()
        {
            if (!isShowing) return;
            
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            
            // Handle input
            bool skipPressed = keyboard != null && keyboard[skipKey].wasPressedThisFrame;
            bool advancePressed = (keyboard != null && keyboard[advanceKey].wasPressedThisFrame) || 
                                  (clickToAdvance && mouse != null && mouse.leftButton.wasPressedThisFrame);
            
            if (isTyping)
            {
                // Skip to end of current text (click/key reveals all text instantly)
                if (skipPressed || advancePressed)
                {
                    FinishTyping();
                }
            }
            else
            {
                // Advance to next dialogue or close
                if (advancePressed)
                {
                    RequestAdvance();
                }
            }
            
            // Animate portrait while typing
            if (animatePortrait && portraitDisplay != null && portraitDisplay.gameObject.activeSelf && isTyping)
            {
                float bobOffset = Mathf.Sin(Time.time * portraitBobSpeed) * portraitBobAmount;
                portraitDisplay.rectTransform.localPosition = originalPortraitPosition + new Vector3(0, bobOffset, 0);
            }
        }
        
        #region Public Methods
        
        /// <summary>
        /// Show a dialogue sequence from a ScriptableObject.
        /// </summary>
        public void ShowDialogueSequence(DialogueSequenceSO sequence)
        {
            if (sequence == null || sequence.lines.Count == 0) return;
            
            // Clear legacy override when using proper sequences
            legacyPortraitOverride = null;
            
            currentSequence = sequence;
            currentLineIndex = 0;
            dialogueQueue.Clear();
            
            // Queue all valid lines
            foreach (var line in sequence.lines)
            {
                if (line.ShouldShow())
                {
                    dialogueQueue.Enqueue(line);
                }
            }
            
            if (dialogueQueue.Count == 0) return;
            
            // Start first line
            if (!isShowing)
            {
                StartNextLine();
            }
        }
        
        /// <summary>
        /// Show a single dialogue line (legacy support - uses fallback settings, no speaker).
        /// For proper dialogue, use DialogueSequenceSO with SpeakerSO assigned.
        /// </summary>
        public void ShowDialogue(string text, Sprite portrait = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            
            // Create a line without a speaker - will use fallback settings
            var line = new DialogueLine
            {
                text = text
            };
            
            // Store portrait for legacy support
            legacyPortraitOverride = portrait;
            
            currentSequence = null;
            dialogueQueue.Clear();
            dialogueQueue.Enqueue(line);
            
            if (!isShowing)
            {
                StartNextLine();
            }
        }
        
        /// <summary>
        /// Show multiple dialogue lines (legacy support - uses fallback settings, no speaker).
        /// For proper dialogue, use DialogueSequenceSO with SpeakerSO assigned.
        /// </summary>
        public void ShowDialogueSequence(params (string text, Sprite portrait)[] dialogues)
        {
            if (dialogues == null || dialogues.Length == 0) return;
            
            currentSequence = null;
            dialogueQueue.Clear();
            
            // For legacy tuple format, we can only use the first portrait as override
            if (dialogues.Length > 0)
            {
                legacyPortraitOverride = dialogues[0].portrait;
            }
            
            foreach (var (text, portrait) in dialogues)
            {
                var line = new DialogueLine
                {
                    text = text
                };
                dialogueQueue.Enqueue(line);
            }
            
            if (!isShowing)
            {
                StartNextLine();
            }
        }
        
        public void ForceClose()
        {
            dialogueQueue.Clear();
            currentSequence = null;
            CloseDialogue();
        }
        
        #endregion
        
        #region Internal Methods
        
        private void StartNextLine()
        {
            if (dialogueQueue.Count == 0)
            {
                // All lines done, complete sequence
                CompleteSequence();
                return;
            }
            
            var line = dialogueQueue.Dequeue();
            
            // Handle delay
            if (line.delayBefore > 0)
            {
                StartCoroutine(DelayedStartLine(line));
                return;
            }
            
            StartLine(line);
        }
        
        private IEnumerator DelayedStartLine(DialogueLine line)
        {
            yield return new WaitForSeconds(line.delayBefore);
            StartLine(line);
        }
        
        private void StartLine(DialogueLine line)
        {
            bool wasShowing = isShowing;
            isShowing = true;
            currentFullText = line.text;
            currentLine = line; // Store for scene change check on advance
            
            // Execute triggers for this line
            line.ExecuteTriggers();
            
            // Handle clue discovery notification
            if (line.discoverClueOnShow != null)
            {
                OnClueDiscovered?.Invoke(line.discoverClueOnShow);
            }
            
            // Set portrait
            SetupPortrait(line);
            
            // Auto-scale chatbox
            AutoScaleChatbox(line.text);
            
            // Show panel
            dialoguePanel.SetActive(true);
            
            // Cancel any previous animation finish coroutine
            if (animationFinishCoroutine != null)
            {
                StopCoroutine(animationFinishCoroutine);
                animationFinishCoroutine = null;
            }
            
            // Setup chatbox visuals
            if (!wasShowing)
            {
                // First line: start frozen at frame 0
                SetChatboxTypingMode(true, resetToFirstFrame: true);
            }
            else
            {
                // New line mid-dialogue: let current animation finish, then freeze
                animationFinishCoroutine = StartCoroutine(FinishAnimationThenFreeze());
            }
            
            // Fade in (only on first line)
            if (!wasShowing)
            {
                if (panelFadeCoroutine != null) StopCoroutine(panelFadeCoroutine);
                panelFadeCoroutine = StartCoroutine(FadeIn());
                OnDialogueStarted?.Invoke();
            }
            
            // Start typewriter or show full text
            if (useTypewriter)
            {
                if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = StartCoroutine(TypewriterEffect(line));
            }
            else
            {
                dialogueText.text = line.text;
                OnTypingComplete();
            }
            
            OnLineStarted?.Invoke();
        }
        
        /// <summary>
        /// Let the animation finish its current loop, then freeze at frame 0.
        /// </summary>
        private IEnumerator FinishAnimationThenFreeze()
        {
            if (chatboxAnimator == null) yield break;
            
            // Stop looping so it finishes current playback
            chatboxAnimator.IsLooping = false;
            
            // Wait for the animation to finish its current loop
            while (chatboxAnimator.IsPlaying)
            {
                yield return null;
            }
            
            // Now freeze at frame 0
            chatboxAnimator.SetFrame(0);
            chatboxAnimator.Pause();
        }
        
        private void SetupPortrait(DialogueLine line)
        {
            if (portraitDisplay == null) return;
            
            if (line.ShouldHidePortrait() && legacyPortraitOverride == null)
            {
                portraitDisplay.gameObject.SetActive(false);
            }
            else
            {
                // Priority: speaker portrait > legacy override > player character portrait
                Sprite portrait = line.GetPortrait();
                
                // If no speaker portrait, try legacy override
                if (portrait == null && legacyPortraitOverride != null)
                {
                    portrait = legacyPortraitOverride;
                }
                
                if (portrait != null)
                {
                    portraitDisplay.sprite = portrait;
                    portraitDisplay.gameObject.SetActive(true);
                }
                else
                {
                    // Fallback to current player character portrait
                    Sprite defaultPortrait = GameManager.Instance?.GetCurrentCharacterPortrait();
                    if (defaultPortrait != null)
                    {
                        portraitDisplay.sprite = defaultPortrait;
                        portraitDisplay.gameObject.SetActive(true);
                    }
                    else
                    {
                        portraitDisplay.gameObject.SetActive(false);
                    }
                }
            }
            
            // Reset portrait position
            portraitDisplay.rectTransform.localPosition = originalPortraitPosition;
        }
        
        private void AutoScaleChatbox(string text)
        {
            if (chatboxRectTransform == null || dialogueText == null) return;
            
            // Temporarily set full text to measure (strip delay tags for accurate sizing)
            string originalText = dialogueText.text;
            dialogueText.text = StripDelayTags(text);
            dialogueText.ForceMeshUpdate();
            
            // Get preferred height
            float preferredHeight = dialogueText.preferredHeight + chatboxPadding;
            float targetHeight = Mathf.Clamp(preferredHeight, minChatboxHeight, maxChatboxHeight);
            
            // Apply height
            Vector2 sizeDelta = chatboxRectTransform.sizeDelta;
            sizeDelta.y = targetHeight;
            chatboxRectTransform.sizeDelta = sizeDelta;
            
            // Reset text (typewriter will fill it)
            dialogueText.text = originalText;
        }
        
        private void SetChatboxTypingMode(bool typing, bool resetToFirstFrame = true)
        {
            // During typing: pause animation (optionally reset to first frame)
            // After typing: play animation
            
            Debug.Log($"[DialogueManager] SetChatboxTypingMode({typing}) - Animator null? {chatboxAnimator == null}");
            
            if (chatboxAnimator == null) return;
            
            if (typing)
            {
                // Prepare if needed
                if (!chatboxAnimator.IsPrepared)
                {
                    chatboxAnimator.Prepare();
                }
                
                // Only reset to frame 0 on first line of dialogue
                if (resetToFirstFrame)
                {
                    chatboxAnimator.SetFrame(0);
                }
                
                chatboxAnimator.Pause();
                Debug.Log("[DialogueManager] Chatbox PAUSED at frame 0");
                
                // Make sure display is visible
                if (chatboxDisplay != null)
                {
                    chatboxDisplay.gameObject.SetActive(true);
                }
            }
            else
            {
                // Re-enable looping and start playing animation
                chatboxAnimator.IsLooping = true;
                chatboxAnimator.Play();
                Debug.Log("[DialogueManager] Chatbox PLAYING");
            }
        }
        
        private void OnTypingComplete()
        {
            Debug.Log("[DialogueManager] OnTypingComplete called");
            // Switch from static to animated chatbox
            SetChatboxTypingMode(false);
            OnLineFinished?.Invoke();
        }
        
        private void RequestAdvance()
        {
            // If animation is playing, let it finish current loop but don't loop again
            if (chatboxAnimator != null && chatboxAnimator.IsPlaying)
            {
                chatboxAnimator.IsLooping = false; // Stop looping after current playback
            }
            
            // Advance immediately - don't wait for animation
            AdvanceDialogue();
        }
        
        private void OnAnimationLoopComplete()
        {
            // No longer used for blocking, but kept for potential future use
        }
        
        private void AdvanceDialogue()
        {
            // Check if current line triggers a scene change
            string lineSceneChange = null;
            if (currentLine != null && !string.IsNullOrEmpty(currentLine.triggerSceneChange))
            {
                lineSceneChange = currentLine.triggerSceneChange;
                Debug.Log($"[DialogueManager] Line triggers scene change to: {lineSceneChange}");
            }
            
            if (dialogueQueue.Count > 0)
            {
                StartNextLine();
            }
            else
            {
                CompleteSequence();
            }
            
            // Execute per-line scene change after dialogue advances
            if (!string.IsNullOrEmpty(lineSceneChange))
            {
                GameSceneManager.Instance?.LoadScene(lineSceneChange);
            }
        }
        
        private void CompleteSequence()
        {
            // Store scene change before clearing sequence
            string sceneToChangeTo = null;
            
            // Execute sequence completion triggers
            if (currentSequence != null)
            {
                if (!string.IsNullOrEmpty(currentSequence.setFlagOnComplete))
                {
                    Debug.Log($"[DialogueManager] Sequence complete - setting flag: {currentSequence.setFlagOnComplete}");
                    GameManager.Instance?.SetFlag(currentSequence.setFlagOnComplete);
                }
                
                if (currentSequence.discoverClueOnComplete != null)
                {
                    ClueManager.Instance?.DiscoverClue(currentSequence.discoverClueOnComplete);
                    OnClueDiscovered?.Invoke(currentSequence.discoverClueOnComplete);
                }
                
                if (currentSequence.revealQuestionOnComplete != null)
                {
                    DeductionBoardUI.Instance?.RevealQuestion(currentSequence.revealQuestionOnComplete);
                }
                
                // Store scene change to execute after closing
                if (!string.IsNullOrEmpty(currentSequence.triggerSceneChangeOnComplete))
                {
                    sceneToChangeTo = currentSequence.triggerSceneChangeOnComplete;
                    Debug.Log($"[DialogueManager] Sequence complete - will change scene to: {sceneToChangeTo}");
                }
            }
            
            currentSequence = null;
            CloseDialogue();
            
            // Execute scene change after dialogue is closed
            if (!string.IsNullOrEmpty(sceneToChangeTo))
            {
                GameSceneManager.Instance?.LoadScene(sceneToChangeTo);
            }
        }
        
        private void CloseDialogue()
        {
            currentLine = null;
            
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }
            
            if (animationFinishCoroutine != null)
            {
                StopCoroutine(animationFinishCoroutine);
                animationFinishCoroutine = null;
            }
            
            // Reset portrait position
            if (portraitDisplay != null)
            {
                portraitDisplay.rectTransform.localPosition = originalPortraitPosition;
            }
            
            // Reset chatbox height
            if (chatboxRectTransform != null)
            {
                Vector2 sizeDelta = chatboxRectTransform.sizeDelta;
                sizeDelta.y = originalChatboxHeight;
                chatboxRectTransform.sizeDelta = sizeDelta;
            }
            
            // Stop animation
            StopAnimationPlayback();
            
            // Fade out
            if (panelFadeCoroutine != null) StopCoroutine(panelFadeCoroutine);
            panelFadeCoroutine = StartCoroutine(FadeOut());
            
            OnDialogueEnded?.Invoke();
        }
        
        private void FinishTyping()
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }
            
            isTyping = false;
            dialogueText.text = StripDelayTags(currentFullText);
            
            // Reset portrait position
            if (portraitDisplay != null)
            {
                portraitDisplay.rectTransform.localPosition = originalPortraitPosition;
            }
            
            OnTypingComplete();
        }
        
        #endregion
        
        #region Animation Control
        
        private void StartAnimationPlayback()
        {
            if (chatboxAnimator == null) return;
            
            chatboxAnimator.Play();
        }
        
        private void StopAnimationPlayback()
        {
            if (chatboxAnimator == null) return;
            
            chatboxAnimator.Pause();
            chatboxAnimator.SetFrame(0);
        }
        
        #endregion
        
        #region Coroutines
        
        private IEnumerator TypewriterEffect(DialogueLine line)
        {
            string text = line.text;
            isTyping = true;
            dialogueText.text = "";
            
            // Get speaker for this line
            SpeakerSO speaker = line.speaker;
            
            // Track current style state
            bool isBold = false;
            bool isItalic = false;
            
            // Get initial settings
            float charsPerSecond, soundInterval, volume;
            AudioClip[] sounds;
            Vector2 pitchRange;
            
            if (speaker != null)
            {
                speaker.GetSettings(isBold, isItalic, out charsPerSecond, out sounds, out pitchRange, out volume);
                soundInterval = speaker.soundInterval;
            }
            else
            {
                charsPerSecond = fallbackCharsPerSecond;
                sounds = fallbackTypingSounds;
                soundInterval = fallbackSoundInterval;
                volume = 1f;
                pitchRange = new Vector2(0.95f, 1.05f);
            }
            
            // Ensure valid values
            if (charsPerSecond <= 0) charsPerSecond = fallbackCharsPerSecond;
            if (soundInterval <= 0) soundInterval = fallbackSoundInterval;
            
            float timeSinceLastSound = soundInterval; // Start ready to play
            bool hasSounds = sounds != null && sounds.Length > 0;
            
            int i = 0;
            while (i < text.Length)
            {
                // Check for rich text tags
                if (text[i] == '<')
                {
                    int tagEnd = text.IndexOf('>', i);
                    if (tagEnd != -1)
                    {
                        string tag = text.Substring(i, tagEnd - i + 1);
                        string tagLower = tag.ToLower();
                        
                        // Check for delay tag: <delay:500>
                        if (tagLower.StartsWith("<delay:"))
                        {
                            string delayStr = tag.Substring(7, tag.Length - 8); // Extract number
                            if (int.TryParse(delayStr, out int delayMs))
                            {
                                // Wait for the specified milliseconds
                                yield return new WaitForSeconds(delayMs / 1000f);
                            }
                            
                            // Skip the delay tag entirely (don't add it to displayed text)
                            i = tagEnd + 1;
                            continue;
                        }
                        
                        // Track style changes
                        bool styleChanged = false;
                        
                        if (tagLower == "<b>")
                        {
                            isBold = true;
                            styleChanged = true;
                        }
                        else if (tagLower == "</b>")
                        {
                            isBold = false;
                            styleChanged = true;
                        }
                        else if (tagLower == "<i>")
                        {
                            isItalic = true;
                            styleChanged = true;
                        }
                        else if (tagLower == "</i>")
                        {
                            isItalic = false;
                            styleChanged = true;
                        }
                        
                        // Update settings if style changed
                        if (styleChanged && speaker != null)
                        {
                            speaker.GetSettings(isBold, isItalic, out charsPerSecond, out sounds, out pitchRange, out volume);
                            hasSounds = sounds != null && sounds.Length > 0;
                        }
                        
                        // Include entire tag immediately (don't type it character by character)
                        // This handles: <b>, </b>, <i>, </i>, <color=#fff>, </color>, <size=20>, </size>, etc.
                        dialogueText.text = StripDelayTags(text.Substring(0, tagEnd + 1));
                        i = tagEnd + 1;
                        continue;
                    }
                }
                
                // Regular character
                dialogueText.text = StripDelayTags(text.Substring(0, i + 1));
                
                // Play typing sound (with interval to prevent spam)
                float charDelay = 1f / charsPerSecond;
                timeSinceLastSound += charDelay;
                
                if (typingAudioSource != null && hasSounds)
                {
                    if (timeSinceLastSound >= soundInterval)
                    {
                        // Don't play sound for spaces or punctuation
                        char currentChar = text[i];
                        if (!char.IsWhiteSpace(currentChar) && !char.IsPunctuation(currentChar))
                        {
                            AudioClip clip = sounds[Random.Range(0, sounds.Length)];
                            if (clip != null)
                            {
                                // Apply pitch variation
                                typingAudioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
                                typingAudioSource.PlayOneShot(clip, volume);
                                timeSinceLastSound = 0f;
                            }
                        }
                    }
                }
                
                yield return new WaitForSeconds(charDelay);
                i++;
            }
            
            // Reset pitch
            if (typingAudioSource != null)
            {
                typingAudioSource.pitch = 1f;
            }
            
            isTyping = false;
            OnTypingComplete();
        }
        
        // Cached regex for stripping delay tags
        private static readonly Regex DelayTagRegex = new Regex(@"<delay:\d+>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        /// <summary>
        /// Remove delay tags from text for display purposes.
        /// </summary>
        private string StripDelayTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return DelayTagRegex.Replace(text, "");
        }
        
        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            panelCanvasGroup.alpha = 0f;
            
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeInDuration;
                panelCanvasGroup.alpha = fadeInCurve.Evaluate(t);
                yield return null;
            }
            
            panelCanvasGroup.alpha = 1f;
        }
        
        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                panelCanvasGroup.alpha = fadeOutCurve.Evaluate(t);
                yield return null;
            }
            
            panelCanvasGroup.alpha = 0f;
            dialoguePanel.SetActive(false);
            isShowing = false;
        }
        
        #endregion
    }
}