using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace PointClickDetective
{
    [CreateAssetMenu(fileName = "New Question", menuName = "Point Click Detective/Deduction Question")]
    public class QuestionSO : ScriptableObject
    {
        [Header("Identification")]
        public int questionId;
        
        [Header("Question Text")]
        [Tooltip("The question text with ___ for the blank")]
        [TextArea(2, 4)]
        public string questionText;
        
        [Header("Answers")]
        [Tooltip("The correct answer and the clues required to unlock it")]
        public AnswerOption correctAnswer;
        
        [Tooltip("Wrong answers - each tied to clues that unlock them")]
        public AnswerOption[] fakeAnswers;
        
        [Header("Context")]
        [TextArea(2, 3)]
        public string additionalDescription;
        public QuestionDifficulty difficulty;
        
        [Header("Display")]
        [Tooltip("Category for grouping in the deduction board")]
        public QuestionCategory category;
        [Tooltip("Order within category")]
        public int displayOrder;
        
        [Header("On Correct Answer")]
        [Tooltip("Dialogue to play when this question is answered correctly")]
        public DialogueSequenceSO dialogueOnCorrect;
        
        [Tooltip("Flag to set when this question is answered correctly")]
        public string setFlagOnCorrect;
        
        [Header("On Wrong Answer")]
        [Tooltip("Dialogue to play when this question is answered incorrectly")]
        public DialogueSequenceSO dialogueOnWrong;
        
        /// <summary>
        /// Check if this question is unlocked (at least one answer is available).
        /// </summary>
        public bool IsUnlocked(HashSet<int> foundClueIds)
        {
            // Question unlocks when ANY answer becomes available
            if (IsAnswerAvailable(correctAnswer, foundClueIds))
                return true;
            
            if (fakeAnswers != null)
            {
                foreach (var answer in fakeAnswers)
                {
                    if (IsAnswerAvailable(answer, foundClueIds))
                        return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if a specific answer is available based on found clues or flags.
        /// </summary>
        public bool IsAnswerAvailable(AnswerOption answer, HashSet<int> foundClueIds)
        {
            if (answer == null) return false;
            
            // Check if unlocked via flag
            if (!string.IsNullOrEmpty(answer.unlockFlag))
            {
                if (GameManager.Instance != null && GameManager.Instance.HasFlag(answer.unlockFlag))
                    return true;
            }
            
            // If no required clues, always available
            if (answer.requiredClueIds == null || answer.requiredClueIds.Length == 0)
            {
                // But only if there's no flag requirement either
                return string.IsNullOrEmpty(answer.unlockFlag);
            }
            
            // Need ALL required clues to unlock this answer
            return answer.requiredClueIds.All(id => foundClueIds.Contains(id));
        }
        
        /// <summary>
        /// Get all currently available answers.
        /// </summary>
        public List<AnswerOption> GetAvailableAnswers(HashSet<int> foundClueIds)
        {
            var available = new List<AnswerOption>();
            
            if (IsAnswerAvailable(correctAnswer, foundClueIds))
                available.Add(correctAnswer);
            
            if (fakeAnswers != null)
            {
                foreach (var answer in fakeAnswers)
                {
                    if (IsAnswerAvailable(answer, foundClueIds))
                        available.Add(answer);
                }
            }
            
            return available;
        }
        
        /// <summary>
        /// Get all available answers shuffled for display.
        /// </summary>
        public List<AnswerOption> GetShuffledAvailableAnswers(HashSet<int> foundClueIds)
        {
            var answers = GetAvailableAnswers(foundClueIds);
            
            // Fisher-Yates shuffle
            for (int i = answers.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (answers[i], answers[j]) = (answers[j], answers[i]);
            }
            
            return answers;
        }
        
        /// <summary>
        /// Check if the correct answer is currently available.
        /// </summary>
        public bool CanAnswerCorrectly(HashSet<int> foundClueIds)
        {
            return IsAnswerAvailable(correctAnswer, foundClueIds);
        }
        
        /// <summary>
        /// Check if an answer text matches the correct answer.
        /// </summary>
        public bool IsCorrectAnswer(string answerText)
        {
            return correctAnswer != null && correctAnswer.answerText == answerText;
        }
    }
    
    [System.Serializable]
    public class AnswerOption
    {
        [Tooltip("The answer text that fills in the blank")]
        public string answerText;
        
        [Tooltip("Clue IDs required to unlock this answer (ALL must be found)")]
        public int[] requiredClueIds;
        
        [Tooltip("Alternative: Flag that unlocks this answer (leave empty to use clues only)")]
        public string unlockFlag;
        
        public override string ToString() => answerText;
    }
    
    public enum QuestionDifficulty
    {
        Easy,
        Medium,
        MediumHard,
        Hard
    }
    
    public enum QuestionCategory
    {
        Murder,         // Q1, Q2 - weapon, body disposal
        Timeline,       // Q12 - when it happened
        PlannedRoute,   // Q3, Q4, Q5, Q13 - original plan
        ActualRoute,    // Q6, Q7, Q8, Q9, Q10 - what actually happened
        Killer          // Q11 - whodunit
    }
}