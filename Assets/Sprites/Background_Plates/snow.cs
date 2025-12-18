using UnityEngine;
using System.Collections.Generic;

namespace PointClickDetective
{
    /// <summary>
    /// Procedural 2D snow effect. Attach to a GameObject in your scene.
    /// No sprite sheets needed - generates snowflakes procedurally.
    /// </summary>
    public class SnowEffect : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Number of snowflakes active at once")]
        [SerializeField] private int maxSnowflakes = 100;
        
        [Tooltip("Snowflakes spawned per second")]
        [SerializeField] private float spawnRate = 30f;
        
        [Header("Snowflake Appearance")]
        [Tooltip("Optional sprite for snowflakes. If null, uses white circle.")]
        [SerializeField] private Sprite snowflakeSprite;
        
        [Tooltip("Min/max size of snowflakes")]
        [SerializeField] private Vector2 sizeRange = new Vector2(0.05f, 0.15f);
        
        [Tooltip("Snowflake color")]
        [SerializeField] private Color snowColor = new Color(1f, 1f, 1f, 0.9f);
        
        [Header("Movement")]
        [Tooltip("Fall speed range")]
        [SerializeField] private Vector2 fallSpeedRange = new Vector2(1f, 3f);
        
        [Tooltip("Horizontal drift speed range")]
        [SerializeField] private Vector2 driftRange = new Vector2(-0.5f, 0.5f);
        
        [Tooltip("Sway amount (side to side movement)")]
        [SerializeField] private float swayAmount = 0.3f;
        
        [Tooltip("Sway speed")]
        [SerializeField] private float swaySpeed = 2f;
        
        [Header("Spawn Area (if no camera found)")]
        [SerializeField] private float manualWidth = 20f;
        [SerializeField] private float manualHeight = 12f;
        
        [Header("Rendering")]
        [Tooltip("Sorting layer for snowflakes")]
        [SerializeField] private string sortingLayerName = "Default";
        
        [Tooltip("Order in layer")]
        [SerializeField] private int orderInLayer = 100;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = true;
        
        // Runtime
        private List<Snowflake> snowflakes = new List<Snowflake>();
        private float spawnTimer;
        private Transform snowContainer;
        private Camera mainCamera;
        private Sprite generatedSprite;
        
        private float spawnMinX, spawnMaxX, spawnY, destroyY;
        
        private class Snowflake
        {
            public GameObject gameObject;
            public SpriteRenderer renderer;
            public float fallSpeed;
            public float drift;
            public float swayOffset;
            public float swayPhase;
        }
        
        private void Start()
        {
            mainCamera = Camera.main;
            
            // Create container for snowflakes
            snowContainer = new GameObject("SnowContainer").transform;
            snowContainer.SetParent(transform);
            snowContainer.localPosition = Vector3.zero;
            
            // Generate circle sprite if no sprite provided
            if (snowflakeSprite == null)
            {
                GenerateCircleSprite();
            }
            
            // Calculate bounds
            UpdateBounds();
            
            // Spawn initial batch
            for (int i = 0; i < maxSnowflakes / 2; i++)
            {
                SpawnSnowflake(true);
            }
            
            Debug.Log($"[SnowEffect] Started with {snowflakes.Count} initial snowflakes. Bounds: X({spawnMinX} to {spawnMaxX}), SpawnY: {spawnY}, DestroyY: {destroyY}");
        }
        
        private void GenerateCircleSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    alpha = Mathf.Pow(alpha, 0.5f); // Softer falloff
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            
            generatedSprite = Sprite.Create(
                tex, 
                new Rect(0, 0, size, size), 
                new Vector2(0.5f, 0.5f),
                32f
            );
            
            Debug.Log("[SnowEffect] Generated circle sprite");
        }
        
        private void UpdateBounds()
        {
            if (mainCamera != null)
            {
                float camHeight = mainCamera.orthographicSize * 2f;
                float camWidth = camHeight * mainCamera.aspect;
                Vector3 camPos = mainCamera.transform.position;
                
                spawnMinX = camPos.x - camWidth / 2f - 1f;
                spawnMaxX = camPos.x + camWidth / 2f + 1f;
                spawnY = camPos.y + mainCamera.orthographicSize + 1f;
                destroyY = camPos.y - mainCamera.orthographicSize - 1f;
            }
            else
            {
                spawnMinX = transform.position.x - manualWidth / 2f;
                spawnMaxX = transform.position.x + manualWidth / 2f;
                spawnY = transform.position.y + manualHeight / 2f + 1f;
                destroyY = transform.position.y - manualHeight / 2f - 1f;
                
                Debug.LogWarning("[SnowEffect] No main camera found, using manual bounds");
            }
        }
        
        private void Update()
        {
            // Update bounds each frame in case camera moves
            UpdateBounds();
            
            // Spawn new snowflakes
            spawnTimer += Time.deltaTime;
            float spawnInterval = 1f / spawnRate;
            
            while (spawnTimer >= spawnInterval && snowflakes.Count < maxSnowflakes)
            {
                SpawnSnowflake(false);
                spawnTimer -= spawnInterval;
            }
            
            // Update existing snowflakes
            UpdateSnowflakes();
        }
        
        private void SpawnSnowflake(bool randomY)
        {
            float x = Random.Range(spawnMinX, spawnMaxX);
            float y = randomY ? Random.Range(destroyY, spawnY) : spawnY;
            
            GameObject go = new GameObject("Snowflake");
            go.transform.SetParent(snowContainer);
            go.transform.position = new Vector3(x, y, 0f);
            
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = snowflakeSprite != null ? snowflakeSprite : generatedSprite;
            sr.color = snowColor;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = orderInLayer;
            
            float size = Random.Range(sizeRange.x, sizeRange.y);
            go.transform.localScale = Vector3.one * size;
            
            Snowflake flake = new Snowflake
            {
                gameObject = go,
                renderer = sr,
                fallSpeed = Random.Range(fallSpeedRange.x, fallSpeedRange.y),
                drift = Random.Range(driftRange.x, driftRange.y),
                swayOffset = Random.Range(0f, Mathf.PI * 2f),
                swayPhase = Random.Range(0.5f, 1.5f)
            };
            
            snowflakes.Add(flake);
        }
        
        private void UpdateSnowflakes()
        {
            for (int i = snowflakes.Count - 1; i >= 0; i--)
            {
                var flake = snowflakes[i];
                
                if (flake.gameObject == null)
                {
                    snowflakes.RemoveAt(i);
                    continue;
                }
                
                // Calculate movement
                float sway = Mathf.Sin((Time.time * swaySpeed * flake.swayPhase) + flake.swayOffset) * swayAmount;
                
                Vector3 pos = flake.gameObject.transform.position;
                pos.y -= flake.fallSpeed * Time.deltaTime;
                pos.x += (flake.drift + sway) * Time.deltaTime;
                flake.gameObject.transform.position = pos;
                
                // Destroy if below screen
                if (pos.y < destroyY)
                {
                    Destroy(flake.gameObject);
                    snowflakes.RemoveAt(i);
                }
            }
        }
        
        private void OnDestroy()
        {
            foreach (var flake in snowflakes)
            {
                if (flake.gameObject != null)
                {
                    Destroy(flake.gameObject);
                }
            }
            snowflakes.Clear();
        }
        
        private void OnDisable()
        {
            foreach (var flake in snowflakes)
            {
                if (flake.gameObject != null)
                {
                    Destroy(flake.gameObject);
                }
            }
            snowflakes.Clear();
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw spawn area
            Gizmos.color = Color.cyan;
            
            Camera cam = Camera.main;
            if (cam != null)
            {
                float camHeight = cam.orthographicSize * 2f;
                float camWidth = camHeight * cam.aspect;
                Vector3 camPos = cam.transform.position;
                
                Vector3 topLeft = new Vector3(camPos.x - camWidth / 2f, camPos.y + cam.orthographicSize + 1f, 0);
                Vector3 topRight = new Vector3(camPos.x + camWidth / 2f, camPos.y + cam.orthographicSize + 1f, 0);
                Gizmos.DrawLine(topLeft, topRight);
                
                Gizmos.color = Color.red;
                Vector3 botLeft = new Vector3(camPos.x - camWidth / 2f, camPos.y - cam.orthographicSize - 1f, 0);
                Vector3 botRight = new Vector3(camPos.x + camWidth / 2f, camPos.y - cam.orthographicSize - 1f, 0);
                Gizmos.DrawLine(botLeft, botRight);
            }
            else
            {
                Vector3 topLeft = new Vector3(transform.position.x - manualWidth / 2f, transform.position.y + manualHeight / 2f + 1f, 0);
                Vector3 topRight = new Vector3(transform.position.x + manualWidth / 2f, transform.position.y + manualHeight / 2f + 1f, 0);
                Gizmos.DrawLine(topLeft, topRight);
            }
        }
        
        #region Public Methods
        
        public void SetIntensity(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            spawnRate = Mathf.Lerp(10f, 60f, intensity);
            maxSnowflakes = Mathf.RoundToInt(Mathf.Lerp(30, 200, intensity));
        }
        
        public void StartBlizzard()
        {
            spawnRate = 100f;
            maxSnowflakes = 300;
            fallSpeedRange = new Vector2(2f, 5f);
            driftRange = new Vector2(-2f, -0.5f);
            swayAmount = 0.2f;
        }
        
        public void GentleSnow()
        {
            spawnRate = 30f;
            maxSnowflakes = 100;
            fallSpeedRange = new Vector2(1f, 3f);
            driftRange = new Vector2(-0.5f, 0.5f);
            swayAmount = 0.3f;
        }
        
        public void StopSnow()
        {
            enabled = false;
        }
        
        public void StartSnow()
        {
            enabled = true;
        }
        
        #endregion
    }
}