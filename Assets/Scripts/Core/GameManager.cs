using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace PointClickDetective
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        
        [Header("Current State")]
        [SerializeField] private CharacterType currentCharacter = CharacterType.Scorpion;
        [SerializeField] private string currentSceneId = "office";
        
        [Header("Character Switching")]
        [Tooltip("If false, character switching is completely disabled (for final scenes, etc.)")]
        [SerializeField] private bool allowCharacterSwitching = true;
        
        [Tooltip("If set, forces this character and disables switching")]
        [SerializeField] private bool forceLockedCharacter = false;
        [SerializeField] private CharacterType lockedCharacter = CharacterType.Scorpion;
        
        [Header("Character Portraits (Default)")]
        public Sprite scorpionDefaultPortrait;
        public Sprite frogDefaultPortrait;
        
        [Header("Character Location Restrictions")]
        [Tooltip("Define scenes where characters are locked to specific locations")]
        [SerializeField] private CharacterLocationRule[] locationRules;
        
        // Events
        public UnityEvent<CharacterType> OnCharacterChanged;
        public UnityEvent<string> OnSceneChanged;
        public UnityEvent<string> OnFlagSet;
        public UnityEvent OnCharacterSwitchBlocked;
        
        // Game State
        private HashSet<string> gameFlags = new HashSet<string>();
        private HashSet<string> lookedAtObjects = new HashSet<string>();
        private HashSet<string> interactedObjects = new HashSet<string>();
        
        // Location locking
        private Dictionary<string, CharacterLocationRule> locationRuleLookup = new Dictionary<string, CharacterLocationRule>();
        
        public CharacterType CurrentCharacter => currentCharacter;
        public string CurrentSceneId => currentSceneId;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            OnCharacterChanged ??= new UnityEvent<CharacterType>();
            OnSceneChanged ??= new UnityEvent<string>();
            OnFlagSet ??= new UnityEvent<string>();
            OnCharacterSwitchBlocked ??= new UnityEvent();
            
            // Build location rule lookup
            if (locationRules != null)
            {
                foreach (var rule in locationRules)
                {
                    if (rule != null && !string.IsNullOrEmpty(rule.triggerSceneId))
                    {
                        locationRuleLookup[rule.triggerSceneId] = rule;
                    }
                }
            }
        }
        
        #region Character Switching
        
        /// <summary>
        /// Check if character switching is currently allowed.
        /// </summary>
        public bool CanSwitchCharacter()
        {
            // Check global lock
            if (!allowCharacterSwitching || forceLockedCharacter)
            {
                return false;
            }
            
            // Check if current scene has a location rule active
            var rule = GetActiveLocationRule();
            if (rule != null && rule.lockCharacterSwitch)
            {
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if a specific character can be in the current scene.
        /// </summary>
        public bool CanCharacterBeInScene(CharacterType character, string sceneId)
        {
            var rule = GetLocationRule(sceneId);
            if (rule == null) return true;
            
            if (!rule.IsRuleActive(gameFlags)) return true;
            
            if (character == CharacterType.Scorpion)
                return rule.scorpionSceneId == sceneId;
            else
                return rule.frogSceneId == sceneId;
        }
        
        public void SwitchCharacter()
        {
            if (!CanSwitchCharacter())
            {
                Debug.Log("[GameManager] Character switch blocked by location rule");
                OnCharacterSwitchBlocked?.Invoke();
                return;
            }
            
            CharacterType newCharacter = currentCharacter == CharacterType.Scorpion 
                ? CharacterType.Frog 
                : CharacterType.Scorpion;
            
            SetCharacter(newCharacter);
        }
        
        public void SetCharacter(CharacterType character)
        {
            if (currentCharacter == character) return;
            
            currentCharacter = character;
            
            // Check if we need to change scenes due to location rule
            var rule = GetActiveLocationRule();
            if (rule != null)
            {
                string targetScene = character == CharacterType.Scorpion 
                    ? rule.scorpionSceneId 
                    : rule.frogSceneId;
                
                if (targetScene != currentSceneId)
                {
                    currentSceneId = targetScene;
                    OnSceneChanged?.Invoke(currentSceneId);
                    GameSceneManager.Instance?.LoadScene(currentSceneId);
                }
            }
            
            OnCharacterChanged?.Invoke(currentCharacter);
            Debug.Log($"[GameManager] Switched to {currentCharacter}");
        }
        
        /// <summary>
        /// Enable or disable character switching globally.
        /// </summary>
        public void SetCharacterSwitchingEnabled(bool enabled)
        {
            allowCharacterSwitching = enabled;
            Debug.Log($"[GameManager] Character switching {(enabled ? "enabled" : "disabled")}");
        }
        
        /// <summary>
        /// Lock to a specific character (disables switching and forces that character).
        /// Used for final scenes, cutscenes, etc.
        /// </summary>
        public void LockToCharacter(CharacterType character)
        {
            forceLockedCharacter = true;
            lockedCharacter = character;
            
            if (currentCharacter != character)
            {
                currentCharacter = character;
                OnCharacterChanged?.Invoke(currentCharacter);
            }
            
            Debug.Log($"[GameManager] Locked to character: {character}");
        }
        
        /// <summary>
        /// Unlock character switching (removes forced character lock).
        /// </summary>
        public void UnlockCharacter()
        {
            forceLockedCharacter = false;
            Debug.Log("[GameManager] Character lock removed");
        }
        
        /// <summary>
        /// Check if currently locked to a specific character.
        /// </summary>
        public bool IsCharacterLocked => forceLockedCharacter;
        
        #endregion
        
        #region Location Rules
        
        private CharacterLocationRule GetActiveLocationRule()
        {
            foreach (var rule in locationRules)
            {
                if (rule == null) continue;
                
                if (rule.IsRuleActive(gameFlags))
                {
                    // Check if we're in either of the rule's scenes
                    if (currentSceneId == rule.scorpionSceneId || 
                        currentSceneId == rule.frogSceneId ||
                        currentSceneId == rule.triggerSceneId)
                    {
                        return rule;
                    }
                }
            }
            
            return null;
        }
        
        private CharacterLocationRule GetLocationRule(string sceneId)
        {
            foreach (var rule in locationRules)
            {
                if (rule == null) continue;
                
                if (rule.scorpionSceneId == sceneId || 
                    rule.frogSceneId == sceneId ||
                    rule.triggerSceneId == sceneId)
                {
                    return rule;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Get the scene a character should be in given a location rule context.
        /// </summary>
        public string GetCharacterScene(CharacterType character)
        {
            var rule = GetActiveLocationRule();
            if (rule != null)
            {
                return character == CharacterType.Scorpion 
                    ? rule.scorpionSceneId 
                    : rule.frogSceneId;
            }
            
            return currentSceneId;
        }
        
        #endregion
        
        #region Scene Management
        
        public void ChangeScene(string newSceneId)
        {
            if (string.IsNullOrEmpty(newSceneId)) return;
            
            // Check if character can go to this scene
            if (!CanCharacterBeInScene(currentCharacter, newSceneId))
            {
                Debug.Log($"[GameManager] {currentCharacter} cannot go to {newSceneId}");
                return;
            }
            
            string previousSceneId = currentSceneId;
            currentSceneId = newSceneId;
            
            OnSceneChanged?.Invoke(currentSceneId);
            GameSceneManager.Instance?.LoadScene(newSceneId);
            
            Debug.Log($"[GameManager] Changed scene from {previousSceneId} to {newSceneId}");
        }
        
        /// <summary>
        /// Set the current scene ID directly without firing events or triggering scene load.
        /// Used by GameSceneManager during initial setup and transitions to sync state.
        /// </summary>
        public void SetSceneIdDirect(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId)) return;
            
            string previousSceneId = currentSceneId;
            currentSceneId = sceneId;
            
            Debug.Log($"[GameManager] Scene ID set directly to: {sceneId}");
            
            // Fire event so Interactables update their visibility
            if (previousSceneId != currentSceneId)
            {
                OnSceneChanged?.Invoke(currentSceneId);
            }
        }
        
        #endregion
        
        #region Flags & State
        
        public void SetFlag(string flagName)
        {
            if (string.IsNullOrEmpty(flagName)) return;
            
            if (gameFlags.Add(flagName))
            {
                OnFlagSet?.Invoke(flagName);
                Debug.Log($"[GameManager] Flag set: {flagName}");
            }
        }
        
        public bool HasFlag(string flagName)
        {
            bool has = gameFlags.Contains(flagName);
            Debug.Log($"[GameManager] HasFlag('{flagName}'): {has}");
            return has;
        }
        
        public void RemoveFlag(string flagName)
        {
            gameFlags.Remove(flagName);
        }
        
        /// <summary>
        /// Alias for RemoveFlag.
        /// </summary>
        public void ClearFlag(string flagName)
        {
            RemoveFlag(flagName);
        }
        
        public void MarkAsLookedAt(string objectId)
        {
            string key = $"{currentCharacter}_{objectId}";
            lookedAtObjects.Add(key);
        }
        
        public bool HasLookedAt(string objectId)
        {
            string key = $"{currentCharacter}_{objectId}";
            return lookedAtObjects.Contains(key);
        }
        
        public void MarkAsInteracted(string objectId)
        {
            string key = $"{currentCharacter}_{objectId}";
            interactedObjects.Add(key);
        }
        
        public bool HasInteracted(string objectId)
        {
            string key = $"{currentCharacter}_{objectId}";
            return interactedObjects.Contains(key);
        }
        
        #endregion
        
        #region Portraits
        
        public Sprite GetDefaultPortrait(CharacterType character)
        {
            return character == CharacterType.Scorpion ? scorpionDefaultPortrait : frogDefaultPortrait;
        }
        
        public Sprite GetCurrentCharacterPortrait()
        {
            return GetDefaultPortrait(currentCharacter);
        }
        
        #endregion
        
        #region Save/Load
        
        [System.Serializable]
        private class SaveData
        {
            public CharacterType character;
            public string sceneId;
            public List<string> flags;
            public List<string> lookedAt;
            public List<string> interacted;
            public List<int> foundClueIds;
        }
        
        public string GetSaveData()
        {
            SaveData data = new SaveData
            {
                character = currentCharacter,
                sceneId = currentSceneId,
                flags = new List<string>(gameFlags),
                lookedAt = new List<string>(lookedAtObjects),
                interacted = new List<string>(interactedObjects),
                foundClueIds = ClueManager.Instance?.GetFoundClueIdsForSave() ?? new List<int>()
            };
            
            return JsonUtility.ToJson(data);
        }
        
        public void LoadSaveData(string json)
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            
            currentCharacter = data.character;
            currentSceneId = data.sceneId;
            gameFlags = new HashSet<string>(data.flags);
            lookedAtObjects = new HashSet<string>(data.lookedAt);
            interactedObjects = new HashSet<string>(data.interacted);
            
            ClueManager.Instance?.LoadFoundClues(data.foundClueIds);
            
            OnCharacterChanged?.Invoke(currentCharacter);
            OnSceneChanged?.Invoke(currentSceneId);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Defines a rule for character location restrictions.
    /// Example: During cafe investigation, Scorpion is outside, Frog is inside.
    /// </summary>
    [System.Serializable]
    public class CharacterLocationRule
    {
        [Tooltip("Descriptive name for this rule")]
        public string ruleName;
        
        [Tooltip("Scene that triggers this rule (e.g., entering the cafe area)")]
        public string triggerSceneId;
        
        [Tooltip("Where Scorpion must be during this rule")]
        public string scorpionSceneId;
        
        [Tooltip("Where Frog must be during this rule")]
        public string frogSceneId;
        
        [Tooltip("If true, characters cannot switch while this rule is active")]
        public bool lockCharacterSwitch;
        
        [Tooltip("Flag that must be set for this rule to be active")]
        public string requiredFlag;
        
        [Tooltip("Flag that disables this rule when set")]
        public string disablingFlag;
        
        /// <summary>
        /// Check if this rule is currently active.
        /// </summary>
        public bool IsRuleActive(HashSet<string> currentFlags)
        {
            // Check required flag
            if (!string.IsNullOrEmpty(requiredFlag))
            {
                if (!currentFlags.Contains(requiredFlag))
                    return false;
            }
            
            // Check disabling flag
            if (!string.IsNullOrEmpty(disablingFlag))
            {
                if (currentFlags.Contains(disablingFlag))
                    return false;
            }
            
            return true;
        }
    }
}