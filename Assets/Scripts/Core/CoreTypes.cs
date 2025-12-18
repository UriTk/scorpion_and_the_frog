using UnityEngine;

namespace PointClickDetective
{
    public enum CharacterType { Scorpion, Frog }
    
    [System.Serializable]
    public class CharacterInteractionData
    {
        [HideInInspector] public CharacterType character;
        
        [Header("Visibility")]
        public bool isVisibleToThisCharacter = true;
        
        [Header("Conditional Dialogues (Checked In Order)")]
        [Tooltip("List of dialogues with conditions. First matching one plays. If none match, falls back to simple dialogue below.")]
        public ConditionalDialogue[] conditionalDialogues;
        
        [Header("Fallback - Simple Dialogue")]
        [Tooltip("Simple dialogue text if no conditional dialogue matches.")]
        [TextArea(2, 5)] public string lookAtDialogue;
        public Sprite lookAtPortrait;
        
        [Header("Fallback - Sequence")]
        [Tooltip("Fallback dialogue sequence if no conditional dialogue matches.")]
        public DialogueSequenceSO lookAtSequence;
        
        [Header("Interact - Simple (Legacy)")]
        [Tooltip("Simple dialogue text for interacting. Use this OR interactSequence, not both.")]
        [TextArea(2, 5)] public string interactDialogue;
        public Sprite interactPortrait;
        
        [Header("Interact - Sequence (Legacy)")]
        [Tooltip("Full dialogue sequence for interacting. Overrides simple dialogue if set.")]
        public DialogueSequenceSO interactSequence;
        
        [Header("Prerequisites")]
        public InteractionPrerequisite[] prerequisites;
        
        [Header("Clue Discovery (used if no sequence)")]
        [Tooltip("Clue discovered when looking at this object")]
        public ClueSO clueOnLook;
        [Tooltip("Clue discovered when interacting with this object")]
        public ClueSO clueOnInteract;
        [Tooltip("Question revealed when interacting (e.g., finding a key piece of evidence)")]
        public QuestionSO questionRevealed;
        
        [Header("Effects")]
        public bool triggersSceneChange;
        public string targetSceneId;
        public string unlockFlagOnLook;
        public string unlockFlagOnInteract;
        
        /// <summary>
        /// Returns true if this uses a dialogue sequence for Look.
        /// </summary>
        public bool HasLookSequence => lookAtSequence != null;
        
        /// <summary>
        /// Returns true if this uses a dialogue sequence for Interact.
        /// </summary>
        public bool HasInteractSequence => interactSequence != null;
        
        /// <summary>
        /// Get the first matching conditional dialogue, or null if none match.
        /// </summary>
        public ConditionalDialogue GetMatchingConditionalDialogue()
        {
            if (conditionalDialogues == null || conditionalDialogues.Length == 0)
                return null;
            
            foreach (var cd in conditionalDialogues)
            {
                if (cd.ShouldShow())
                    return cd;
            }
            
            return null;
        }
    }
    
    [System.Serializable]
    public class ConditionalDialogue
    {
        [Header("Dialogue")]
        [Tooltip("The dialogue sequence to play")]
        public DialogueSequenceSO dialogue;
        
        [Header("Conditions (ALL must be met)")]
        [Tooltip("Only show if this flag IS set (leave empty to ignore)")]
        public string requiresFlag;
        
        [Tooltip("Only show if this clue has been found")]
        public ClueSO requiresClue;
        
        [Tooltip("Skip if this flag IS set (leave empty to ignore)")]
        public string skipIfFlag;
        
        [Tooltip("Skip if this clue has been found")]
        public ClueSO skipIfClue;
        
        [Header("After Showing")]
        [Tooltip("Flag to set after this dialogue plays (useful to prevent repeat)")]
        public string setFlagAfter;
        
        [Tooltip("Clue to discover after this dialogue plays")]
        public ClueSO discoverClueAfter;
        
        /// <summary>
        /// Check if this conditional dialogue should show.
        /// </summary>
        public bool ShouldShow()
        {
            // Check requires flag
            if (!string.IsNullOrEmpty(requiresFlag))
            {
                if (GameManager.Instance == null || !GameManager.Instance.HasFlag(requiresFlag))
                    return false;
            }
            
            // Check requires clue
            if (requiresClue != null)
            {
                if (ClueManager.Instance == null || !ClueManager.Instance.HasClue(requiresClue))
                    return false;
            }
            
            // Check skip flag
            if (!string.IsNullOrEmpty(skipIfFlag))
            {
                if (GameManager.Instance != null && GameManager.Instance.HasFlag(skipIfFlag))
                    return false;
            }
            
            // Check skip clue
            if (skipIfClue != null)
            {
                if (ClueManager.Instance != null && ClueManager.Instance.HasClue(skipIfClue))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Execute after-show effects (set flag, discover clue).
        /// </summary>
        public void ExecuteAfterEffects()
        {
            if (!string.IsNullOrEmpty(setFlagAfter))
            {
                GameManager.Instance?.SetFlag(setFlagAfter);
            }
            
            if (discoverClueAfter != null)
            {
                ClueManager.Instance?.DiscoverClue(discoverClueAfter);
            }
        }
    }
    
    [System.Serializable]
    public class InteractionPrerequisite
    {
        public PrerequisiteType type;
        public string flagName;
        [Tooltip("For RequiresClue type - which clue must be found")]
        public ClueSO requiredClue;
        [TextArea(2, 3)] public string lockedLookDialogue;
        [TextArea(2, 3)] public string lockedInteractDialogue;
    }
    
    public enum PrerequisiteType 
    { 
        RequiresFlag, 
        RequiresClue,       // Must have found a specific clue
        RequiresLookedAt, 
        RequiresInteracted 
    }
    
    public struct InteractionResult
    {
        public bool success;
        public string dialogue;
        public Sprite portrait;
        public bool isLocked;
        public ClueSO discoveredClue;
        public QuestionSO revealedQuestion;
        public bool triggeredSceneChange;
        public string targetSceneId;
        public DialogueSequenceSO dialogueSequence;
        public ConditionalDialogue conditionalDialogue; // Track which conditional was used
    }
}