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
        public ConditionalDialogue GetMatchingConditionalDialogue(string objectId = null)
        {
            if (conditionalDialogues == null || conditionalDialogues.Length == 0)
                return null;
            
            Debug.Log($"[CharacterInteractionData] Checking {conditionalDialogues.Length} conditional dialogues for {objectId}");
            
            foreach (var cd in conditionalDialogues)
            {
                if (cd.ShouldShow(objectId))
                    return cd;
            }
            
            Debug.Log($"[CharacterInteractionData] No conditional dialogue matched, using fallback");
            return null;
        }
    }
    
    [System.Serializable]
    public class ConditionalDialogue
    {
        [Header("Dialogue")]
        [Tooltip("The dialogue sequence to play")]
        public DialogueSequenceSO dialogue;
        
        [Header("Play Once")]
        [Tooltip("If true, this dialogue only plays once ever (auto-generates skip flag)")]
        public bool playOnce = false;
        
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
        
        // Runtime ID for playOnce
        [System.NonSerialized] private string _cachedPlayOnceFlag;
        
        /// <summary>
        /// Get unique flag name for playOnce.
        /// </summary>
        public string GetPlayOnceFlag(string objectId)
        {
            if (dialogue == null) return null;
            return $"played_{objectId}_{dialogue.name}";
        }
        
        /// <summary>
        /// Check if this conditional dialogue should show.
        /// </summary>
        public bool ShouldShow(string objectId = null)
        {
            // Check playOnce
            if (playOnce && !string.IsNullOrEmpty(objectId))
            {
                string playOnceFlag = GetPlayOnceFlag(objectId);
                if (GameManager.Instance != null && GameManager.Instance.HasFlag(playOnceFlag))
                {
                    Debug.Log($"[ConditionalDialogue] Skipping (playOnce already played): {dialogue?.name}");
                    return false;
                }
            }
            
            // Check requires flag
            if (!string.IsNullOrEmpty(requiresFlag))
            {
                bool hasFlag = GameManager.Instance != null && GameManager.Instance.HasFlag(requiresFlag);
                Debug.Log($"[ConditionalDialogue] Checking requiresFlag '{requiresFlag}': {hasFlag}");
                if (!hasFlag)
                    return false;
            }
            
            // Check requires clue
            if (requiresClue != null)
            {
                bool hasClue = ClueManager.Instance != null && ClueManager.Instance.HasClue(requiresClue);
                Debug.Log($"[ConditionalDialogue] Checking requiresClue '{requiresClue.name}': {hasClue}");
                if (!hasClue)
                    return false;
            }
            
            // Check skip flag
            if (!string.IsNullOrEmpty(skipIfFlag))
            {
                bool hasFlag = GameManager.Instance != null && GameManager.Instance.HasFlag(skipIfFlag);
                Debug.Log($"[ConditionalDialogue] Checking skipIfFlag '{skipIfFlag}': {hasFlag}");
                if (hasFlag)
                    return false;
            }
            
            // Check skip clue
            if (skipIfClue != null)
            {
                bool hasClue = ClueManager.Instance != null && ClueManager.Instance.HasClue(skipIfClue);
                Debug.Log($"[ConditionalDialogue] Checking skipIfClue '{skipIfClue.name}': {hasClue}");
                if (hasClue)
                    return false;
            }
            
            Debug.Log($"[ConditionalDialogue] MATCH: {dialogue?.name}");
            return true;
        }
        
        /// <summary>
        /// Execute after-show effects (set flag, discover clue, mark playOnce).
        /// </summary>
        public void ExecuteAfterEffects(string objectId = null)
        {
            // Mark playOnce
            if (playOnce && !string.IsNullOrEmpty(objectId))
            {
                string playOnceFlag = GetPlayOnceFlag(objectId);
                GameManager.Instance?.SetFlag(playOnceFlag);
                Debug.Log($"[ConditionalDialogue] Set playOnce flag: {playOnceFlag}");
            }
            
            if (!string.IsNullOrEmpty(setFlagAfter))
            {
                GameManager.Instance?.SetFlag(setFlagAfter);
                Debug.Log($"[ConditionalDialogue] Set flag: {setFlagAfter}");
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