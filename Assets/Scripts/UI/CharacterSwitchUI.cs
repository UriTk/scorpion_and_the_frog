using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;

namespace PointClickDetective
{
    public class CharacterSwitchUI : MonoBehaviour
    {
        [Header("Character Visuals")]
        [Tooltip("Icon shown when Scorpion is active (includes background)")]
        [SerializeField] private Sprite scorpionIcon;
        [Tooltip("Icon shown when Frog is active (includes background)")]
        [SerializeField] private Sprite frogIcon;
        
        [Header("Animation")]
        [SerializeField] private bool animateSwitch = true;
        [SerializeField] private float switchAnimationDuration = 0.3f;
        [SerializeField] private AnimationCurve switchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip switchToScorpionSFX;
        [SerializeField] private AudioClip switchToFrogSFX;
        
        [Header("Input")]
        [SerializeField] private Key switchKey = Key.Tab;
        
        [Header("Events")]
        public UnityEvent OnSwitchStarted;
        public UnityEvent OnSwitchComplete;
        
        // Auto-found from this GameObject
        private Button switchButton;
        
        // State
        private bool canSwitch = true;
        private Coroutine animationCoroutine;
        
        private void Awake()
        {
            OnSwitchStarted ??= new UnityEvent();
            OnSwitchComplete ??= new UnityEvent();
            
            // Auto-get button from this GameObject
            switchButton = GetComponent<Button>();
            
            if (switchButton == null)
            {
                Debug.LogError($"[CharacterSwitchUI] No Button component found on {gameObject.name}!");
            }
        }
        
        private void Start()
        {
            // Setup button click listener
            if (switchButton != null)
            {
                switchButton.onClick.AddListener(OnSwitchButtonClicked);
            }
            
            // Subscribe to character changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterChanged.AddListener(OnCharacterChanged);
                UpdateVisuals(GameManager.Instance.CurrentCharacter, false);
            }
            else
            {
                Debug.LogError("[CharacterSwitchUI] GameManager.Instance is null! Make sure GameManager exists in the scene.");
            }
        }
        
        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterChanged.RemoveListener(OnCharacterChanged);
            }
        }
        
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[switchKey].wasPressedThisFrame && canSwitch)
            {
                SwitchCharacter();
            }
        }
        
        #region Public Methods
        
        public void SwitchCharacter()
        {
            if (!canSwitch) return;
            
            // Don't switch during dialogue
            if (DialogueManager.Instance?.IsShowing == true) return;
            
            // Don't switch during scene transitions
            if (GameSceneManager.Instance?.IsTransitioning == true) return;
            
            // Close any open interaction popup
            InteractionManager.Instance?.ClosePopup();
            
            if (GameManager.Instance == null)
            {
                Debug.LogError("[CharacterSwitchUI] GameManager.Instance is null!");
                return;
            }
            
            OnSwitchStarted?.Invoke();
            GameManager.Instance.SwitchCharacter();
            StartCoroutine(SwitchCooldownCoroutine());
        }
        
        #endregion
        
        #region Internal Methods
        
        private void OnSwitchButtonClicked()
        {
            SwitchCharacter();
        }
        
        private void OnCharacterChanged(CharacterType newCharacter)
        {
            // Play switch SFX
            if (audioSource != null)
            {
                AudioClip clip = newCharacter == CharacterType.Scorpion ? switchToScorpionSFX : switchToFrogSFX;
                if (clip != null)
                {
                    audioSource.PlayOneShot(clip);
                }
            }
            
            UpdateVisuals(newCharacter, animateSwitch);
            OnSwitchComplete?.Invoke();
        }
        
        private void UpdateVisuals(CharacterType character, bool animate)
        {
            Sprite targetIcon = character == CharacterType.Scorpion ? scorpionIcon : frogIcon;
            
            if (animate)
            {
                if (animationCoroutine != null)
                {
                    StopCoroutine(animationCoroutine);
                }
                animationCoroutine = StartCoroutine(AnimateSwitch(targetIcon));
            }
            else
            {
                if (switchButton != null)
                {
                    switchButton.image.sprite = targetIcon;
                }
            }
        }
        
        private IEnumerator AnimateSwitch(Sprite newIcon)
        {
            Image buttonImage = switchButton.image;
            Vector3 startScale = switchButton.transform.localScale;
            
            float halfDuration = switchAnimationDuration / 2f;
            float elapsed = 0f;
            
            // First half: shrink and fade out
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = switchCurve.Evaluate(Mathf.Clamp01(elapsed / halfDuration));
                
                switchButton.transform.localScale = Vector3.Lerp(startScale, startScale * 0.8f, t);
                buttonImage.color = new Color(1f, 1f, 1f, 1f - t);
                
                yield return null;
            }
            
            // Midpoint: fully invisible, swap sprite
            buttonImage.color = new Color(1f, 1f, 1f, 0f);
            buttonImage.sprite = newIcon;
            
            // Second half: grow and fade in
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = switchCurve.Evaluate(Mathf.Clamp01(elapsed / halfDuration));
                
                switchButton.transform.localScale = Vector3.Lerp(startScale * 0.8f, startScale, t);
                buttonImage.color = new Color(1f, 1f, 1f, t);
                
                yield return null;
            }
            
            // Ensure final state is clean
            switchButton.transform.localScale = startScale;
            buttonImage.color = Color.white;
            
            animationCoroutine = null;
        }
        
        private IEnumerator SwitchCooldownCoroutine()
        {
            canSwitch = false;
            
            if (switchButton != null)
            {
                switchButton.interactable = false;
            }
            
            yield return new WaitForSeconds(switchAnimationDuration);
            
            canSwitch = true;
            
            if (switchButton != null)
            {
                switchButton.interactable = true;
            }
        }
        
        #endregion
    }
}