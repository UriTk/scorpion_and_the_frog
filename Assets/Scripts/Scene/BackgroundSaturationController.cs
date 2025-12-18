using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PointClickDetective
{
    /// <summary>
    /// Controls background saturation based on active character.
    /// Scorpion = grayscale, Frog = full color.
    /// Automatically finds all SpriteRenderers and MeshRenderers (Quads) in the scene.
    /// </summary>
    public class BackgroundSaturationController : MonoBehaviour
    {
        public static BackgroundSaturationController Instance { get; private set; }
        
        [Header("Blacklist")]
        [Tooltip("GameObjects that should NOT be affected by saturation (UI elements, characters, etc.)")]
        [SerializeField] private List<GameObject> blacklistedObjects = new List<GameObject>();
        
        [Tooltip("Tags that should NOT be affected by saturation (must be defined in Tag Manager)")]
        [SerializeField] private List<string> blacklistedTags = new List<string>();
        
        [Tooltip("Layers that should NOT be affected by saturation")]
        [SerializeField] private LayerMask blacklistedLayers;
        
        [Header("Saturation Settings")]
        [Tooltip("Saturation when Scorpion is active (0 = grayscale, 1 = full color)")]
        [Range(0f, 1f)]
        [SerializeField] private float scorpionSaturation = 0f;
        
        [Tooltip("Saturation when Frog is active")]
        [Range(0f, 1f)]
        [SerializeField] private float frogSaturation = 1f;
        
        [Tooltip("How long the transition takes (should match character switch animation)")]
        [SerializeField] private float transitionDuration = 0.5f;
        
        [Header("Debug")]
        [Tooltip("Log which renderers are found and affected")]
        [SerializeField] private bool debugLogging = false;
        
        // State
        private float currentSaturation = 1f;
        private Coroutine transitionCoroutine;
        private Dictionary<Renderer, Material> materialInstances = new Dictionary<Renderer, Material>();
        private Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();
        
        private Shader grayscaleSpriteShader;
        private Shader grayscaleDefaultShader;
        
        private static readonly int SaturationProperty = Shader.PropertyToID("_Saturation");
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Find/create shaders
            grayscaleSpriteShader = Shader.Find("PointClickDetective/GrayscaleSprite");
            grayscaleDefaultShader = Shader.Find("PointClickDetective/GrayscaleDefault");
            
            if (grayscaleSpriteShader == null)
            {
                Debug.LogError("[BackgroundSaturationController] Could not find GrayscaleSprite shader!");
            }
        }
        
        private void Start()
        {
            // Subscribe to character changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterChanged.AddListener(OnCharacterChanged);
                
                // Set initial saturation based on current character (no transition)
                float targetSat = GameManager.Instance.CurrentCharacter == CharacterType.Scorpion 
                    ? scorpionSaturation 
                    : frogSaturation;
                currentSaturation = targetSat;
            }
            
            // Note: RefreshRenderers() is called by GameSceneContainer.OnEnter() 
            // when scenes become active, so we don't need to call it here.
            // This avoids timing issues with inactive scenes.
        }
        
        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterChanged.RemoveListener(OnCharacterChanged);
            }
            
            // Restore original materials and clean up
            RestoreAllMaterials();
        }
        
        #region Public Methods
        
        /// <summary>
        /// Add a GameObject to the blacklist (won't be affected by saturation).
        /// </summary>
        public void AddToBlacklist(GameObject go)
        {
            if (go != null && !blacklistedObjects.Contains(go))
            {
                blacklistedObjects.Add(go);
                
                // Remove from affected renderers if already set up
                var renderers = go.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    RemoveRenderer(r);
                }
            }
        }
        
        /// <summary>
        /// Remove a GameObject from the blacklist.
        /// </summary>
        public void RemoveFromBlacklist(GameObject go)
        {
            if (go != null)
            {
                blacklistedObjects.Remove(go);
            }
        }
        
        /// <summary>
        /// Manually trigger saturation transition.
        /// </summary>
        public void TransitionToSaturation(float targetSaturation, float duration = -1f)
        {
            if (duration < 0) duration = transitionDuration;
            
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            
            transitionCoroutine = StartCoroutine(TransitionSaturation(targetSaturation, duration));
        }
        
        /// <summary>
        /// Set saturation immediately without transition.
        /// </summary>
        public void SetSaturationImmediate(float saturation)
        {
            currentSaturation = saturation;
            ApplySaturation(saturation);
        }
        
        /// <summary>
        /// Scan scene for all renderers and set them up.
        /// Call this when entering a new scene.
        /// </summary>
        public void RefreshRenderers()
        {
            // Clean up old materials first
            RestoreAllMaterials();
            
            // Determine what saturation we SHOULD be at based on current character
            // This is important because we might be called during a transition
            CharacterType currentCharacter = GameManager.Instance?.CurrentCharacter ?? CharacterType.Scorpion;
            float targetSaturation = currentCharacter == CharacterType.Scorpion 
                ? scorpionSaturation 
                : frogSaturation;
            
            // Set saturation immediately to the correct value for this character
            currentSaturation = targetSaturation;
            
            // Stop any ongoing transition since we're setting to the target immediately
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
            
            int foundSprites = 0;
            int foundQuads = 0;
            int skippedInactive = 0;
            int skippedBlacklisted = 0;
            
            // Find all SpriteRenderers (include inactive to get all, then filter)
            SpriteRenderer[] spriteRenderers = FindObjectsOfType<SpriteRenderer>(true);
            Debug.Log($"[BackgroundSaturationController] Total SpriteRenderers in scene: {spriteRenderers.Length}");
            
            foreach (var sr in spriteRenderers)
            {
                // Only affect if the object is actually visible (active in hierarchy)
                if (!sr.gameObject.activeInHierarchy)
                {
                    skippedInactive++;
                    continue;
                }
                
                if (IsBlacklisted(sr.gameObject))
                {
                    skippedBlacklisted++;
                    continue;
                }
                
                SetupSpriteRenderer(sr);
                foundSprites++;
            }
            
            // Find all MeshRenderers (for Quads)
            MeshRenderer[] meshRenderers = FindObjectsOfType<MeshRenderer>(true);
            foreach (var mr in meshRenderers)
            {
                // Only affect if the object is actually visible (active in hierarchy)
                if (!mr.gameObject.activeInHierarchy)
                {
                    skippedInactive++;
                    continue;
                }
                
                // Check if it's a Quad (has MeshFilter with Quad mesh)
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && 
                    (mf.sharedMesh.name == "Quad" || mf.sharedMesh.name.Contains("Quad")))
                {
                    if (IsBlacklisted(mr.gameObject))
                    {
                        skippedBlacklisted++;
                        continue;
                    }
                    
                    SetupMeshRenderer(mr);
                    foundQuads++;
                }
            }
            
            // Apply current saturation to all
            ApplySaturation(currentSaturation);
            
            Debug.Log($"[BackgroundSaturationController] Setup complete - Sprites: {foundSprites}, Quads: {foundQuads}, Skipped (inactive): {skippedInactive}, Skipped (blacklisted): {skippedBlacklisted}, Current saturation: {currentSaturation}");
        }
        
        #endregion
        
        #region Internal Methods
        
        private bool IsBlacklisted(GameObject go)
        {
            if (go == null) return true;
            
            // Check explicit blacklist
            if (blacklistedObjects.Contains(go))
            {
                if (debugLogging) Debug.Log($"[Saturation] Blacklisted (explicit): {go.name}");
                return true;
            }
            
            // Check parent objects in blacklist
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                if (blacklistedObjects.Contains(parent.gameObject))
                {
                    if (debugLogging) Debug.Log($"[Saturation] Blacklisted (parent): {go.name}");
                    return true;
                }
                parent = parent.parent;
            }
            
            // Check tags using go.tag (doesn't throw if tag doesn't exist)
            string goTag = go.tag;
            foreach (var tag in blacklistedTags)
            {
                if (!string.IsNullOrEmpty(tag) && goTag == tag)
                {
                    if (debugLogging) Debug.Log($"[Saturation] Blacklisted (tag {tag}): {go.name}");
                    return true;
                }
            }
            
            // Check layers
            if (((1 << go.layer) & blacklistedLayers) != 0)
            {
                if (debugLogging) Debug.Log($"[Saturation] Blacklisted (layer): {go.name}");
                return true;
            }
            
            return false;
        }
        
        private void OnCharacterChanged(CharacterType newCharacter)
        {
            float targetSaturation = newCharacter == CharacterType.Scorpion 
                ? scorpionSaturation 
                : frogSaturation;
            
            TransitionToSaturation(targetSaturation);
            
            if (debugLogging)
            {
                Debug.Log($"[BackgroundSaturationController] Transitioning to saturation {targetSaturation} for {newCharacter}");
            }
        }
        
        private void SetupSpriteRenderer(SpriteRenderer sr)
        {
            if (sr == null || grayscaleSpriteShader == null) return;
            if (materialInstances.ContainsKey(sr)) return;
            
            // Store original material
            originalMaterials[sr] = sr.sharedMaterial;
            
            // Create material instance with grayscale shader
            Material mat = new Material(grayscaleSpriteShader);
            
            // Copy properties from original
            if (sr.sharedMaterial != null && sr.sharedMaterial.mainTexture != null)
            {
                mat.mainTexture = sr.sharedMaterial.mainTexture;
            }
            else if (sr.sprite != null)
            {
                mat.mainTexture = sr.sprite.texture;
            }
            
            mat.color = sr.color;
            mat.SetFloat(SaturationProperty, currentSaturation);
            
            sr.material = mat;
            materialInstances[sr] = mat;
            
            if (debugLogging)
            {
                Debug.Log($"[Saturation] Setup SpriteRenderer: {sr.gameObject.name}");
            }
        }
        
        private void SetupMeshRenderer(MeshRenderer mr)
        {
            if (mr == null || grayscaleSpriteShader == null) return;
            if (materialInstances.ContainsKey(mr)) return;
            
            // Store original material
            originalMaterials[mr] = mr.sharedMaterial;
            
            // Create material instance with grayscale shader
            Material mat = new Material(grayscaleSpriteShader);
            
            // Copy texture from original material
            if (mr.sharedMaterial != null && mr.sharedMaterial.mainTexture != null)
            {
                mat.mainTexture = mr.sharedMaterial.mainTexture;
            }
            
            // Copy color if available
            if (mr.sharedMaterial != null && mr.sharedMaterial.HasProperty("_Color"))
            {
                mat.color = mr.sharedMaterial.color;
            }
            
            mat.SetFloat(SaturationProperty, currentSaturation);
            
            mr.material = mat;
            materialInstances[mr] = mat;
            
            if (debugLogging)
            {
                Debug.Log($"[Saturation] Setup MeshRenderer (Quad): {mr.gameObject.name}");
            }
        }
        
        private void RemoveRenderer(Renderer r)
        {
            if (r == null) return;
            
            // Restore original material
            if (originalMaterials.TryGetValue(r, out Material originalMat))
            {
                r.material = originalMat;
                originalMaterials.Remove(r);
            }
            
            // Destroy our material instance
            if (materialInstances.TryGetValue(r, out Material mat))
            {
                Destroy(mat);
                materialInstances.Remove(r);
            }
        }
        
        private void RestoreAllMaterials()
        {
            // Create a list of keys to process (avoid modifying dictionary during iteration)
            var renderersToRestore = new List<Renderer>(originalMaterials.Keys);
            int restoredCount = 0;
            
            foreach (var renderer in renderersToRestore)
            {
                // Skip if renderer was destroyed
                if (renderer == null) continue;
                
                // Only restore if the renderer is still active in hierarchy
                // This prevents us from messing with materials on inactive scenes
                if (!renderer.gameObject.activeInHierarchy)
                {
                    // Remove from tracking but don't restore - let it keep the grayscale shader
                    // It will get properly set up again when its scene becomes active
                    originalMaterials.Remove(renderer);
                    if (materialInstances.TryGetValue(renderer, out Material mat))
                    {
                        // Don't destroy the material - renderer is still using it
                        materialInstances.Remove(renderer);
                    }
                    continue;
                }
                
                // Restore original material if we have it
                if (originalMaterials.TryGetValue(renderer, out Material originalMat) && originalMat != null)
                {
                    renderer.material = originalMat;
                    restoredCount++;
                }
            }
            originalMaterials.Clear();
            
            // Destroy material instances only for active renderers we restored
            foreach (var mat in materialInstances.Values)
            {
                if (mat != null)
                {
                    Destroy(mat);
                }
            }
            materialInstances.Clear();
            
            Debug.Log($"[BackgroundSaturationController] Restored {restoredCount} materials (skipped inactive)");
        }
        
        private void ApplySaturation(float saturation)
        {
            foreach (var kvp in materialInstances)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Value.SetFloat(SaturationProperty, saturation);
                }
            }
        }
        
        private IEnumerator TransitionSaturation(float targetSaturation, float duration)
        {
            float startSaturation = currentSaturation;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Smooth step for nicer transition
                t = t * t * (3f - 2f * t);
                
                currentSaturation = Mathf.Lerp(startSaturation, targetSaturation, t);
                ApplySaturation(currentSaturation);
                
                yield return null;
            }
            
            currentSaturation = targetSaturation;
            ApplySaturation(targetSaturation);
            
            transitionCoroutine = null;
        }
        
        #endregion
    }
}