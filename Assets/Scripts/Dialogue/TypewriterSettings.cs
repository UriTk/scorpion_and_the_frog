using UnityEngine;

namespace PointClickDetective
{
    /// <summary>
    /// Settings for a specific text style (normal, italic, bold).
    /// </summary>
    [System.Serializable]
    public class TypewriterStyleSettings
    {
        [Tooltip("Characters revealed per second")]
        [Range(1f, 100f)]
        public float charactersPerSecond = 30f;
        
        [Tooltip("Minimum time between typing sounds")]
        public float soundInterval = 0.05f;
        
        [Tooltip("Sound clips for this style (randomly selected)")]
        public AudioClip[] typingSounds;
        
        [Tooltip("Pitch variation range (0.9 to 1.1 means ±10%)")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        
        [Tooltip("Volume multiplier for this style")]
        [Range(0f, 1f)]
        public float volume = 1f;
        
        /// <summary>
        /// Get a random typing sound, or null if none configured.
        /// </summary>
        public AudioClip GetRandomSound()
        {
            if (typingSounds == null || typingSounds.Length == 0)
                return null;
            return typingSounds[Random.Range(0, typingSounds.Length)];
        }
        
        /// <summary>
        /// Get a random pitch within the configured range.
        /// </summary>
        public float GetRandomPitch()
        {
            return Random.Range(pitchRange.x, pitchRange.y);
        }
        
        /// <summary>
        /// Check if this style has sounds configured.
        /// </summary>
        public bool HasSounds => typingSounds != null && typingSounds.Length > 0;
    }
    
    /// <summary>
    /// Complete typewriter settings for a character, including style variations.
    /// </summary>
    [System.Serializable]
    public class CharacterTypewriterSettings
    {
        [Tooltip("Which character these settings apply to")]
        public CharacterType character;
        
        [Tooltip("Settings for normal text")]
        public TypewriterStyleSettings normalStyle = new TypewriterStyleSettings();
        
        [Tooltip("Settings for italic text (thoughts, emphasis)")]
        public TypewriterStyleSettings italicStyle = new TypewriterStyleSettings();
        
        [Tooltip("Settings for bold text (shouting, emphasis)")]
        public TypewriterStyleSettings boldStyle = new TypewriterStyleSettings();
        
        [Tooltip("Settings for bold italic text")]
        public TypewriterStyleSettings boldItalicStyle = new TypewriterStyleSettings();
        
        /// <summary>
        /// Get the appropriate style settings for a text style.
        /// Falls back to normal if the requested style has no sounds.
        /// </summary>
        public TypewriterStyleSettings GetStyleSettings(TextStyle style)
        {
            TypewriterStyleSettings result = style switch
            {
                TextStyle.BoldItalic => boldItalicStyle,
                TextStyle.Italic => italicStyle,
                TextStyle.Bold => boldStyle,
                _ => normalStyle
            };
            
            // Fall back to normal if no sounds configured for this style
            if (result == null || !result.HasSounds)
            {
                return normalStyle;
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// Text style being displayed.
    /// </summary>
    public enum TextStyle
    {
        Normal,
        Italic,
        Bold,
        BoldItalic
    }
    
    /// <summary>
    /// ScriptableObject for reusable typewriter configurations.
    /// </summary>
    [CreateAssetMenu(fileName = "TypewriterConfig", menuName = "Point Click Detective/Typewriter Config")]
    public class TypewriterConfigSO : ScriptableObject
    {
        [Header("Default Settings (used when no character match)")]
        public TypewriterStyleSettings defaultNormal = new TypewriterStyleSettings();
        public TypewriterStyleSettings defaultItalic = new TypewriterStyleSettings();
        public TypewriterStyleSettings defaultBold = new TypewriterStyleSettings();
        public TypewriterStyleSettings defaultBoldItalic = new TypewriterStyleSettings();
        
        [Header("Character-Specific Settings")]
        public CharacterTypewriterSettings[] characterSettings;
        
        /// <summary>
        /// Get typewriter settings for a specific character and style.
        /// </summary>
        public TypewriterStyleSettings GetSettings(CharacterType? character, TextStyle style)
        {
            // Try to find character-specific settings
            if (character.HasValue && characterSettings != null)
            {
                foreach (var charSettings in characterSettings)
                {
                    if (charSettings.character == character.Value)
                    {
                        return charSettings.GetStyleSettings(style);
                    }
                }
            }
            
            // Fall back to defaults
            TypewriterStyleSettings result = style switch
            {
                TextStyle.BoldItalic => defaultBoldItalic,
                TextStyle.Italic => defaultItalic,
                TextStyle.Bold => defaultBold,
                _ => defaultNormal
            };
            
            // Fall back to normal if no sounds configured
            if (result == null || !result.HasSounds)
            {
                return defaultNormal;
            }
            
            return result;
        }
    }
}
