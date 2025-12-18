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
            // Prepare and show frame 0, but do NOT play
            // DialogueManager will call Play() when needed
            Prepare();
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
            
            if (targetRawImage.texture != spriteSheet)
            {
                targetRawImage.texture = spriteSheet;
            }
            
            float frameWidth = 1f / columns;
            float frameHeight = 1f / rows;
            
            int col = frameIndex % columns;
            int row = frameIndex / columns;
            
            float x = col * frameWidth;
            float y = 1f - ((row + 1) * frameHeight);
            
            targetRawImage.uvRect = new Rect(x, y, frameWidth, frameHeight);
        }
        
        public void Prepare()
        {
            if (spriteSheet == null || targetRawImage == null) return;
            
            targetRawImage.texture = spriteSheet;
            isPrepared = true;
            isPlaying = false;
            currentFrame = 0;
            frameTimer = 0f;
            DisplayFrame(0);
        }
        
        public void Play()
        {
            if (!isPrepared) Prepare();
            isPlaying = true;
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