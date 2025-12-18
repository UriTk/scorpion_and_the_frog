using UnityEngine;

namespace PointClickDetective
{
    [CreateAssetMenu(fileName = "New Clue", menuName = "Point Click Detective/Clue")]
    public class ClueSO : ScriptableObject
    {
        [Header("Identification")]
        public string clueId;
        public int clueNumber;
        
        [Header("Display")]
        public string clueName;
        [TextArea(3, 6)]
        public string description;
        [TextArea(2, 4)]
        public string additionalDescription;
        public Sprite icon;
        
        [Header("Discovery")]
        [Tooltip("Which character can find this clue")]
        public ClueVisibility visibleTo;
        [Tooltip("Why this character can find it (e.g., 'can see colors', 'can read')")]
        public string visibilityReason;
        
        [Header("Location")]
        public string locationSceneId;
        
        [Header("Deduction Links")]
        [Tooltip("Which question IDs this clue helps answer")]
        public int[] relatedQuestionIds;
        
        [Header("Clue Type")]
        public bool isFakeClue;
        [TextArea(2, 3)]
        public string fakeClueExplanation;
        
        /// <summary>
        /// Check if this clue is visible to the given character.
        /// </summary>
        public bool IsVisibleTo(CharacterType character)
        {
            switch (visibleTo)
            {
                case ClueVisibility.Both:
                    return true;
                case ClueVisibility.ScorpionOnly:
                    return character == CharacterType.Scorpion;
                case ClueVisibility.FrogOnly:
                    return character == CharacterType.Frog;
                default:
                    return true;
            }
        }
    }
    
    public enum ClueVisibility
    {
        Both,
        ScorpionOnly,
        FrogOnly
    }
}
