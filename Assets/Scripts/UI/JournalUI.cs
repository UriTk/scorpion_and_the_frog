using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;

namespace PointClickDetective
{
    /// <summary>
    /// UI for the detective journal - displays found clues.
    /// </summary>
    public class JournalUI : MonoBehaviour
    {
        public static JournalUI Instance { get; private set; }
        
        [Header("Panels")]
        [SerializeField] private GameObject journalPanel;
        [SerializeField] private GameObject clueListPanel;
        [SerializeField] private GameObject clueDetailPanel;
        
        [Header("Clue List")]
        [SerializeField] private Transform clueListContainer;
        [SerializeField] private GameObject clueEntryPrefab;
        [SerializeField] private float entryHeight = 60f;
        [SerializeField] private float entrySpacing = 5f;
        
        [Header("Clue Detail")]
        [SerializeField] private Image clueIcon;
        [SerializeField] private TextMeshProUGUI clueNameText;
        [SerializeField] private TextMeshProUGUI clueDescriptionText;
        [SerializeField] private TextMeshProUGUI clueLocationText;
        [SerializeField] private TextMeshProUGUI clueFoundByText;
        
        [Header("Tabs/Filters")]
        [SerializeField] private Button allCluesTab;
        [SerializeField] private Button byLocationTab;
        
        [Header("Navigation")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button deductionBoardButton;
        
        [Header("Input")]
        [SerializeField] private Key toggleKey = Key.J;
        
        [Header("Events")]
        public UnityEvent OnJournalOpened;
        public UnityEvent OnJournalClosed;
        public UnityEvent<ClueSO> OnClueSelected;
        
        // State
        private bool isOpen;
        private ClueSO selectedClue;
        private List<GameObject> spawnedEntries = new List<GameObject>();
        
        public bool IsOpen => isOpen;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            
            OnJournalOpened ??= new UnityEvent();
            OnJournalClosed ??= new UnityEvent();
            OnClueSelected ??= new UnityEvent<ClueSO>();
        }
        
        private void Start()
        {
            // Setup buttons
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseJournal);
            
            if (backButton != null)
                backButton.onClick.AddListener(ShowClueList);
            
            if (deductionBoardButton != null)
                deductionBoardButton.onClick.AddListener(OpenDeductionBoard);
            
            if (allCluesTab != null)
                allCluesTab.onClick.AddListener(ShowAllClues);
            
            if (byLocationTab != null)
                byLocationTab.onClick.AddListener(ShowCluesByLocation);
            
            // Subscribe to clue changes
            if (ClueManager.Instance != null)
            {
                ClueManager.Instance.OnClueFound.AddListener(OnNewClueFound);
            }
            
            // Initial state
            if (journalPanel != null) journalPanel.SetActive(false);
        }
        
        private void OnDestroy()
        {
            if (ClueManager.Instance != null)
            {
                ClueManager.Instance.OnClueFound.RemoveListener(OnNewClueFound);
            }
        }
        
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                if (isOpen)
                    CloseJournal();
                else
                    OpenJournal();
            }
            
            // Close with Escape
            if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (clueDetailPanel != null && clueDetailPanel.activeSelf)
                    ShowClueList();
                else
                    CloseJournal();
            }
        }
        
        #region Public Methods
        
        public void OpenJournal()
        {
            if (isOpen) return;
            
            // Don't open during dialogue
            if (DialogueManager.Instance?.IsShowing == true) return;
            
            // Close other UIs
            DeductionBoardUI.Instance?.CloseBoard();
            WorldMapUI.Instance?.CloseMap();
            
            isOpen = true;
            
            if (journalPanel != null)
                journalPanel.SetActive(true);
            
            ShowAllClues();
            
            OnJournalOpened?.Invoke();
        }
        
        public void CloseJournal()
        {
            if (!isOpen) return;
            
            isOpen = false;
            
            if (journalPanel != null)
                journalPanel.SetActive(false);
            
            OnJournalClosed?.Invoke();
        }
        
        public void ToggleJournal()
        {
            if (isOpen) CloseJournal();
            else OpenJournal();
        }
        
        #endregion
        
        #region Clue Display
        
        public void ShowAllClues()
        {
            ShowClueList();
            
            var clues = ClueManager.Instance?.GetFoundClues() ?? new List<ClueSO>();
            PopulateClueList(clues);
        }
        
        public void ShowCluesByLocation()
        {
            ShowClueList();
            
            // Group by location
            var clues = ClueManager.Instance?.GetFoundClues() ?? new List<ClueSO>();
            
            // Sort by location
            clues.Sort((a, b) => string.Compare(a.locationSceneId, b.locationSceneId));
            PopulateClueList(clues);
        }
        
        private void ShowClueList()
        {
            // Both panels visible - list on left, detail on right
            if (clueListPanel != null) clueListPanel.SetActive(true);
            // Detail panel stays visible, just shows placeholder if no clue selected
        }
        
        private void PopulateClueList(List<ClueSO> clues)
        {
            // Clear existing
            foreach (var entry in spawnedEntries)
            {
                if (entry != null) Destroy(entry);
            }
            spawnedEntries.Clear();
            
            if (clueListContainer == null || clueEntryPrefab == null) return;
            
            RectTransform containerRect = clueListContainer as RectTransform;
            
            for (int i = 0; i < clues.Count; i++)
            {
                var clue = clues[i];
                var entry = Instantiate(clueEntryPrefab, clueListContainer);
                spawnedEntries.Add(entry);
                
                // Position manually
                RectTransform entryRect = entry.GetComponent<RectTransform>();
                if (entryRect != null)
                {
                    entryRect.anchorMin = new Vector2(0, 1);
                    entryRect.anchorMax = new Vector2(1, 1);
                    entryRect.pivot = new Vector2(0.5f, 1);
                    entryRect.anchoredPosition = new Vector2(0, -i * (entryHeight + entrySpacing));
                    entryRect.sizeDelta = new Vector2(0, entryHeight);
                }
                
                // Setup entry
                var entryComponent = entry.GetComponent<JournalClueEntry>();
                if (entryComponent != null)
                {
                    entryComponent.Setup(clue, this);
                }
                else
                {
                    // Fallback: try to find button and text
                    var button = entry.GetComponent<Button>();
                    var text = entry.GetComponentInChildren<TextMeshProUGUI>();
                    var icon = entry.GetComponentInChildren<Image>();
                    
                    if (text != null)
                        text.text = $"#{clue.clueNumber}: {clue.clueName}";
                    
                    if (icon != null && clue.icon != null)
                        icon.sprite = clue.icon;
                    
                    if (button != null)
                    {
                        var capturedClue = clue;
                        button.onClick.AddListener(() => SelectClue(capturedClue));
                    }
                }
            }
            
            // Resize content to fit all entries
            if (containerRect != null)
            {
                float totalHeight = clues.Count * (entryHeight + entrySpacing);
                containerRect.sizeDelta = new Vector2(containerRect.sizeDelta.x, totalHeight);
            }
            
            // Auto-select first clue if none selected
            if (selectedClue == null && clues.Count > 0)
            {
                SelectClue(clues[0]);
            }
        }
        
        public void SelectClue(ClueSO clue)
        {
            if (clue == null) return;
            
            selectedClue = clue;
            
            // Both panels stay visible - just update detail panel content
            if (clueDetailPanel != null) clueDetailPanel.SetActive(true);
            
            // Populate detail
            if (clueIcon != null)
            {
                clueIcon.sprite = clue.icon;
                clueIcon.gameObject.SetActive(clue.icon != null);
            }
            
            if (clueNameText != null)
                clueNameText.text = $"Clue #{clue.clueNumber}: {clue.clueName}";
            
            if (clueDescriptionText != null)
            {
                string desc = clue.description;
                if (!string.IsNullOrEmpty(clue.additionalDescription))
                    desc += $"\n\n<i>{clue.additionalDescription}</i>";
                clueDescriptionText.text = desc;
            }
            
            if (clueLocationText != null)
                clueLocationText.text = $"Found in: {clue.locationSceneId}";
            
            if (clueFoundByText != null)
            {
                string foundBy = clue.visibleTo switch
                {
                    ClueVisibility.ScorpionOnly => $"Scorpion ({clue.visibilityReason})",
                    ClueVisibility.FrogOnly => $"Frog ({clue.visibilityReason})",
                    _ => "Either detective"
                };
                clueFoundByText.text = $"Discovered by: {foundBy}";
            }
            
            OnClueSelected?.Invoke(clue);
        }
        
        #endregion
        
        #region Events
        
        private void OnNewClueFound(ClueSO clue)
        {
            // Could show a notification here
            Debug.Log($"[JournalUI] New clue added to journal: {clue.clueName}");
        }
        
        private void OpenDeductionBoard()
        {
            CloseJournal();
            DeductionBoardUI.Instance?.OpenBoard();
        }
        
        #endregion
    }
    
    /// <summary>
    /// Component for individual clue entries in the journal list.
    /// </summary>
    public class JournalClueEntry : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI locationText;
        
        private ClueSO clue;
        private JournalUI journal;
        
        public void Setup(ClueSO clue, JournalUI journal)
        {
            this.clue = clue;
            this.journal = journal;
            
            if (nameText != null)
                nameText.text = $"#{clue.clueNumber}: {clue.clueName}";
            
            if (locationText != null)
                locationText.text = clue.locationSceneId;
            
            if (icon != null && clue.icon != null)
                icon.sprite = clue.icon;
            
            if (button != null)
                button.onClick.AddListener(OnClick);
        }
        
        private void OnClick()
        {
            journal?.SelectClue(clue);
        }
    }
}