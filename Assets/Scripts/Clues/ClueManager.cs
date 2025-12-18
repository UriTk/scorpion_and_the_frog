using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

namespace PointClickDetective
{
    /// <summary>
    /// Manages clue discovery and storage. Replaces the inventory system.
    /// </summary>
    public class ClueManager : MonoBehaviour
    {
        public static ClueManager Instance { get; private set; }
        
        [Header("All Game Clues")]
        [Tooltip("Reference all clues in the game for lookup")]
        [SerializeField] private ClueSO[] allClues;
        
        [Header("Events")]
        public UnityEvent<ClueSO> OnClueFound;
        public UnityEvent OnCluesChanged;
        
        // State
        private HashSet<int> foundClueIds = new HashSet<int>();
        private Dictionary<int, ClueSO> clueLookup = new Dictionary<int, ClueSO>();
        
        public IReadOnlyCollection<int> FoundClueIds => foundClueIds;
        public int FoundClueCount => foundClueIds.Count;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Build lookup
            if (allClues != null)
            {
                foreach (var clue in allClues)
                {
                    if (clue != null)
                    {
                        clueLookup[clue.clueNumber] = clue;
                    }
                }
            }
            
            OnClueFound ??= new UnityEvent<ClueSO>();
            OnCluesChanged ??= new UnityEvent();
        }
        
        #region Clue Discovery
        
        /// <summary>
        /// Discover a clue. Returns true if it was newly discovered.
        /// </summary>
        public bool DiscoverClue(ClueSO clue)
        {
            if (clue == null) return false;
            
            if (foundClueIds.Add(clue.clueNumber))
            {
                OnClueFound?.Invoke(clue);
                OnCluesChanged?.Invoke();
                
                Debug.Log($"[ClueManager] Discovered clue #{clue.clueNumber}: {clue.clueName}");
                return true;
            }
            
            return false; // Already found
        }
        
        /// <summary>
        /// Discover a clue by its number.
        /// </summary>
        public bool DiscoverClue(int clueNumber)
        {
            if (clueLookup.TryGetValue(clueNumber, out var clue))
            {
                return DiscoverClue(clue);
            }
            
            Debug.LogWarning($"[ClueManager] Clue #{clueNumber} not found in lookup");
            return false;
        }
        
        /// <summary>
        /// Check if a clue has been found.
        /// </summary>
        public bool HasClue(int clueNumber)
        {
            return foundClueIds.Contains(clueNumber);
        }
        
        /// <summary>
        /// Check if a clue has been found.
        /// </summary>
        public bool HasClue(ClueSO clue)
        {
            return clue != null && foundClueIds.Contains(clue.clueNumber);
        }
        
        #endregion
        
        #region Clue Retrieval
        
        /// <summary>
        /// Get all found clues.
        /// </summary>
        public List<ClueSO> GetFoundClues()
        {
            return foundClueIds
                .Where(id => clueLookup.ContainsKey(id))
                .Select(id => clueLookup[id])
                .OrderBy(c => c.clueNumber)
                .ToList();
        }
        
        /// <summary>
        /// Get found clues filtered by location.
        /// </summary>
        public List<ClueSO> GetFoundCluesByLocation(string sceneId)
        {
            return GetFoundClues()
                .Where(c => c.locationSceneId == sceneId)
                .ToList();
        }
        
        /// <summary>
        /// Get found clues that relate to a specific question.
        /// </summary>
        public List<ClueSO> GetCluesForQuestion(int questionId)
        {
            return GetFoundClues()
                .Where(c => c.relatedQuestionIds != null && c.relatedQuestionIds.Contains(questionId))
                .ToList();
        }
        
        /// <summary>
        /// Get a clue by its number.
        /// </summary>
        public ClueSO GetClue(int clueNumber)
        {
            return clueLookup.TryGetValue(clueNumber, out var clue) ? clue : null;
        }
        
        /// <summary>
        /// Get all clues (found and unfound) - useful for debug.
        /// </summary>
        public List<ClueSO> GetAllClues()
        {
            return allClues?.ToList() ?? new List<ClueSO>();
        }
        
        #endregion
        
        #region Clue Visibility
        
        /// <summary>
        /// Check if a clue can be seen by the current character.
        /// </summary>
        public bool CanCurrentCharacterSee(ClueSO clue)
        {
            if (clue == null) return false;
            
            var currentCharacter = GameManager.Instance?.CurrentCharacter ?? CharacterType.Scorpion;
            return clue.IsVisibleTo(currentCharacter);
        }
        
        #endregion
        
        #region Save/Load
        
        public List<int> GetFoundClueIdsForSave()
        {
            return foundClueIds.ToList();
        }
        
        public void LoadFoundClues(List<int> clueIds)
        {
            foundClueIds.Clear();
            
            if (clueIds != null)
            {
                foreach (int id in clueIds)
                {
                    foundClueIds.Add(id);
                }
            }
            
            OnCluesChanged?.Invoke();
        }
        
        public void ClearAllClues()
        {
            foundClueIds.Clear();
            OnCluesChanged?.Invoke();
        }
        
        #endregion
    }
}
