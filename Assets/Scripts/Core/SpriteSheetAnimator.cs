using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace PointClickDetective
{
    public class SpriteSheetAnimator : MonoBehaviour
    {
        [Header("Sprite Sheet")]
        [SerializeField] private Texture2D spriteSheet;
        
        [Header("Grid Settings")]
        [SerializeField] private int columns = 4;
        [SerializeField] private int rows = 4;
        [SerializeField] private int totalFrames = 16;
        
        [Header("Playback")]
        [SerializeField] private float fps = 12f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnAwake = true;
        
        [Header("Display")]
        [SerializeField] private RawImage targetRawImage;
        
        [Header("Events")]
        public UnityEvent OnAnimationComplete;
        public UnityEvent OnLoopComplete;
        
        private int currentFrame;
        private float frameTimer;
        private bool isPlaying;
        private bool isPrepared;
        
        public bool IsPlaying => isPlaying;
        public bool IsPrepared => isPrepared;
        public bool IsLooping { get => loop; set => loop = value; }
        public int CurrentFrame => currentFrame;
        public int TotalFrames => totalFrames;
        
        private void Awake()
        {
            OnAnimationComplete ??= new UnityEvent();
            OnLoopComplete ??= new UnityEvent();
        }
        
        private void Start()
        {
            Debug.Log($"[SpriteSheetAnimator] START - Sheet: {(spriteSheet != null ? spriteSheet.name : "NULL")}, RawImage: {(targetRawImage != null ? targetRawImage.name : "NULL")}");
            
            Prepare();
            
            if (playOnAwake)
            {
                Play();
            }
        }
        
        private void Update()
        {
            if (!isPlaying || !isPrepared) return;
            
            frameTimer += Time.deltaTime;
            float frameTime = 1f / fps;
            
            if (frameTimer >= frameTime)
            {
                frameTimer = 0f;
                currentFrame++;
                
                if (currentFrame >= totalFrames)
                {
                    OnLoopComplete?.Invoke();
                    
                    if (loop)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        currentFrame = totalFrames - 1;
                        isPlaying = false;
                        OnAnimationComplete?.Invoke();
                        return;
                    }
                }
                
                DisplayFrame(currentFrame);
            }
        }
        
        private void DisplayFrame(int frameIndex)
        {
            if (targetRawImage == null || spriteSheet == null) return;
            
            // Ensure texture is assigned
            if (targetRawImage.texture != spriteSheet)
            {
                targetRawImage.texture = spriteSheet;
            }
            
            float frameWidth = 1f / columns;
            float frameHeight = 1f / rows;
            
            int col = frameIndex % columns;
            int row = frameIndex / columns;
            
            // Top-left origin (row 0 = top of image)
            float x = col * frameWidth;
            float y = 1f - ((row + 1) * frameHeight);
            
            targetRawImage.uvRect = new Rect(x, y, frameWidth, frameHeight);
        }
        
        public void Prepare()
        {
            if (spriteSheet == null)
            {
                Debug.LogError("[SpriteSheetAnimator] No sprite sheet assigned!");
                return;
            }
            
            if (targetRawImage == null)
            {
                Debug.LogError("[SpriteSheetAnimator] No RawImage assigned!");
                return;
            }
            
            targetRawImage.texture = spriteSheet;
            isPrepared = true;
            DisplayFrame(0);
            
            Debug.Log($"[SpriteSheetAnimator] PREPARED - {columns}x{rows} grid, {totalFrames} frames, {fps} fps");
        }
        
        public void Play()
        {
            if (!isPrepared) Prepare();
            isPlaying = true;
            Debug.Log("[SpriteSheetAnimator] PLAYING");
        }
        
        public void Pause()
        {
            isPlaying = false;
        }
        
        public void Stop()
        {
            isPlaying = false;
            currentFrame = 0;
            frameTimer = 0f;
            DisplayFrame(0);
        }
        
        public void SetFrame(int frame)
        {
            currentFrame = Mathf.Clamp(frame, 0, totalFrames - 1);
            DisplayFrame(currentFrame);
        }
        
        public void SetSpriteSheet(Texture2D sheet, int cols, int rowCount, int frames, float framesPerSecond = 12f)
        {
            spriteSheet = sheet;
            columns = cols;
            rows = rowCount;
            totalFrames = frames;
            fps = framesPerSecond;
            isPrepared = false;
            Prepare();
        }
        
        public void FinishAndStop()
        {
            loop = false;
        }
    }
}