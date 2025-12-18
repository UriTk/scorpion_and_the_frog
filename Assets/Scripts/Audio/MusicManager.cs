using UnityEngine;
using UnityEngine.Events;
using System.Collections;

namespace PointClickDetective
{
    /// <summary>
    /// Represents a music track with optional intro and loop portions.
    /// </summary>
    [System.Serializable]
    public class MusicTrack
    {
        [Tooltip("Intro portion that plays once (optional - leave empty if no intro)")]
        public AudioClip intro;
        
        [Tooltip("Main loop portion (required for looping tracks, optional for one-shots)")]
        public AudioClip loop;
        
        [Tooltip("If false, the track plays once and stops (for credits, reveals, etc.)")]
        public bool shouldLoop = true;
        
        public bool HasIntro => intro != null;
        public bool HasLoop => loop != null;
        public bool IsValid => intro != null || loop != null;
        
        /// <summary>
        /// Get the clip to play first (intro if available, otherwise loop).
        /// </summary>
        public AudioClip FirstClip => intro != null ? intro : loop;
    }
    
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }
        
        [Header("Current Scene Music")]
        [Tooltip("The music track that plays when Scorpion is active")]
        [SerializeField] private MusicTrack scorpionTrack;
        [Tooltip("The music track that plays when Frog is active")]
        [SerializeField] private MusicTrack frogTrack;
        
        [Header("Crossfade Settings")]
        [Tooltip("How long it takes to transition between Scorpion and Frog tracks")]
        [SerializeField] private float characterCrossfadeDuration = 1.5f;
        [Tooltip("How long it takes to transition when changing scenes")]
        [SerializeField] private float sceneCrossfadeDuration = 2.0f;
        [SerializeField] private AnimationCurve crossfadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Sync Settings")]
        [Tooltip("When switching characters, should the new track start at the same position? (Keeps the beat synced)")]
        [SerializeField] private bool maintainPositionOnCharacterSwitch = true;
        
        [Header("Volume Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 1f;
        
        [Header("Events")]
        public UnityEvent OnCrossfadeStarted;
        public UnityEvent OnCrossfadeComplete;
        public UnityEvent OnTrackFinished; // For non-looping tracks
        public UnityEvent OnIntroFinished;
        
        // Internal audio sources (created automatically)
        private AudioSource sourceA;
        private AudioSource sourceB;
        private AudioSource activeSource;
        private AudioSource inactiveSource;
        
        // State
        private Coroutine crossfadeCoroutine;
        private Coroutine introToLoopCoroutine;
        private CharacterType currentCharacter;
        private MusicTrack currentTrack;
        private bool isPlayingIntro;
        private bool isPlayingOneShot;
        
        // Stored tracks for returning after one-shots
        private MusicTrack storedScorpionTrack;
        private MusicTrack storedFrogTrack;
        
        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                UpdateVolume();
            }
        }
        
        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                UpdateVolume();
            }
        }
        
        public bool IsPlayingOneShot => isPlayingOneShot;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Create audio sources automatically
            sourceA = gameObject.AddComponent<AudioSource>();
            sourceA.playOnAwake = false;
            
            sourceB = gameObject.AddComponent<AudioSource>();
            sourceB.playOnAwake = false;
            
            activeSource = sourceA;
            inactiveSource = sourceB;
            
            OnCrossfadeStarted ??= new UnityEvent();
            OnCrossfadeComplete ??= new UnityEvent();
            OnTrackFinished ??= new UnityEvent();
            OnIntroFinished ??= new UnityEvent();
        }
        
        private void Start()
        {
            // Subscribe to character changes
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterChanged.AddListener(OnCharacterChanged);
                currentCharacter = GameManager.Instance.CurrentCharacter;
            }
            else
            {
                Debug.LogWarning("[MusicManager] GameManager.Instance is null - music won't auto-switch");
            }
            
            // Preload and start playing
            StartCoroutine(PreloadAndPlay());
        }
        
        private IEnumerator PreloadAndPlay()
        {
            // Preload all configured tracks
            PreloadTrack(scorpionTrack);
            PreloadTrack(frogTrack);
            
            // Wait for the current character's track to be fully loaded
            MusicTrack targetTrack = currentCharacter == CharacterType.Scorpion ? scorpionTrack : frogTrack;
            if (targetTrack != null)
            {
                if (targetTrack.intro != null)
                {
                    while (targetTrack.intro.loadState == AudioDataLoadState.Loading)
                        yield return null;
                }
                if (targetTrack.loop != null)
                {
                    while (targetTrack.loop.loadState == AudioDataLoadState.Loading)
                        yield return null;
                }
            }
            
            // Now play
            PlayCurrentCharacterTrack(false);
            Debug.Log($"[MusicManager] Started with character: {currentCharacter}");
        }
        
        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnCharacterChanged.RemoveListener(OnCharacterChanged);
            }
        }
        
        #region Public Methods
        
        /// <summary>
        /// Set new tracks for the current scene using MusicTrack objects.
        /// </summary>
        public void SetSceneTracks(MusicTrack newScorpionTrack, MusicTrack newFrogTrack, bool crossfade = true)
        {
            scorpionTrack = newScorpionTrack;
            frogTrack = newFrogTrack;
            
            // Preload all clips
            PreloadTrack(scorpionTrack);
            PreloadTrack(frogTrack);
            
            PlayCurrentCharacterTrack(crossfade);
            
            Debug.Log($"[MusicManager] Set new scene tracks");
        }
        
        /// <summary>
        /// Set new tracks for the current scene using simple AudioClips (loops only, no intro).
        /// </summary>
        public void SetSceneTracks(AudioClip newScorpionLoop, AudioClip newFrogLoop, bool crossfade = true)
        {
            scorpionTrack = new MusicTrack { loop = newScorpionLoop, shouldLoop = true };
            frogTrack = new MusicTrack { loop = newFrogLoop, shouldLoop = true };
            
            // Preload all clips
            PreloadTrack(scorpionTrack);
            PreloadTrack(frogTrack);
            
            PlayCurrentCharacterTrack(crossfade);
            
            Debug.Log($"[MusicManager] Set new scene tracks (simple). Scorpion: {newScorpionLoop?.name}, Frog: {newFrogLoop?.name}");
        }
        
        /// <summary>
        /// Set new tracks with intro + loop for each character.
        /// </summary>
        public void SetSceneTracks(
            AudioClip scorpionIntro, AudioClip scorpionLoop,
            AudioClip frogIntro, AudioClip frogLoop,
            bool crossfade = true)
        {
            scorpionTrack = new MusicTrack { intro = scorpionIntro, loop = scorpionLoop, shouldLoop = true };
            frogTrack = new MusicTrack { intro = frogIntro, loop = frogLoop, shouldLoop = true };
            
            // Preload all clips
            PreloadTrack(scorpionTrack);
            PreloadTrack(frogTrack);
            
            PlayCurrentCharacterTrack(crossfade);
            
            Debug.Log($"[MusicManager] Set new scene tracks with intros");
        }
        
        /// <summary>
        /// Set new tracks with intro + loop for each character, with sync option.
        /// Used by GameSceneContainer for scene transitions.
        /// </summary>
        /// <param name="syncPosition">If true, syncs to current playback position (for scene variations). 
        /// If false, starts from beginning (for completely different songs like credits).</param>
        public void SetSceneTracksWithIntro(
            AudioClip scorpionIntro, AudioClip scorpionLoop,
            AudioClip frogIntro, AudioClip frogLoop,
            bool crossfade = true,
            bool syncPosition = true)
        {
            scorpionTrack = new MusicTrack { intro = scorpionIntro, loop = scorpionLoop, shouldLoop = true };
            frogTrack = new MusicTrack { intro = frogIntro, loop = frogLoop, shouldLoop = true };
            
            // Preload all clips
            PreloadTrack(scorpionTrack);
            PreloadTrack(frogTrack);
            
            // Play with sync option
            PlayCurrentCharacterTrackWithSync(crossfade, syncPosition);
            
            Debug.Log($"[MusicManager] Set scene tracks (sync: {syncPosition})");
        }
        
        /// <summary>
        /// Play a one-shot track (no loop, plays once). 
        /// Call this via flags for credits, special moments, etc.
        /// After the track finishes, music stops - call SetSceneTracks to resume normal music.
        /// </summary>
        public void PlayOneShotTrack(AudioClip clip, bool crossfade = true)
        {
            if (clip == null) return;
            
            isPlayingOneShot = true;
            
            // Stop any intro-to-loop transition
            if (introToLoopCoroutine != null)
            {
                StopCoroutine(introToLoopCoroutine);
                introToLoopCoroutine = null;
            }
            
            if (crossfade && activeSource.isPlaying)
            {
                if (crossfadeCoroutine != null) StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = StartCoroutine(CrossfadeCoroutine(clip, sceneCrossfadeDuration, false, false, () => {
                    StartCoroutine(WaitForTrackEnd());
                }));
            }
            else
            {
                activeSource.Stop();
                activeSource.clip = clip;
                activeSource.loop = false;
                activeSource.volume = masterVolume * musicVolume;
                activeSource.time = 0;
                activeSource.Play();
                StartCoroutine(WaitForTrackEnd());
            }
            
            Debug.Log($"[MusicManager] Playing one-shot track: {clip.name}");
        }
        
        /// <summary>
        /// Preload a track's audio data into memory.
        /// </summary>
        public void PreloadTrack(MusicTrack track)
        {
            if (track == null) return;
            
            if (track.intro != null && track.intro.loadState != AudioDataLoadState.Loaded)
            {
                track.intro.LoadAudioData();
            }
            
            if (track.loop != null && track.loop.loadState != AudioDataLoadState.Loaded)
            {
                track.loop.LoadAudioData();
            }
        }
        
        /// <summary>
        /// Preload an audio clip into memory.
        /// </summary>
        public void PreloadClip(AudioClip clip)
        {
            if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
            }
        }
        
        /// <summary>
        /// Play a custom one-shot track (no loop).
        /// </summary>
        public void PlayOneShotTrack(AudioClip clip, float fadeInDuration = 1f)
        {
            if (clip == null) return;
            
            // Store current tracks to restore later
            storedScorpionTrack = scorpionTrack;
            storedFrogTrack = frogTrack;
            isPlayingOneShot = true;
            
            // Stop intro-to-loop coroutine if running
            if (introToLoopCoroutine != null)
            {
                StopCoroutine(introToLoopCoroutine);
                introToLoopCoroutine = null;
            }
            
            StartCrossfade(clip, fadeInDuration, false, false, () =>
            {
                // Monitor for track completion
                StartCoroutine(WaitForTrackEnd());
            });
            
            Debug.Log($"[MusicManager] Playing one-shot: {clip.name}");
        }
        
        /// <summary>
        /// Return to the scene music after a one-shot track.
        /// </summary>
        public void ReturnToSceneMusic(float crossfadeDuration = 1f)
        {
            if (!isPlayingOneShot) return;
            
            isPlayingOneShot = false;
            scorpionTrack = storedScorpionTrack;
            frogTrack = storedFrogTrack;
            
            PlayCurrentCharacterTrack(true);
            
            Debug.Log("[MusicManager] Returned to scene music");
        }
        
        /// <summary>
        /// Manually trigger a crossfade to a specific track.
        /// </summary>
        public void CrossfadeToTrack(AudioClip clip, float duration, bool loop = true, bool maintainPosition = false)
        {
            if (clip == null) return;
            StartCrossfade(clip, duration, maintainPosition, loop);
        }
        
        public void StopMusic(float fadeOutDuration = 1f)
        {
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
            }
            
            if (introToLoopCoroutine != null)
            {
                StopCoroutine(introToLoopCoroutine);
                introToLoopCoroutine = null;
            }
            
            crossfadeCoroutine = StartCoroutine(FadeOutCoroutine(fadeOutDuration));
        }
        
        public void PauseMusic()
        {
            activeSource.Pause();
        }
        
        public void ResumeMusic()
        {
            activeSource.UnPause();
        }
        
        #endregion
        
        #region Internal Methods
        
        private void OnCharacterChanged(CharacterType newCharacter)
        {
            if (currentCharacter == newCharacter) return;
            if (isPlayingOneShot) return; // Don't interrupt one-shots
            
            currentCharacter = newCharacter;
            
            // Character switch should crossfade at the same absolute timestamp
            PlayCharacterTrackForSwitch();
            
            Debug.Log($"[MusicManager] Character changed to: {newCharacter}");
        }
        
        /// <summary>
        /// Play track for character switch - maintains absolute position.
        /// Same timestamp in one character's track = same timestamp in other's.
        /// </summary>
        private void PlayCharacterTrackForSwitch()
        {
            MusicTrack newTrack = currentCharacter == CharacterType.Scorpion ? scorpionTrack : frogTrack;
            
            if (newTrack == null || !newTrack.IsValid)
            {
                Debug.LogWarning($"[MusicManager] No valid track for {currentCharacter}");
                return;
            }
            
            // Cancel any scheduled playback on inactive source
            inactiveSource.Stop();
            
            // Stop any intro-to-loop transition from previous track
            bool wasInIntro = isPlayingIntro;
            if (introToLoopCoroutine != null)
            {
                StopCoroutine(introToLoopCoroutine);
                introToLoopCoroutine = null;
                isPlayingIntro = false;
            }
            
            // Get current playback position
            float currentTime = activeSource.time;
            
            AudioClip clipToPlay;
            float targetTime;
            bool shouldStartIntroCoroutine = false;
            
            if (wasInIntro && newTrack.HasIntro)
            {
                // We were in intro, switch to new character's intro at same timestamp
                clipToPlay = newTrack.intro;
                targetTime = Mathf.Min(currentTime, clipToPlay.length - 0.01f);
                shouldStartIntroCoroutine = newTrack.HasLoop;
            }
            else if (!wasInIntro || !newTrack.HasIntro)
            {
                // We were in loop (or new track has no intro), switch to loop at same timestamp
                clipToPlay = newTrack.HasLoop ? newTrack.loop : newTrack.FirstClip;
                
                if (wasInIntro && !newTrack.HasIntro)
                {
                    // Old track was in intro but new track has no intro
                    // Start new track's loop at the same absolute time
                    targetTime = Mathf.Min(currentTime, clipToPlay.length - 0.01f);
                }
                else
                {
                    // Both in loop, sync position
                    targetTime = currentTime % clipToPlay.length; // Wrap around if needed
                }
            }
            else
            {
                // Fallback
                clipToPlay = newTrack.FirstClip;
                targetTime = 0f;
            }
            
            currentTrack = newTrack;
            
            if (activeSource.isPlaying)
            {
                // Crossfade with absolute time sync
                StartCrossfadeAtTime(clipToPlay, targetTime, characterCrossfadeDuration, true, () =>
                {
                    if (shouldStartIntroCoroutine)
                    {
                        introToLoopCoroutine = StartCoroutine(WaitForIntroThenLoop());
                    }
                });
            }
            else
            {
                PlayImmediateAtTime(clipToPlay, targetTime, !shouldStartIntroCoroutine);
                
                if (shouldStartIntroCoroutine)
                {
                    introToLoopCoroutine = StartCoroutine(WaitForIntroThenLoop());
                }
            }
        }
        
        /// <summary>
        /// Start crossfade to a clip at a specific time position.
        /// </summary>
        private void StartCrossfadeAtTime(AudioClip newClip, float startTime, float duration, bool loop, System.Action onComplete = null)
        {
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
            }
            
            crossfadeCoroutine = StartCoroutine(CrossfadeAtTimeCoroutine(newClip, startTime, duration, loop, onComplete));
        }
        
        private IEnumerator CrossfadeAtTimeCoroutine(AudioClip newClip, float startTime, float duration, bool loop, System.Action onComplete = null)
        {
            OnCrossfadeStarted?.Invoke();
            Debug.Log($"[MusicManager] Crossfading to: {newClip.name} at {startTime:F2}s over {duration}s");
            
            float targetVolume = masterVolume * musicVolume;
            
            // Setup inactive source with new clip at specific time
            inactiveSource.clip = newClip;
            inactiveSource.loop = loop;
            inactiveSource.volume = 0;
            inactiveSource.time = startTime;
            inactiveSource.Play();
            
            // Crossfade
            float elapsed = 0f;
            float startVolumeActive = activeSource.volume;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = crossfadeCurve.Evaluate(elapsed / duration);
                
                activeSource.volume = Mathf.Lerp(startVolumeActive, 0, t);
                inactiveSource.volume = Mathf.Lerp(0, targetVolume, t);
                
                yield return null;
            }
            
            // Finalize
            activeSource.Stop();
            activeSource.volume = 0;
            inactiveSource.volume = targetVolume;
            
            // Swap sources
            (activeSource, inactiveSource) = (inactiveSource, activeSource);
            
            crossfadeCoroutine = null;
            OnCrossfadeComplete?.Invoke();
            
            onComplete?.Invoke();
            
            Debug.Log($"[MusicManager] Crossfade complete");
        }
        
        /// <summary>
        /// Play a clip immediately at a specific time position.
        /// </summary>
        private void PlayImmediateAtTime(AudioClip clip, float startTime, bool loop)
        {
            activeSource.clip = clip;
            activeSource.loop = loop;
            activeSource.volume = masterVolume * musicVolume;
            activeSource.time = startTime;
            activeSource.Play();
            
            inactiveSource.Stop();
            inactiveSource.volume = 0;
            
            Debug.Log($"[MusicManager] Playing immediately: {clip.name} at {startTime:F2}s (loop: {loop})");
        }
        
        /// <summary>
        /// Play track from the beginning (with intro if available).
        /// Used for initial play and scene changes.
        /// </summary>
        private void PlayCurrentCharacterTrack(bool crossfade)
        {
            PlayCurrentCharacterTrackWithSync(crossfade, syncPosition: false);
        }
        
        /// <summary>
        /// Play track with optional sync to current playback position.
        /// Used for scene transitions where music should stay in sync.
        /// </summary>
        private void PlayCurrentCharacterTrackWithSync(bool crossfade, bool syncPosition)
        {
            currentTrack = currentCharacter == CharacterType.Scorpion ? scorpionTrack : frogTrack;
            
            if (currentTrack == null || !currentTrack.IsValid)
            {
                Debug.LogWarning($"[MusicManager] No valid track for {currentCharacter}");
                return;
            }
            
            // Stop any intro-to-loop transition
            if (introToLoopCoroutine != null)
            {
                StopCoroutine(introToLoopCoroutine);
                introToLoopCoroutine = null;
            }
            
            AudioClip clipToPlay = currentTrack.HasIntro ? currentTrack.intro : currentTrack.loop;
            bool hasIntro = currentTrack.HasIntro;
            
            // If syncing position, skip intro and go straight to loop at current time
            if (syncPosition && activeSource.isPlaying && activeSource.clip != null)
            {
                // Calculate current position as percentage
                float normalizedPosition = activeSource.time / activeSource.clip.length;
                
                // Use loop clip for synced playback (skip intro)
                AudioClip syncClip = currentTrack.loop ?? currentTrack.intro;
                float startTime = normalizedPosition * syncClip.length;
                
                if (crossfade)
                {
                    StartCrossfadeAtTime(syncClip, startTime, sceneCrossfadeDuration, currentTrack.shouldLoop);
                }
                else
                {
                    PlayImmediateAtTime(syncClip, startTime, currentTrack.shouldLoop);
                }
                
                Debug.Log($"[MusicManager] Synced to position: {startTime:F2}s ({normalizedPosition:P0})");
            }
            else
            {
                // Normal playback from beginning
                if (crossfade && activeSource.isPlaying)
                {
                    StartCrossfade(clipToPlay, sceneCrossfadeDuration, false, currentTrack.shouldLoop && !hasIntro, () =>
                    {
                        if (hasIntro && currentTrack.HasLoop)
                        {
                            introToLoopCoroutine = StartCoroutine(WaitForIntroThenLoop());
                        }
                    });
                }
                else
                {
                    PlayImmediate(clipToPlay, currentTrack.shouldLoop && !hasIntro);
                    
                    if (hasIntro && currentTrack.HasLoop)
                    {
                        introToLoopCoroutine = StartCoroutine(WaitForIntroThenLoop());
                    }
                }
            }
        }
        
        private void PlayImmediate(AudioClip clip, bool loop)
        {
            activeSource.clip = clip;
            activeSource.loop = loop;
            activeSource.volume = masterVolume * musicVolume;
            activeSource.Play();
            
            inactiveSource.Stop();
            inactiveSource.volume = 0;
            
            Debug.Log($"[MusicManager] Playing immediately: {clip.name} (loop: {loop})");
        }
        
        private void StartCrossfade(AudioClip newClip, float duration, bool maintainPosition, bool loop = true, System.Action onComplete = null)
        {
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
            }
            
            crossfadeCoroutine = StartCoroutine(CrossfadeCoroutine(newClip, duration, maintainPosition, loop, onComplete));
        }
        
        private void UpdateVolume()
        {
            float targetVolume = masterVolume * musicVolume;
            
            if (crossfadeCoroutine == null && activeSource != null)
            {
                activeSource.volume = targetVolume;
            }
        }
        
        #endregion
        
        #region Coroutines
        
        private IEnumerator CrossfadeCoroutine(AudioClip newClip, float duration, bool maintainPosition, bool loop, System.Action onComplete = null)
        {
            OnCrossfadeStarted?.Invoke();
            Debug.Log($"[MusicManager] Crossfading to: {newClip.name} over {duration}s (loop: {loop})");
            
            float targetVolume = masterVolume * musicVolume;
            
            // Setup inactive source with new clip
            inactiveSource.clip = newClip;
            inactiveSource.loop = loop;
            inactiveSource.volume = 0;
            
            // Sync position if requested
            if (maintainPosition && activeSource.isPlaying && activeSource.clip != null)
            {
                float normalizedPosition = activeSource.time / activeSource.clip.length;
                inactiveSource.time = normalizedPosition * newClip.length;
            }
            else
            {
                inactiveSource.time = 0;
            }
            
            inactiveSource.Play();
            
            // Crossfade
            float elapsed = 0f;
            float startVolumeActive = activeSource.volume;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = crossfadeCurve.Evaluate(elapsed / duration);
                
                activeSource.volume = Mathf.Lerp(startVolumeActive, 0, t);
                inactiveSource.volume = Mathf.Lerp(0, targetVolume, t);
                
                yield return null;
            }
            
            // Finalize
            activeSource.Stop();
            activeSource.volume = 0;
            inactiveSource.volume = targetVolume;
            
            // Swap sources
            (activeSource, inactiveSource) = (inactiveSource, activeSource);
            
            crossfadeCoroutine = null;
            OnCrossfadeComplete?.Invoke();
            
            onComplete?.Invoke();
            
            Debug.Log($"[MusicManager] Crossfade complete");
        }
        
        private IEnumerator WaitForIntroThenLoop()
        {
            isPlayingIntro = true;
            
            if (currentTrack == null || !currentTrack.HasLoop)
            {
                isPlayingIntro = false;
                introToLoopCoroutine = null;
                yield break;
            }
            
            // Store reference to the track we're working with
            // (in case currentTrack changes during character switch)
            MusicTrack trackForThisCoroutine = currentTrack;
            AudioClip loopClip = trackForThisCoroutine.loop;
            
            // Preload the loop clip immediately so it's ready when we need it
            if (loopClip.loadState != AudioDataLoadState.Loaded)
            {
                loopClip.LoadAudioData();
                
                // Wait for it to load
                while (loopClip.loadState == AudioDataLoadState.Loading)
                {
                    yield return null;
                    
                    // Check if we were interrupted
                    if (currentTrack != trackForThisCoroutine)
                    {
                        isPlayingIntro = false;
                        introToLoopCoroutine = null;
                        yield break;
                    }
                }
                
                if (loopClip.loadState != AudioDataLoadState.Loaded)
                {
                    Debug.LogWarning($"[MusicManager] Failed to load loop clip: {loopClip.name}");
                    isPlayingIntro = false;
                    introToLoopCoroutine = null;
                    yield break;
                }
            }
            
            // Check if we were interrupted during loading
            if (currentTrack != trackForThisCoroutine || !activeSource.isPlaying)
            {
                isPlayingIntro = false;
                introToLoopCoroutine = null;
                yield break;
            }
            
            // Setup the inactive source with the loop NOW so it's fully ready
            inactiveSource.clip = loopClip;
            inactiveSource.loop = trackForThisCoroutine.shouldLoop;
            inactiveSource.volume = activeSource.volume;
            
            // Calculate when the intro will end
            double introLength = (double)activeSource.clip.samples / activeSource.clip.frequency;
            double currentTime = (double)activeSource.timeSamples / activeSource.clip.frequency;
            double remainingTime = introLength - currentTime;
            double dspStartTime = AudioSettings.dspTime + remainingTime;
            
            // Schedule the loop to start exactly when intro ends
            inactiveSource.PlayScheduled(dspStartTime);
            
            Debug.Log($"[MusicManager] Loop scheduled to start in {remainingTime:F3}s (DSP: {dspStartTime:F3})");
            
            // Wait until just before the transition
            float waitTime = (float)remainingTime - 0.1f;
            if (waitTime > 0)
            {
                yield return new WaitForSeconds(waitTime);
            }
            
            // Check if we were interrupted
            if (currentTrack != trackForThisCoroutine)
            {
                // Cancel the scheduled playback
                inactiveSource.Stop();
                isPlayingIntro = false;
                introToLoopCoroutine = null;
                yield break;
            }
            
            // Wait for the actual transition
            while (AudioSettings.dspTime < dspStartTime)
            {
                yield return null;
                
                // Check if we were interrupted
                if (currentTrack != trackForThisCoroutine)
                {
                    inactiveSource.Stop();
                    isPlayingIntro = false;
                    introToLoopCoroutine = null;
                    yield break;
                }
            }
            
            // Swap sources - loop is now playing
            activeSource.Stop();
            (activeSource, inactiveSource) = (inactiveSource, activeSource);
            
            isPlayingIntro = false;
            OnIntroFinished?.Invoke();
            
            Debug.Log($"[MusicManager] Seamlessly transitioned to loop: {loopClip.name}");
            
            introToLoopCoroutine = null;
        }
        
        private IEnumerator WaitForTrackEnd()
        {
            // Wait for the one-shot track to finish
            while (activeSource.isPlaying)
            {
                yield return null;
            }
            
            isPlayingOneShot = false;
            OnTrackFinished?.Invoke();
            
            Debug.Log("[MusicManager] One-shot track finished");
        }
        
        private IEnumerator FadeOutCoroutine(float duration)
        {
            float startVolume = activeSource.volume;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                activeSource.volume = Mathf.Lerp(startVolume, 0, t);
                yield return null;
            }
            
            activeSource.Stop();
            activeSource.volume = 0;
            crossfadeCoroutine = null;
        }
        
        #endregion
    }
}