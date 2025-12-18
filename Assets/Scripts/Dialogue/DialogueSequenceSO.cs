using UnityEngine;
using System.Collections.Generic;

namespace PointClickDetective
{
    [CreateAssetMenu(fileName = "NewDialogueSequence", menuName = "Point Click Detective/Dialogue Sequence")]
    public class DialogueSequenceSO : ScriptableObject
    {
        [Header("Dialogue Lines")]
        [Tooltip("The sequence of dialogue lines to play")]
        public List<DialogueLine> lines = new List<DialogueLine>();
        
        [Header("Completion Triggers")]
        [Tooltip("Flag to set when this entire sequence completes")]
        public string setFlagOnComplete;
        
        [Tooltip("Clue to discover when this entire sequence completes")]
        public ClueSO discoverClueOnComplete;
        
        [Tooltip("Question to reveal when this entire sequence completes")]
        public QuestionSO revealQuestionOnComplete;
        
        [Tooltip("Scene to change to when this entire sequence completes (after dialogue closes)")]
        public string triggerSceneChangeOnComplete;
    }
    
    [System.Serializable]
    public class DialogueLine
    {
        [Header("Speaker")]
        [Tooltip("Who is speaking this line. Determines portrait and typewriter sounds.")]
        public SpeakerSO speaker;
        
        [Header("Text")]
        [TextArea(2, 6)]
        [Tooltip("The dialogue text to display. Supports rich text and <delay:500> for inline pauses (milliseconds).")]
        public string text;
        
        [Header("Mid-Dialogue Triggers")]
        [Tooltip("Flag to set when this line is shown")]
        public string setFlagOnShow;
        
        [Tooltip("Clue to discover when this line is shown")]
        public ClueSO discoverClueOnShow;
        
        [Tooltip("Question to reveal when this line is shown")]
        public QuestionSO revealQuestionOnShow;
        
        [Header("Conditions")]
        [Tooltip("Only show this line if this flag is set (leave empty to always show)")]
        public string requiresFlag;
        
        [Tooltip("Only show this line if this clue has been found")]
        public ClueSO requiresClue;
        
        [Tooltip("Skip this line if this flag is set")]
        public string skipIfFlag;
        
        [Header("Special")]
        [Tooltip("If set, triggers a scene change after this line")]
        public string triggerSceneChange;
        
        [Tooltip("Delay before showing this line (seconds)")]
        public float delayBefore = 0f;
        
        /// <summary>
        /// Check if this line should be shown based on conditions.
        /// </summary>
        public bool ShouldShow()
        {
            // Check required flag
            if (!string.IsNullOrEmpty(requiresFlag))
            {
                if (GameManager.Instance == null || !GameManager.Instance.HasFlag(requiresFlag))
                    return false;
            }
            
            // Check required clue
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
            
            return true;
        }
        
        /// <summary>
        /// Execute any triggers for this line.
        /// </summary>
        public void ExecuteTriggers()
        {
            // Set flag
            if (!string.IsNullOrEmpty(setFlagOnShow))
            {
                GameManager.Instance?.SetFlag(setFlagOnShow);
            }
            
            // Discover clue
            if (discoverClueOnShow != null)
            {
                ClueManager.Instance?.DiscoverClue(discoverClueOnShow);
            }
            
            // Reveal question
            if (revealQuestionOnShow != null)
            {
                DeductionBoardUI.Instance?.RevealQuestion(revealQuestionOnShow);
            }
        }
        
        /// <summary>
        /// Get the portrait to display for this line.
        /// </summary>
        public Sprite GetPortrait()
        {
            if (speaker != null) return speaker.defaultPortrait;
            return null;
        }
        
        /// <summary>
        /// Check if portrait should be hidden for this line.
        /// </summary>
        public bool ShouldHidePortrait()
        {
            // Hide if speaker says to hide, or if no portrait available
            if (speaker != null && speaker.hidePortrait) return true;
            return GetPortrait() == null;
        }
    }
}