using UnityEngine;
using UnityEngine.Events;

namespace PointClickDetective
{
    [RequireComponent(typeof(Collider2D))]
    public class Interactable : MonoBehaviour
    {
        [Header("Identification")]
        [SerializeField] private string objectId;
        [SerializeField] private string displayName;
        
        [Header("Character-Specific Data")]
        [SerializeField] private CharacterInteractionData scorpionData;
        [SerializeField] private CharacterInteractionData frogData;
        
        [Header("Scene Binding")]
        [SerializeField] private string belongsToSceneId;
        
        [Header("Events")]
        public UnityEvent OnLookedAt;
        public UnityEvent OnInteracted;
        public UnityEvent<ClueSO> OnClueDiscovered;
        
        // State
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private bool isCurrentlyVisible = true;
        
        public string ObjectId => objectId;
        public string DisplayName => displayName;
        public string SceneId => belongsToSceneId;
        public bool IsVisible => isCurrentlyVisible;
        
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
            
            // Generate ID if empty
            if (string.IsNullOrEmpty(objectId))
            {
                objectId = $"{gameObject.name}_{GetInstanceID()}";
            }
            
            // Initialize data if null (ensures visibility defaults work)
            if (scorpionData == null)
            {
                scorpionData = new CharacterInteractionData();
            }
            if (frogData == null)
            {
                frogData = new CharacterInteractionData();
            }
            
            // Set correct character types
            scorpionData.character = CharacterType.Scorpion;
            frogData.character = CharacterType.Frog;
            
            OnClueDiscovered ??= new UnityEvent<ClueSO>();
        }
        
        private void Start()
        {
            // Subscribe to character changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterChanged.AddListener(OnCharacterChanged);
                GameManager.Instance.OnSceneChanged.AddListener(OnSceneChanged);
            }
            
            // Delay visibility check to ensure GameSceneManager has set up the scene
            StartCoroutine(DelayedVisibilityUpdate());
        }
        
        private System.Collections.IEnumerator DelayedVisibilityUpdate()
        {
            // Wait one frame to ensure all Start() methods have run
            yield return null;
            UpdateVisibility();
            
            // Try to register again in case InteractionManager wasn't ready in OnEnable
            InteractionManager.Instance?.RegisterInteractable(this);
        }
        
        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterChanged.RemoveListener(OnCharacterChanged);
                GameManager.Instance.OnSceneChanged.RemoveListener(OnSceneChanged);
            }
        }
        
        #region Character & Scene Visibility
        
        private void OnCharacterChanged(CharacterType newCharacter)
        {
            UpdateVisibility();
        }
        
        private void OnSceneChanged(string newSceneId)
        {
            UpdateVisibility();
        }
        
        private void UpdateVisibility()
        {
            bool isVisibleToCharacter = IsVisibleToCurrentCharacter();
            bool isInScene = IsInCurrentScene();
            bool shouldBeVisible = isVisibleToCharacter && isInScene;
            
            // Always log for debugging
            Debug.Log($"[Interactable] '{displayName}' UpdateVisibility: shouldBeVisible={shouldBeVisible} (visibleToCharacter={isVisibleToCharacter}, inScene={isInScene}, currentScene={GameManager.Instance?.CurrentSceneId}, belongsTo={belongsToSceneId})");
            
            isCurrentlyVisible = shouldBeVisible;
            
            // Instead of disabling the entire GameObject (which breaks event listeners),
            // we disable the visual and interaction components
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = shouldBeVisible;
            }
            
            // Disable all colliders so it can't be clicked
            var colliders = GetComponents<Collider2D>();
            foreach (var col in colliders)
            {
                col.enabled = shouldBeVisible;
            }
            
            // Also hide any child renderers
            var childRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in childRenderers)
            {
                renderer.enabled = shouldBeVisible;
            }
        }
        
        public bool IsVisibleToCurrentCharacter()
        {
            var data = GetCurrentCharacterData();
            
            // If data is null, default to visible (don't hide things accidentally)
            if (data == null)
            {
                Debug.LogWarning($"[Interactable] '{displayName}' has null character data for current character, defaulting to visible");
                return true;
            }
            
            Debug.Log($"[Interactable] '{displayName}' IsVisibleToCurrentCharacter: data.isVisibleToThisCharacter={data.isVisibleToThisCharacter}, character={GameManager.Instance?.CurrentCharacter}");
            return data.isVisibleToThisCharacter;
        }
        
        public bool IsInCurrentScene()
        {
            // If no scene binding, always visible (scene-wise)
            if (string.IsNullOrEmpty(belongsToSceneId))
            {
                Debug.Log($"[Interactable] '{displayName}' IsInCurrentScene: no scene binding, returning true");
                return true;
            }
            
            // If GameManager not ready yet, assume visible (will update on scene change event)
            if (GameManager.Instance == null)
            {
                Debug.Log($"[Interactable] '{displayName}' IsInCurrentScene: GameManager null, returning true");
                return true;
            }
            
            bool inScene = GameManager.Instance.CurrentSceneId == belongsToSceneId;
            Debug.Log($"[Interactable] '{displayName}' IsInCurrentScene: current={GameManager.Instance.CurrentSceneId}, belongsTo={belongsToSceneId}, result={inScene}");
            return inScene;
        }
        
        public CharacterInteractionData GetCurrentCharacterData()
        {
            CharacterType character = GameManager.Instance?.CurrentCharacter ?? CharacterType.Scorpion;
            var data = character == CharacterType.Scorpion ? scorpionData : frogData;
            Debug.Log($"[Interactable] '{displayName}' GetCurrentCharacterData: character={character}, data null?={data == null}");
            return data;
        }
        
        #endregion
        
        #region Prerequisites
        
        public bool MeetsPrerequisites(out string lockedLookDialogue, out string lockedInteractDialogue)
        {
            lockedLookDialogue = null;
            lockedInteractDialogue = null;
            
            var data = GetCurrentCharacterData();
            if (data?.prerequisites == null || data.prerequisites.Length == 0)
            {
                return true;
            }
            
            foreach (var prereq in data.prerequisites)
            {
                bool met = CheckPrerequisite(prereq);
                if (!met)
                {
                    lockedLookDialogue = prereq.lockedLookDialogue;
                    lockedInteractDialogue = prereq.lockedInteractDialogue;
                    return false;
                }
            }
            
            return true;
        }
        
        private bool CheckPrerequisite(InteractionPrerequisite prereq)
        {
            switch (prereq.type)
            {
                case PrerequisiteType.RequiresFlag:
                    return GameManager.Instance?.HasFlag(prereq.flagName) ?? false;
                    
                case PrerequisiteType.RequiresClue:
                    return ClueManager.Instance?.HasClue(prereq.requiredClue) ?? false;
                    
                case PrerequisiteType.RequiresLookedAt:
                    return GameManager.Instance?.HasLookedAt(prereq.flagName) ?? false;
                    
                case PrerequisiteType.RequiresInteracted:
                    return GameManager.Instance?.HasInteracted(prereq.flagName) ?? false;
                    
                default:
                    return true;
            }
        }
        
        #endregion
        
        #region Interactions
        
        public InteractionResult LookAt()
        {
            var data = GetCurrentCharacterData();
            if (data == null) return new InteractionResult { success = false };
            
            string dialogue;
            Sprite portrait = data.lookAtPortrait ?? GameManager.Instance?.GetCurrentCharacterPortrait();
            ClueSO discoveredClue = null;
            
            // Check prerequisites
            if (!MeetsPrerequisites(out string lockedDialogue, out _))
            {
                dialogue = !string.IsNullOrEmpty(lockedDialogue) 
                    ? lockedDialogue 
                    : "I can't get a good look at that right now.";
                    
                return new InteractionResult
                {
                    success = false,
                    dialogue = dialogue,
                    portrait = portrait,
                    isLocked = true
                };
            }
            
            // Mark as looked at
            GameManager.Instance?.MarkAsLookedAt(objectId);
            
            // Set unlock flag if configured
            if (!string.IsNullOrEmpty(data.unlockFlagOnLook))
            {
                GameManager.Instance?.SetFlag(data.unlockFlagOnLook);
            }
            
            // Check for conditional dialogues first
            var conditional = data.GetMatchingConditionalDialogue(objectId);
            Debug.Log($"[Interactable] LookAt '{objectId}': conditional={conditional}, dialogue={conditional?.dialogue}");
            
            if (conditional != null && conditional.dialogue != null)
            {
                Debug.Log($"[Interactable] Using conditional dialogue: {conditional.dialogue.name}");
                
                // Execute after effects (set flag, discover clue, playOnce)
                conditional.ExecuteAfterEffects(objectId);
                
                OnLookedAt?.Invoke();
                
                return new InteractionResult
                {
                    success = true,
                    dialogueSequence = conditional.dialogue,
                    conditionalDialogue = conditional,
                    discoveredClue = conditional.discoverClueAfter
                };
            }
            
            Debug.Log($"[Interactable] No conditional matched, checking fallback sequence");
            
            // Check if using fallback dialogue sequence
            if (data.HasLookSequence)
            {
                // Sequence handles its own clues/flags
                OnLookedAt?.Invoke();
                
                return new InteractionResult
                {
                    success = true,
                    dialogueSequence = data.lookAtSequence
                };
            }
            
            // Fallback to simple dialogue mode
            dialogue = data.lookAtDialogue;
            
            // Discover clue if configured
            if (data.clueOnLook != null)
            {
                bool isNew = ClueManager.Instance?.DiscoverClue(data.clueOnLook) ?? false;
                if (isNew)
                {
                    discoveredClue = data.clueOnLook;
                    OnClueDiscovered?.Invoke(discoveredClue);
                }
            }
            
            OnLookedAt?.Invoke();
            
            return new InteractionResult
            {
                success = true,
                dialogue = dialogue,
                portrait = portrait,
                discoveredClue = discoveredClue,
                triggeredSceneChange = data.triggersSceneChange,
                targetSceneId = data.targetSceneId
            };
        }
        
        
        #endregion
        
        #region Highlighting
        
        public void SetHighlight(bool highlighted)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = highlighted 
                    ? new Color(originalColor.r + 0.2f, originalColor.g + 0.2f, originalColor.b + 0.2f, originalColor.a)
                    : originalColor;
            }
        }
        
        #endregion
        
        #region Registration
        
        private void OnEnable()
        {
            // Try to register immediately
            TryRegister();
        }
        
        private void OnDisable()
        {
            SetHighlight(false);
            InteractionManager.Instance?.UnregisterInteractable(this);
        }
        
        private void TryRegister()
        {
            if (InteractionManager.Instance != null)
            {
                InteractionManager.Instance.RegisterInteractable(this);
            }
        }
        
        /// <summary>
        /// Called by InteractionManager when mouse enters this object's collider.
        /// </summary>
        public void NotifyMouseEnter()
        {
            SetHighlight(true);
        }
        
        /// <summary>
        /// Called by InteractionManager when mouse exits this object's collider.
        /// </summary>
        public void NotifyMouseExit()
        {
            SetHighlight(false);
        }
        
        /// <summary>
        /// Get this object's collider for raycast detection.
        /// </summary>
        public Collider2D GetCollider()
        {
            return GetComponent<Collider2D>();
        }
        
        #endregion
        
        #region Editor Helpers
        
        private void OnValidate()
        {
            if (scorpionData != null) scorpionData.character = CharacterType.Scorpion;
            if (frogData != null) frogData.character = CharacterType.Frog;
        }
        
        #endregion
    }
}