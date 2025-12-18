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
        
        [Header("Cursor")]
        [SerializeField] private Texture2D defaultCursor;
        [SerializeField] private Texture2D hoverCursor;
        [SerializeField] private Vector2 cursorHotspot = Vector2.zero;
        
        [Header("Clue Discovery Notification")]
        [SerializeField] private GameObject clueNotificationPanel;
        [SerializeField] private TMPro.TextMeshProUGUI clueNotificationText;
        [SerializeField] private float notificationDuration = 2f;
        
        [Header("Raycast Settings")]
        [SerializeField] private LayerMask interactableLayerMask = ~0;
        
        [Header("Events")]
        public UnityEvent<Interactable> OnInteractionStarted;
        public UnityEvent OnInteractionEnded;
        public UnityEvent<ClueSO> OnClueDiscovered;
        
        // State
        private Interactable currentTarget;
        private Interactable hoveredInteractable;
        private Coroutine notificationCoroutine;
        private float lastInteractionTime;
        private bool isOnCooldown;
        private float cooldownDuration = 0.3f;
        
        // Registered interactables
        private HashSet<Interactable> registeredInteractables = new HashSet<Interactable>();
        
        // Camera reference
        private Camera mainCamera;
        
        public bool IsPopupOpen => false;
        public Interactable CurrentTarget => currentTarget;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
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
            
            if (mouse == null) return;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;
            
            // Update cooldown
            if (isOnCooldown && Time.time >= lastInteractionTime + cooldownDuration)
            {
                isOnCooldown = false;
            }
            
            // Hover detection
            UpdateHoverDetection(mouse);
            
            // Left click triggers LookAt
            if (mouse.leftButton.wasPressedThisFrame)
            {
                HandleLeftClick();
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
                
                if (hitInteractable != null && !registeredInteractables.Contains(hitInteractable))
                {
                    hitInteractable = null;
                }
            }
            
            if (hitInteractable != hoveredInteractable)
            {
                if (hoveredInteractable != null)
                {
                    hoveredInteractable.NotifyMouseExit();
                }
                
                hoveredInteractable = hitInteractable;
                
                if (hoveredInteractable != null)
                {
                    hoveredInteractable.NotifyMouseEnter();
                }
                
                UpdateCursor();
            }
        }
        
        private void HandleLeftClick()
        {
            if (isOnCooldown) return;
            
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsShowing) return;
            
            if (hoveredInteractable != null)
            {
                PerformLookAt(hoveredInteractable);
            }
        }
        
        #endregion
        
        #region Interaction
        
        private void PerformLookAt(Interactable target)
        {
            if (target == null) return;
            
            lastInteractionTime = Time.time;
            isOnCooldown = true;
            
            currentTarget = target;
            OnInteractionStarted?.Invoke(target);
            
            InteractionResult result = target.LookAt();
            
            Debug.Log($"[InteractionManager] PerformLookAt result: dialogueSequence={result.dialogueSequence}, dialogue='{result.dialogue}', sceneChange={result.triggeredSceneChange}, targetScene={result.targetSceneId}");
            
            // Store pending scene change (will execute after dialogue ends)
            string pendingSceneChange = null;
            if (result.triggeredSceneChange && !string.IsNullOrEmpty(result.targetSceneId))
            {
                pendingSceneChange = result.targetSceneId;
            }
            
            if (result.dialogueSequence != null)
            {
                Debug.Log($"[InteractionManager] Calling ShowDialogueSequence with: {result.dialogueSequence.name}");
                
                // If scene change is on Interactable but not in sequence, set it on the sequence temporarily
                if (!string.IsNullOrEmpty(pendingSceneChange) && 
                    string.IsNullOrEmpty(result.dialogueSequence.triggerSceneChangeOnComplete))
                {
                    // Note: This modifies the ScriptableObject! Better to handle via callback
                    Debug.Log($"[InteractionManager] Scene change '{pendingSceneChange}' will happen after dialogue via Interactable setting");
                    ScheduleSceneChangeAfterDialogue(pendingSceneChange);
                }
                
                DialogueManager.Instance?.ShowDialogueSequence(result.dialogueSequence);
            }
            else if (!string.IsNullOrEmpty(result.dialogue))
            {
                Debug.Log($"[InteractionManager] Calling ShowDialogue with: '{result.dialogue}'");
                
                // For simple dialogue, schedule scene change after it ends
                if (!string.IsNullOrEmpty(pendingSceneChange))
                {
                    ScheduleSceneChangeAfterDialogue(pendingSceneChange);
                }
                
                DialogueManager.Instance?.ShowDialogue(result.dialogue, result.portrait);
            }
            else
            {
                Debug.Log($"[InteractionManager] No dialogue to show!");
                
                // No dialogue - immediate scene change
                if (!string.IsNullOrEmpty(pendingSceneChange))
                {
                    Debug.Log($"[InteractionManager] Immediate scene change to: {pendingSceneChange}");
                    GameSceneManager.Instance?.LoadScene(pendingSceneChange);
                }
            }
            
            if (result.discoveredClue != null)
            {
                ShowClueNotification(result.discoveredClue);
                OnClueDiscovered?.Invoke(result.discoveredClue);
            }
            
            currentTarget = null;
            OnInteractionEnded?.Invoke();
        }
        
        private string pendingSceneChangeAfterDialogue;
        
        private void ScheduleSceneChangeAfterDialogue(string sceneId)
        {
            pendingSceneChangeAfterDialogue = sceneId;
            
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnDialogueEnded.AddListener(OnDialogueEndedForSceneChange);
            }
        }
        
        private void OnDialogueEndedForSceneChange()
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.OnDialogueEnded.RemoveListener(OnDialogueEndedForSceneChange);
            }
            
            if (!string.IsNullOrEmpty(pendingSceneChangeAfterDialogue))
            {
                Debug.Log($"[InteractionManager] Dialogue ended - changing scene to: {pendingSceneChangeAfterDialogue}");
                string sceneId = pendingSceneChangeAfterDialogue;
                pendingSceneChangeAfterDialogue = null;
                GameSceneManager.Instance?.LoadScene(sceneId);
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
                
                if (hoveredInteractable == interactable)
                {
                    hoveredInteractable = null;
                    UpdateCursor();
                }
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
        
        #region Legacy (kept for compatibility)
        
        public void ClosePopup() { }
        
        #endregion
    }
}