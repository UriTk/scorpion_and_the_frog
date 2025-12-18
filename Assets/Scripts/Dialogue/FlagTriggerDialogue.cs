using UnityEngine;
using System.Collections.Generic;

namespace PointClickDetective
{
    /// <summary>
    /// Triggers a dialogue sequence when specific flags are raised.
    /// Attach to any GameObject in the scene.
    /// </summary>
    public class FlagTriggerDialogue : MonoBehaviour
    {
        [Header("Required Flags")]
        [Tooltip("All these flags must be set to trigger the dialogue")]
        [SerializeField] private string[] requiredFlags;
        
        [Header("Dialogue")]
        [Tooltip("Dialogue to play when all flags are set")]
        [SerializeField] private DialogueSequenceSO dialogueToTrigger;
        
        [Header("Options")]
        [Tooltip("If true, only triggers once per session")]
        [SerializeField] private bool triggerOnce = true;
        
        [Tooltip("Custom flag to mark as triggered (auto-generated if empty)")]
        [SerializeField] private string triggeredFlag;
        
        [Tooltip("Delay before showing dialogue (seconds)")]
        [SerializeField] private float triggerDelay = 0.1f;
        
        [Tooltip("If true, checks flags every frame. If false, only checks when a flag is set.")]
        [SerializeField] private bool pollEveryFrame = false;
        
        // State
        private bool hasTriggered = false;
        private bool isWaitingToTrigger = false;
        
        private void OnEnable()
        {
            Debug.Log($"[FlagTriggerDialogue] OnEnable: {gameObject.name}, requiredFlags={requiredFlags?.Length ?? 0}");
            
            // Generate triggered flag if not specified
            if (triggerOnce && string.IsNullOrEmpty(triggeredFlag))
            {
                triggeredFlag = $"flagtrigger_{gameObject.name}_{dialogueToTrigger?.name ?? "null"}";
            }
            
            // Check if already triggered
            if (triggerOnce && !string.IsNullOrEmpty(triggeredFlag))
            {
                if (GameManager.Instance != null && GameManager.Instance.HasFlag(triggeredFlag))
                {
                    hasTriggered = true;
                    Debug.Log($"[FlagTriggerDialogue] Already triggered previously: {triggeredFlag}");
                    return;
                }
            }
            
            // Subscribe to flag changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnFlagSet.AddListener(OnFlagSet);
                Debug.Log($"[FlagTriggerDialogue] Subscribed to OnFlagSet");
            }
            else
            {
                Debug.LogWarning($"[FlagTriggerDialogue] GameManager.Instance is null!");
            }
            
            // Initial check in case all flags are already set
            CheckAndTrigger();
        }
        
        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnFlagSet.RemoveListener(OnFlagSet);
            }
        }
        
        private void Update()
        {
            if (pollEveryFrame && !hasTriggered && !isWaitingToTrigger)
            {
                CheckAndTrigger();
            }
        }
        
        private void OnFlagSet(string flagName)
        {
            Debug.Log($"[FlagTriggerDialogue] OnFlagSet received: {flagName}, hasTriggered={hasTriggered}");
            
            if (hasTriggered || isWaitingToTrigger) return;
            
            // Check if this flag is one we care about
            bool isRelevant = false;
            foreach (var flag in requiredFlags)
            {
                if (flag == flagName)
                {
                    isRelevant = true;
                    break;
                }
            }
            
            Debug.Log($"[FlagTriggerDialogue] Flag '{flagName}' relevant: {isRelevant}");
            
            if (isRelevant)
            {
                CheckAndTrigger();
            }
        }
        
        private void CheckAndTrigger()
        {
            if (hasTriggered || isWaitingToTrigger) return;
            if (dialogueToTrigger == null)
            {
                Debug.LogWarning($"[FlagTriggerDialogue] dialogueToTrigger is null!");
                return;
            }
            if (requiredFlags == null || requiredFlags.Length == 0)
            {
                Debug.LogWarning($"[FlagTriggerDialogue] No required flags set!");
                return;
            }
            if (GameManager.Instance == null)
            {
                Debug.LogWarning($"[FlagTriggerDialogue] GameManager.Instance is null!");
                return;
            }
            
            // Check all required flags
            foreach (var flag in requiredFlags)
            {
                if (string.IsNullOrEmpty(flag)) continue;
                
                bool hasFlag = GameManager.Instance.HasFlag(flag);
                Debug.Log($"[FlagTriggerDialogue] Checking flag '{flag}': {hasFlag}");
                
                if (!hasFlag)
                {
                    return; // Missing a flag, don't trigger
                }
            }
            
            // All flags are set - trigger!
            hasTriggered = true;
            isWaitingToTrigger = true;
            
            // Mark as triggered
            if (triggerOnce && !string.IsNullOrEmpty(triggeredFlag))
            {
                GameManager.Instance.SetFlag(triggeredFlag);
            }
            
            Debug.Log($"[FlagTriggerDialogue] All {requiredFlags.Length} flags set! Triggering: {dialogueToTrigger.name}");
            
            if (triggerDelay > 0)
            {
                StartCoroutine(TriggerAfterDelay());
            }
            else
            {
                StartCoroutine(WaitForDialogueAndTrigger());
            }
        }
        
        private System.Collections.IEnumerator TriggerAfterDelay()
        {
            Debug.Log($"[FlagTriggerDialogue] Waiting {triggerDelay}s before trigger...");
            yield return new WaitForSeconds(triggerDelay);
            yield return StartCoroutine(WaitForDialogueAndTrigger());
        }
        
        private System.Collections.IEnumerator WaitForDialogueAndTrigger()
        {
            // Wait for any current dialogue to finish
            int waitFrames = 0;
            while (DialogueManager.Instance != null && DialogueManager.Instance.IsShowing)
            {
                if (waitFrames % 60 == 0) // Log every ~1 second
                {
                    Debug.Log($"[FlagTriggerDialogue] Waiting for current dialogue to finish...");
                }
                waitFrames++;
                yield return null;
            }
            
            // Small buffer after dialogue ends
            Debug.Log($"[FlagTriggerDialogue] Dialogue finished, waiting 0.3s buffer...");
            yield return new WaitForSeconds(0.3f);
            
            // Double-check dialogue didn't start again
            while (DialogueManager.Instance != null && DialogueManager.Instance.IsShowing)
            {
                yield return null;
            }
            
            isWaitingToTrigger = false;
            
            Debug.Log($"[FlagTriggerDialogue] NOW playing dialogue: {dialogueToTrigger.name}");
            
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.ShowDialogueSequence(dialogueToTrigger);
            }
            else
            {
                Debug.LogError($"[FlagTriggerDialogue] DialogueManager.Instance is null!");
            }
        }
        
        /// <summary>
        /// Manually trigger the dialogue (ignores flag requirements).
        /// </summary>
        public void ForceTrigger()
        {
            if (hasTriggered && triggerOnce) return;
            
            hasTriggered = true;
            
            if (triggerOnce && !string.IsNullOrEmpty(triggeredFlag))
            {
                GameManager.Instance?.SetFlag(triggeredFlag);
            }
            
            StartCoroutine(WaitForDialogueAndTrigger());
        }
        
        /// <summary>
        /// Reset the trigger so it can fire again.
        /// </summary>
        public void ResetTrigger()
        {
            hasTriggered = false;
            isWaitingToTrigger = false;
            
            if (!string.IsNullOrEmpty(triggeredFlag))
            {
                GameManager.Instance?.RemoveFlag(triggeredFlag);
            }
        }
    }
}