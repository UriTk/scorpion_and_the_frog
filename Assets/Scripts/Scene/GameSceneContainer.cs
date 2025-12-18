using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace PointClickDetective
{
    /// <summary>
    /// Attach this to a parent GameObject that contains all objects for one "scene".
    /// The GameSceneManager will enable/disable these containers to switch between scenes.
    /// </summary>
    public class GameSceneContainer : MonoBehaviour
    {
        [Header("Scene Info")]
        [SerializeField] private string sceneId;
        [SerializeField] private string displayName;
        [TextArea(2, 4)]
        [SerializeField] private string description;
        
        [Header("World Map Settings")]
        [Tooltip("Icon shown on the world map for this location")]
        [SerializeField] private Sprite mapIcon;
        [Tooltip("If true, this scene starts locked and must be unlocked via flag")]
        [SerializeField] private bool startsLocked = false;
        [Tooltip("The flag name that unlocks this scene (leave empty if not locked)")]
        [SerializeField] private string unlockFlagName;
        
        [Header("Background Scaling")]
        [Tooltip("If true, automatically scales background and effect to fill camera view")]
        [SerializeField] private bool stretchToFillCamera = true;
        
        [Tooltip("SpriteRenderer for the background image (will be scaled to camera)")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        
        [Header("Music")]
        [Tooltip("Scorpion's intro music (plays once before loop)")]
        [SerializeField] private AudioClip scorpionMusicIntro;
        [Tooltip("Scorpion's looping music")]
        [SerializeField] private AudioClip scorpionMusicLoop;
        
        [Tooltip("Frog's intro music (plays once before loop)")]
        [SerializeField] private AudioClip frogMusicIntro;
        [Tooltip("Frog's looping music")]
        [SerializeField] private AudioClip frogMusicLoop;
        
        [Tooltip("If true, music syncs to current playback time when entering scene (for scene variations). If false, music starts from beginning (for completely different songs).")]
        [SerializeField] private bool syncMusicOnEnter = true;
        
        [Header("Scene Effect (Sprite Sheet Animation)")]
        [Tooltip("SpriteSheetAnimator for scene effect like snow/rain. Loops while scene is active.")]
        [SerializeField] private SpriteSheetAnimator sceneEffectAnimator;
        
        [Tooltip("RawImage for UI Canvas display (the animator will control this)")]
        [SerializeField] private RawImage sceneEffectDisplay;
        
        [Tooltip("SpriteRenderer for world-space display (the animator will control this)")]
        [SerializeField] private SpriteRenderer sceneEffectRenderer;
        
        [Tooltip("If true, the effect fades in/out. If false, it appears/disappears instantly.")]
        [SerializeField] private bool fadeEffect = true;
        
        [SerializeField] private float effectFadeDuration = 0.5f;
        
        [Header("Events")]
        public UnityEvent OnSceneEnter;
        public UnityEvent OnSceneExit;
        
        // Properties
        public string SceneId => sceneId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite MapIcon => mapIcon;
        public bool StartsLocked => startsLocked;
        public string UnlockFlagName => unlockFlagName;
        public AudioClip ScorpionMusicIntro => scorpionMusicIntro;
        public AudioClip ScorpionMusicLoop => scorpionMusicLoop;
        public AudioClip FrogMusicIntro => frogMusicIntro;
        public AudioClip FrogMusicLoop => frogMusicLoop;
        public bool SyncMusicOnEnter => syncMusicOnEnter;
        public bool HasMusic => scorpionMusicLoop != null || frogMusicLoop != null;
        public bool HasSceneEffect => sceneEffectAnimator != null;
        
        // Cached
        private Coroutine effectFadeCoroutine;
        private bool effectSetup = false;
        
        private void Awake()
        {
            OnSceneEnter ??= new UnityEvent();
            OnSceneExit ??= new UnityEvent();
            
            // Setup animator for effect
            SetupEffectAnimator();
        }
        
        private void OnEnable()
        {
            // Ensure setup runs if Awake was called while inactive
            SetupEffectAnimator();
        }
        
        private void SetupEffectAnimator()
        {
            if (sceneEffectAnimator == null) return;
            if (effectSetup) return;
            
            effectSetup = true;
            
            // Hide displays initially
            if (sceneEffectDisplay != null)
            {
                sceneEffectDisplay.gameObject.SetActive(false);
            }
            
            if (sceneEffectRenderer != null)
            {
                sceneEffectRenderer.gameObject.SetActive(false);
            }
            
            // Prepare the animator
            sceneEffectAnimator.Prepare();
            sceneEffectAnimator.IsLooping = true;
            
            Debug.Log($"[GameSceneContainer] Sprite sheet animator setup complete for {sceneId}");
        }
        
        private void OnDestroy()
        {
            // No render texture to clean up with sprite sheet approach
        }
        
        /// <summary>
        /// Check if this scene is currently unlocked.
        /// </summary>
        public bool IsUnlocked()
        {
            if (!startsLocked) return true;
            if (string.IsNullOrEmpty(unlockFlagName)) return true;
            
            return GameManager.Instance?.HasFlag(unlockFlagName) ?? false;
        }
        
        /// <summary>
        /// Called by GameSceneManager when entering this scene.
        /// </summary>
        public void OnEnter()
        {
            // Set flag for visiting this scene (first time only)
            if (GameManager.Instance != null && !string.IsNullOrEmpty(sceneId))
            {
                string visitedFlag = $"visited_{sceneId}";
                if (!GameManager.Instance.HasFlag(visitedFlag))
                {
                    GameManager.Instance.SetFlag(visitedFlag);
                    Debug.Log($"[GameSceneContainer] First visit to scene: {sceneId}");
                }
            }
            
            // Scale background and effects to fill camera
            if (stretchToFillCamera)
            {
                ScaleToFillCamera();
            }
            
            // Update music if tracks are assigned
            if (HasMusic && MusicManager.Instance != null)
            {
                MusicManager.Instance.SetSceneTracksWithIntro(
                    scorpionMusicIntro, scorpionMusicLoop,
                    frogMusicIntro, frogMusicLoop,
                    crossfade: true,
                    syncPosition: syncMusicOnEnter
                );
            }
            
            // Start scene effect
            StartSceneEffect();
            
            // Refresh saturation controller for new scene
            if (BackgroundSaturationController.Instance != null)
            {
                BackgroundSaturationController.Instance.RefreshRenderers();
            }
            
            OnSceneEnter?.Invoke();
            Debug.Log($"[GameSceneContainer] Entered scene: {sceneId}");
        }
        
        /// <summary>
        /// Called by GameSceneManager when exiting this scene.
        /// </summary>
        public void OnExit()
        {
            // Stop scene effect
            StopSceneEffect();
            
            OnSceneExit?.Invoke();
            Debug.Log($"[GameSceneContainer] Exited scene: {sceneId}");
        }
        
        /// <summary>
        /// Scales background sprite and effect quad to fill the camera view.
        /// </summary>
        private void ScaleToFillCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            
            // Calculate camera world bounds
            float cameraHeight = cam.orthographicSize * 2f;
            float cameraWidth = cameraHeight * cam.aspect;
            Vector3 cameraCenter = cam.transform.position;
            cameraCenter.z = 0; // For 2D
            
            // Scale background SpriteRenderer
            if (backgroundRenderer != null && backgroundRenderer.sprite != null)
            {
                ScaleSpriteRendererToFill(backgroundRenderer, cameraWidth, cameraHeight, cameraCenter);
            }
            
            // Scale effect Quad/Renderer
            if (sceneEffectRenderer != null)
            {
                ScaleRendererToFill(sceneEffectRenderer, cameraWidth, cameraHeight, cameraCenter);
            }
        }
        
        private void ScaleSpriteRendererToFill(SpriteRenderer sr, float targetWidth, float targetHeight, Vector3 center)
        {
            Sprite sprite = sr.sprite;
            if (sprite == null) return;
            
            // Reset scale to 1 first to get accurate sprite bounds
            sr.transform.localScale = Vector3.one;
            
            // Get sprite size in world units (pixels per unit is already factored into bounds)
            float spriteWidth = sprite.bounds.size.x;
            float spriteHeight = sprite.bounds.size.y;
            
            // Calculate scale to fill (cover entire camera, may crop)
            float scaleX = targetWidth / spriteWidth;
            float scaleY = targetHeight / spriteHeight;
            float scale = Mathf.Max(scaleX, scaleY); // Use max to ensure full coverage
            
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            sr.transform.position = new Vector3(center.x, center.y, sr.transform.position.z);
            
            Debug.Log($"[GameSceneContainer] Scaled background '{sr.gameObject.name}' to {scale:F2}x (sprite: {spriteWidth:F2}x{spriteHeight:F2}, target: {targetWidth:F2}x{targetHeight:F2})");
        }
        
        private void ScaleRendererToFill(Renderer r, float targetWidth, float targetHeight, Vector3 center)
        {
            // For a Quad, default size is 1x1 units
            // Scale it to match camera size
            r.transform.localScale = new Vector3(targetWidth, targetHeight, 1f);
            r.transform.position = new Vector3(center.x, center.y, r.transform.position.z);
            
            Debug.Log($"[GameSceneContainer] Scaled effect renderer to {targetWidth:F2}x{targetHeight:F2}");
        }
        
        /// <summary>
        /// Show or hide this scene container.
        /// </summary>
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        #region Scene Effects
        
        private void StartSceneEffect()
        {
            if (sceneEffectAnimator == null)
            {
                Debug.Log($"[GameSceneContainer] No animator configured for {sceneId}");
                return;
            }
            
            if (sceneEffectDisplay == null && sceneEffectRenderer == null)
            {
                Debug.Log($"[GameSceneContainer] No display (RawImage or SpriteRenderer) configured for {sceneId}");
                return;
            }
            
            // Ensure setup has run
            SetupEffectAnimator();
            
            if (effectFadeCoroutine != null)
            {
                StopCoroutine(effectFadeCoroutine);
            }
            
            // Show the display
            if (sceneEffectDisplay != null)
            {
                sceneEffectDisplay.gameObject.SetActive(true);
            }
            if (sceneEffectRenderer != null)
            {
                sceneEffectRenderer.gameObject.SetActive(true);
            }
            
            // Play the animation
            PlayEffect();
            
            // Fade in
            if (fadeEffect)
            {
                effectFadeCoroutine = StartCoroutine(FadeEffectIn());
            }
            else
            {
                SetEffectAlpha(1f);
            }
        }
        
        private void PlayEffect()
        {
            if (sceneEffectAnimator == null) return;
            
            sceneEffectAnimator.SetFrame(0);
            sceneEffectAnimator.IsLooping = true;
            sceneEffectAnimator.Play();
            
            Debug.Log($"[GameSceneContainer] Playing scene effect for {sceneId} - isPlaying: {sceneEffectAnimator.IsPlaying}");
        }
        
        private void StopSceneEffect()
        {
            if (sceneEffectAnimator == null) return;
            if (sceneEffectDisplay == null && sceneEffectRenderer == null) return;
            
            if (effectFadeCoroutine != null)
            {
                StopCoroutine(effectFadeCoroutine);
            }
            
            // Fade out
            if (fadeEffect && gameObject.activeInHierarchy)
            {
                effectFadeCoroutine = StartCoroutine(FadeEffectOut());
            }
            else
            {
                sceneEffectAnimator.Stop();
                HideEffectDisplay();
            }
        }
        
        private void HideEffectDisplay()
        {
            if (sceneEffectDisplay != null)
            {
                sceneEffectDisplay.gameObject.SetActive(false);
            }
            if (sceneEffectRenderer != null)
            {
                sceneEffectRenderer.gameObject.SetActive(false);
            }
        }
        
        private void SetEffectAlpha(float alpha)
        {
            // RawImage (UI)
            if (sceneEffectDisplay != null)
            {
                var color = sceneEffectDisplay.color;
                color.a = alpha;
                sceneEffectDisplay.color = color;
            }
            
            // Renderer (world-space) - adjust material color
            if (sceneEffectRenderer != null && sceneEffectRenderer.material != null)
            {
                var color = sceneEffectRenderer.material.color;
                color.a = alpha;
                sceneEffectRenderer.material.color = color;
            }
        }
        
        private float GetEffectAlpha()
        {
            if (sceneEffectDisplay != null)
            {
                return sceneEffectDisplay.color.a;
            }
            if (sceneEffectRenderer != null && sceneEffectRenderer.material != null)
            {
                return sceneEffectRenderer.material.color.a;
            }
            return 1f;
        }
        
        private System.Collections.IEnumerator FadeEffectIn()
        {
            SetEffectAlpha(0f);
            float elapsed = 0f;
            
            while (elapsed < effectFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / effectFadeDuration;
                SetEffectAlpha(Mathf.Lerp(0f, 1f, t));
                yield return null;
            }
            
            SetEffectAlpha(1f);
        }
        
        private System.Collections.IEnumerator FadeEffectOut()
        {
            float startAlpha = GetEffectAlpha();
            float elapsed = 0f;
            
            while (elapsed < effectFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / effectFadeDuration;
                SetEffectAlpha(Mathf.Lerp(startAlpha, 0f, t));
                yield return null;
            }
            
            SetEffectAlpha(0f);
            sceneEffectAnimator.Stop();
            HideEffectDisplay();
        }
        
        #endregion
    }
}