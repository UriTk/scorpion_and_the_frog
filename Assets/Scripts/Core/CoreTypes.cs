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
        
        [Header("Look At - Simple")]
        [Tooltip("Simple dialogue text for looking. Use this OR lookAtSequence, not both.")]
        [TextArea(2, 5)] public string lookAtDialogue;
        public Sprite lookAtPortrait;
        
        [Header("Look At - Sequence")]
        [Tooltip("Full dialogue sequence for looking. Overrides simple dialogue if set.")]
        public DialogueSequenceSO lookAtSequence;
        
        [Header("Interact - Simple")]
        [Tooltip("Simple dialogue text for interacting. Use this OR interactSequence, not both.")]
        [TextArea(2, 5)] public string interactDialogue;
        public Sprite interactPortrait;
        
        [Header("Interact - Sequence")]
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
        public DialogueSequenceSO dialogueSequence; // New: full sequence if available
    }
}