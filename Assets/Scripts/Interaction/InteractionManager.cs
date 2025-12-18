using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

namespace PointClickDetective
{
    public class InteractionManager : MonoBehaviour
    {
        public static InteractionManager Instance { get; private set; }
        
        [Header("Interaction Popup UI")]
        [SerializeField] private GameObject interactionPopup;
        [SerializeField] private Button lookButton;
        [SerializeField] private Button interactButton;
        [SerializeField] private RectTransform popupRectTransform;
        
        [Header("Button Icons")]
        [SerializeField] private Sprite eyeIcon;
        [SerializeField] private Sprite handIcon;
        
        [Header("Popup Settings")]
        [SerializeField] private Vector2 popupOffset = new Vector2(50f, 50f);
        [Tooltip("Duration of popup open/close animation. Also used as interaction cooldown.")]
        [SerializeField] private float popupAnimationDuration = 0.2f;
        [SerializeField] private bool closeOnMissClick = true;
        
        [Header("Cursor")]
        [SerializeField] private Texture2D defaultCursor;
        [SerializeField] private Texture2D hoverCursor;
        [SerializeField] private Vector2 cursorHotspot = Vector2.zero;
        
        [Header("Clue Discovery Notification")]
        [SerializeField] private GameObject clueNotificationPanel;
        [SerializeField] private TMPro.TextMeshProUGUI clueNotificationText;
        [SerializeField] private float notificationDuration = 2f;
        
        [Header("Raycast Settings")]
        [SerializeField] private LayerMask interactableLayerMask = ~0; // Default: all layers
        
        [Header("Events")]
        public UnityEvent<Interactable> OnInteractionStarted;
        public UnityEvent OnInteractionEnded;
        public UnityEvent<ClueSO> OnClueDiscovered;
        
        // State
        private Interactable currentTarget;
        private Interactable hoveredInteractable;
        private bool isPopupOpen;
        private CanvasGroup popupCanvasGroup;
        private Coroutine notificationCoroutine;
        private float lastInteractionTime;
        private bool isOnCooldown;
        
        // Registered interactables
        private HashSet<Interactable> registeredInteractables = new HashSet<Interactable>();
        
        // Camera reference
        private Camera mainCamera;
        
        public bool IsPopupOpen => isPopupOpen;
        public Interactable CurrentTarget => currentTarget;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            // Setup popup
            if (interactionPopup != null)
            {
                popupCanvasGroup = interactionPopup.GetComponent<CanvasGroup>();
                if (popupCanvasGroup == null)
                {
                    popupCanvasGroup = interactionPopup.AddComponent<CanvasGroup>();
                }
                
                if (popupRectTransform == null)
                {
                    popupRectTransform = interactionPopup.GetComponent<RectTransform>();
                }
                
                interactionPopup.SetActive(false);
            }
            
            // Setup buttons
            if (lookButton != null)
            {
                lookButton.onClick.AddListener(OnLookClicked);
                if (eyeIcon != null)
                {
                    lookButton.image.sprite = eyeIcon;
                }
            }
            
            if (interactButton != null)
            {
                interactButton.onClick.AddListener(OnInteractClicked);
                if (handIcon != null)
                {
                    interactButton.image.sprite = handIcon;
                }
            }
            
            // Hide notification
            if (clueNotificationPanel != null)
            {
                clueNotificationPanel.SetActive(false);
            }
            
            OnInteractionStarted ??= new UnityEvent<Interactable>();
            OnInteractionEnded ??= new UnityEvent();
            OnClueDiscovered ??= new UnityEvent<ClueSO>();
        }
        
        private void Start()
        {
            mainCamera = Camera.main;
        }
        
        private void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            
            if (mouse == null) return;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;
            
            // Update cooldown (uses animation duration)
            if (isOnCooldown && Time.time >= lastInteractionTime + popupAnimationDuration)
            {
                isOnCooldown = false;
            }
            
            // Perform hover detection via raycast
            UpdateHoverDetection(mouse);
            
            // Handle left click only - no right click interaction
            if (mouse.leftButton.wasPressedThisFrame)
            {
                HandleLeftClick(mouse);
            }
            
            // ESC to close popup
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (isPopupOpen)
                {
                    ClosePopup();
                }
            }
        }
        
        #region Raycast Detection
        
        private void UpdateHoverDetection(Mouse mouse)
        {
            Vector2 mouseScreenPos = mouse.position.ReadValue();
            Vector2 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
            
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero, 0f, interactableLayerMask);
            
            Interactable hitInteractable = null;
            
            if (hit.collider != null)
            {
                hitInteractable = hit.collider.GetComponent<Interactable>();
                
                // Only count if registered
                if (hitInteractable != null && !registeredInteractables.Contains(hitInteractable))
                {
                    hitInteractable = null;
                }
            }
            
            // Handle hover state changes
            if (hitInteractable != hoveredInteractable)
            {
                // Exit previous
                if (hoveredInteractable != null)
                {
                    hoveredInteractable.NotifyMouseExit();
                }
                
                // Enter new
                hoveredInteractable = hitInteractable;
                
                if (hoveredInteractable != null)
                {
                    hoveredInteractable.NotifyMouseEnter();
                }
                
                UpdateCursor();
            }
        }
        
        private void HandleLeftClick(Mouse mouse)
        {
            // Block interactions while on cooldown
            if (isOnCooldown)
            {
                return;
            }
            
            // Block interactions while dialogue is playing
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsShowing)
            {
                return;
            }
            
            // Check if clicking on a popup button specifically (not the whole popup area)
            if (isPopupOpen && IsPointerOverPopupButton())
            {
                // Let the button handle it
                return;
            }
            
            // Check if clicking on an interactable
            if (hoveredInteractable != null)
            {
                OnInteractableClicked(hoveredInteractable);
            }
            else if (isPopupOpen)
            {
                // Clicked anywhere else while popup is open - close it
                ClosePopup();
            }
        }
        
        #endregion
        
        #region Registration
        
        public void RegisterInteractable(Interactable interactable)
        {
            if (interactable != null)
            {
                registeredInteractables.Add(interactable);
            }
        }
        
        public void UnregisterInteractable(Interactable interactable)
        {
            if (interactable != null)
            {
                registeredInteractables.Remove(interactable);
                
                // Clear hover if this was hovered
                if (hoveredInteractable == interactable)
                {
                    hoveredInteractable = null;
                    UpdateCursor();
                }
                
                // Clear target if this was target
                if (currentTarget == interactable)
                {
                    ClosePopup();
                }
            }
        }
        
        #endregion
        
        #region Interactable Callbacks (for backwards compatibility)
        
        public void OnInteractableHover(Interactable interactable)
        {
            // Now handled by raycast detection
        }
        
        public void OnInteractableUnhover(Interactable interactable)
        {
            // Now handled by raycast detection
        }
        
        public void OnInteractableClicked(Interactable interactable)
        {
            // Toggle popup
            if (currentTarget == interactable && isPopupOpen)
            {
                ClosePopup();
                return;
            }
            
            OpenPopup(interactable);
        }
        
        #endregion
        
        #region Popup Management
        
        private void OpenPopup(Interactable target)
        {
            // Start cooldown
            lastInteractionTime = Time.time;
            isOnCooldown = true;
            
            currentTarget = target;
            isPopupOpen = true;
            
            // Position popup at the interactable's center (world to screen)
            Vector3 worldPos = target.transform.position;
            Vector2 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            PositionPopup(screenPos);
            
            // Show popup
            interactionPopup.SetActive(true);
            
            // Animate: spiral in (scale + rotation)
            StartCoroutine(SpiralPopupIn());
            
            OnInteractionStarted?.Invoke(target);
        }
        
        private IEnumerator SpiralPopupIn()
        {
            float duration = popupAnimationDuration > 0 ? popupAnimationDuration : 0.2f;
            float elapsed = 0f;
            
            // Starting values
            Vector3 startScale = Vector3.zero;
            Vector3 endScale = Vector3.one;
            float startRotation = -180f;
            float endRotation = 0f;
            
            popupCanvasGroup.alpha = 1f;
            popupRectTransform.localScale = startScale;
            popupRectTransform.localRotation = Quaternion.Euler(0, 0, startRotation);
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Ease out curve for snappy feel
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
                
                popupRectTransform.localScale = Vector3.Lerp(startScale, endScale, easeT);
                float rotation = Mathf.Lerp(startRotation, endRotation, easeT);
                popupRectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
                
                yield return null;
            }
            
            popupRectTransform.localScale = endScale;
            popupRectTransform.localRotation = Quaternion.identity;
        }
        
        private IEnumerator SpiralPopupOut()
        {
            float duration = popupAnimationDuration > 0 ? popupAnimationDuration : 0.2f;
            float elapsed = 0f;
            
            Vector3 startScale = popupRectTransform.localScale;
            Vector3 endScale = Vector3.zero;
            float startRotation = 0f;
            float endRotation = 180f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Ease in curve
                float easeT = t * t;
                
                popupRectTransform.localScale = Vector3.Lerp(startScale, endScale, easeT);
                float rotation = Mathf.Lerp(startRotation, endRotation, easeT);
                popupRectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
                
                yield return null;
            }
            
            popupRectTransform.localScale = Vector3.one; // Reset for next open
            popupRectTransform.localRotation = Quaternion.identity;
            interactionPopup.SetActive(false);
        }
        
        public void ClosePopup()
        {
            if (!isPopupOpen) return;
            
            // Start cooldown
            lastInteractionTime = Time.time;
            isOnCooldown = true;
            
            StartCoroutine(SpiralPopupOut());
            
            isPopupOpen = false;
            currentTarget = null;
            
            OnInteractionEnded?.Invoke();
        }
        
        private void PositionPopup(Vector2 screenPosition)
        {
            if (popupRectTransform == null) return;
            
            Canvas canvas = popupRectTransform.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            
            Vector2 anchoredPos;
            
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Convert screen position to canvas position
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    null,
                    out anchoredPos
                );
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.GetComponent<RectTransform>(),
                    screenPosition,
                    canvas.worldCamera,
                    out anchoredPos
                );
            }
            
            // Apply offset
            anchoredPos += popupOffset;
            
            // Keep on screen
            RectTransform canvasRectTransform = canvas.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRectTransform.rect.size;
            Vector2 popupSize = popupRectTransform.sizeDelta;
            
            // Clamp to canvas bounds
            float halfWidth = popupSize.x / 2;
            float halfHeight = popupSize.y / 2;
            
            anchoredPos.x = Mathf.Clamp(anchoredPos.x, -canvasSize.x/2 + halfWidth, canvasSize.x/2 - halfWidth);
            anchoredPos.y = Mathf.Clamp(anchoredPos.y, -canvasSize.y/2 + halfHeight, canvasSize.y/2 - halfHeight);
            
            popupRectTransform.anchoredPosition = anchoredPos;
        }
        
        private bool IsPointerOverPopupButton()
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            
            // Check if over look button
            if (lookButton != null)
            {
                RectTransform lookRect = lookButton.GetComponent<RectTransform>();
                if (lookRect != null && RectTransformUtility.RectangleContainsScreenPoint(lookRect, mousePos, null))
                {
                    return true;
                }
            }
            
            // Check if over interact button
            if (interactButton != null)
            {
                RectTransform interactRect = interactButton.GetComponent<RectTransform>();
                if (interactRect != null && RectTransformUtility.RectangleContainsScreenPoint(interactRect, mousePos, null))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        // Keep these for potential other uses, but popup now uses spiral
        private IEnumerator FadePopup(float from, float to, float duration)
        {
            float elapsed = 0f;
            popupCanvasGroup.alpha = from;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                popupCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            
            popupCanvasGroup.alpha = to;
        }
        
        private IEnumerator FadePopupAndClose(float from, float to, float duration)
        {
            yield return FadePopup(from, to, duration);
            interactionPopup.SetActive(false);
        }
        
        #endregion
        
        #region Button Handlers
        
        private void OnLookClicked()
        {
            if (currentTarget == null) return;
            
            InteractionResult result = currentTarget.LookAt();
            ClosePopup();
            
            // Show dialogue - prefer sequence over simple dialogue
            if (result.dialogueSequence != null)
            {
                DialogueManager.Instance?.ShowDialogueSequence(result.dialogueSequence);
            }
            else if (!string.IsNullOrEmpty(result.dialogue))
            {
                DialogueManager.Instance?.ShowDialogue(result.dialogue, result.portrait);
            }
            
            // Handle clue discovery (for simple dialogue mode - sequences handle their own)
            if (result.discoveredClue != null)
            {
                ShowClueNotification(result.discoveredClue);
                OnClueDiscovered?.Invoke(result.discoveredClue);
            }
        }
        
        private void OnInteractClicked()
        {
            if (currentTarget == null) return;
            
            InteractionResult result = currentTarget.Interact();
            ClosePopup();
            
            // Show dialogue - prefer sequence over simple dialogue
            if (result.dialogueSequence != null)
            {
                DialogueManager.Instance?.ShowDialogueSequence(result.dialogueSequence);
            }
            else if (!string.IsNullOrEmpty(result.dialogue))
            {
                DialogueManager.Instance?.ShowDialogue(result.dialogue, result.portrait);
            }
            
            // Handle clue discovery (for simple dialogue mode - sequences handle their own)
            if (result.discoveredClue != null)
            {
                ShowClueNotification(result.discoveredClue);
                OnClueDiscovered?.Invoke(result.discoveredClue);
            }
        }
        
        #endregion
        
        #region Clue Notification
        
        private void ShowClueNotification(ClueSO clue)
        {
            if (clueNotificationPanel == null) return;
            
            if (notificationCoroutine != null)
            {
                StopCoroutine(notificationCoroutine);
            }
            
            if (clueNotificationText != null)
            {
                clueNotificationText.text = $"Clue Found: {clue.clueName}";
            }
            
            clueNotificationPanel.SetActive(true);
            notificationCoroutine = StartCoroutine(HideNotificationAfterDelay());
        }
        
        private IEnumerator HideNotificationAfterDelay()
        {
            yield return new WaitForSeconds(notificationDuration);
            
            if (clueNotificationPanel != null)
            {
                clueNotificationPanel.SetActive(false);
            }
            
            notificationCoroutine = null;
        }
        
        #endregion
        
        #region Cursor Management
        
        private void UpdateCursor()
        {
            if (hoveredInteractable != null)
            {
                if (hoverCursor != null)
                {
                    Cursor.SetCursor(hoverCursor, cursorHotspot, CursorMode.Auto);
                }
            }
            else
            {
                if (defaultCursor != null)
                {
                    Cursor.SetCursor(defaultCursor, cursorHotspot, CursorMode.Auto);
                }
                else
                {
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                }
            }
        }
        
        #endregion
    }
}