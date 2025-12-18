using UnityEngine;

namespace PointClickDetective
{
    /// <summary>
    /// Defines a speaker for dialogue - includes portrait and typewriter settings.
    /// Speakers are separate from player characters - NPCs can have speakers too.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpeaker", menuName = "Point Click Detective/Speaker")]
    public class SpeakerSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name for this speaker")]
        public string speakerName;
        
        [Tooltip("Default portrait for this speaker")]
        public Sprite defaultPortrait;
        
        [Tooltip("If true, hide the portrait when this speaker talks (for thoughts/narration)")]
        public bool hidePortrait;
        
        [Header("Normal Text Settings")]
        [Tooltip("Characters revealed per second for normal text")]
        [Range(1f, 100f)]
        public float charactersPerSecond = 30f;
        
        [Tooltip("Minimum time between typing sounds")]
        public float soundInterval = 0.05f;
        
        [Tooltip("Sound clips for normal typing (randomly selected)")]
        public AudioClip[] typingSounds;
        
        [Tooltip("Pitch variation range for normal text")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        
        [Tooltip("Volume multiplier")]
        [Range(0f, 1f)]
        public float volume = 1f;
        
        [Header("Italic Text Settings (for <i> tags)")]
        [Tooltip("Use different settings for italic text")]
        public bool useItalicSettings = false;
        
        [Tooltip("Characters per second for italic text")]
        [Range(1f, 100f)]
        public float italicCharsPerSecond = 25f;
        
        [Tooltip("Sound clips for italic typing (e.g., softer/whisper sounds)")]
        public AudioClip[] italicTypingSounds;
        
        [Tooltip("Pitch variation for italic text")]
        public Vector2 italicPitchRange = new Vector2(1.0f, 1.15f);
        
        [Tooltip("Volume for italic text")]
        [Range(0f, 1f)]
        public float italicVolume = 0.8f;
        
        [Header("Bold Text Settings (for <b> tags)")]
        [Tooltip("Use different settings for bold text")]
        public bool useBoldSettings = false;
        
        [Tooltip("Characters per second for bold text")]
        [Range(1f, 100f)]
        public float boldCharsPerSecond = 20f;
        
        [Tooltip("Sound clips for bold typing (e.g., heavier/emphatic sounds)")]
        public AudioClip[] boldTypingSounds;
        
        [Tooltip("Pitch variation for bold text")]
        public Vector2 boldPitchRange = new Vector2(0.85f, 1.0f);
        
        [Tooltip("Volume for bold text")]
        [Range(0f, 1f)]
        public float boldVolume = 1f;
        
        /// <summary>
        /// Get typing settings based on current text style.
        /// </summary>
        public void GetSettings(bool isBold, bool isItalic, 
            out float outCharsPerSecond, out AudioClip[] outSounds, 
            out Vector2 outPitchRange, out float outVolume)
        {
            // Bold takes priority over italic
            if (isBold && useBoldSettings)
            {
                outCharsPerSecond = boldCharsPerSecond;
                outSounds = (boldTypingSounds != null && boldTypingSounds.Length > 0) ? boldTypingSounds : typingSounds;
                outPitchRange = boldPitchRange;
                outVolume = boldVolume;
            }
            else if (isItalic && useItalicSettings)
            {
                outCharsPerSecond = italicCharsPerSecond;
                outSounds = (italicTypingSounds != null && italicTypingSounds.Length > 0) ? italicTypingSounds : typingSounds;
                outPitchRange = italicPitchRange;
                outVolume = italicVolume;
            }
            else
            {
                outCharsPerSecond = charactersPerSecond;
                outSounds = typingSounds;
                outPitchRange = pitchRange;
                outVolume = volume;
            }
        }
        
        /// <summary>
        /// Get a random typing sound for the given style.
        /// </summary>
        public AudioClip GetRandomSound(bool isBold, bool isItalic)
        {
            AudioClip[] sounds = typingSounds;
            
            if (isBold && useBoldSettings && boldTypingSounds != null && boldTypingSounds.Length > 0)
            {
                sounds = boldTypingSounds;
            }
            else if (isItalic && useItalicSettings && italicTypingSounds != null && italicTypingSounds.Length > 0)
            {
                sounds = italicTypingSounds;
            }
            
            if (sounds == null || sounds.Length == 0)
                return null;
            return sounds[Random.Range(0, sounds.Length)];
        }
        
        /// <summary>
        /// Get a random pitch for the given style.
        /// </summary>
        public float GetRandomPitch(bool isBold, bool isItalic)
        {
            Vector2 range = pitchRange;
            
            if (isBold && useBoldSettings)
            {
                range = boldPitchRange;
            }
            else if (isItalic && useItalicSettings)
            {
                range = italicPitchRange;
            }
            
            return Random.Range(range.x, range.y);
        }
        
        /// <summary>
        /// Check if this speaker has any sounds configured.
        /// </summary>
        public bool HasSounds => typingSounds != null && typingSounds.Length > 0;
    }
}