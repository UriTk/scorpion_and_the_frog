using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace PointClickDetective
{
    /// <summary>
    /// Duck Detective-style deduction board.
    /// Shows questions with inline dropdowns for answers.
    /// Answers become available based on discovered clues.
    /// </summary>
    public class DeductionBoardUI : MonoBehaviour
    {
        public static DeductionBoardUI Instance { get; private set; }
        
        [Header("Main Panel")]
        [SerializeField] private GameObject boardPanel;
        
        [Header("Question List")]
        [SerializeField] private Transform questionListContainer;
        [SerializeField] private GameObject questionEntryPrefab;
        
        [Header("Detail Panel (shown alongside list)")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private TextMeshProUGUI selectedQuestionText;
        [SerializeField] private TextMeshProUGUI questionDescriptionText;
        [SerializeField] private TMP_Dropdown answerDropdown;
        [SerializeField] private Button confirmAnswerButton;
        
        [Header("Feedback")]
        [SerializeField] private GameObject correctFeedback;
        [SerializeField] private GameObject incorrectFeedback;
        [SerializeField] private TextMeshProUGUI feedbackText;
        [SerializeField] private float feedbackDuration = 1.5f;
        
        [Header("Progress")]
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Slider progressSlider;
        
        [Header("Navigation")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button journalButton;
        
        [Header("Input")]
        [SerializeField] private Key toggleKey = Key.Tab;
        
        [Header("Question Data")]
        [SerializeField] private QuestionSO[] allQuestions;
        
        [Header("Events")]
        public UnityEvent OnBoardOpened;
        public UnityEvent OnBoardClosed;
        public UnityEvent<QuestionSO, bool> OnQuestionAnswered; // Question, wasCorrect
        public UnityEvent OnAllQuestionsAnswered;
        
        // State
        private bool isOpen;
        private QuestionSO selectedQuestion;
        private Dictionary<int, QuestionSO> questionLookup = new Dictionary<int, QuestionSO>();
        private Dictionary<int, string> answeredQuestions = new Dictionary<int, string>(); // questionId -> answer
        private HashSet<int> revealedQuestions = new HashSet<int>(); // Questions revealed via dialogue
        private List<GameObject> spawnedQuestionEntries = new List<GameObject>();
        private List<AnswerOption> currentAnswerOptions = new List<AnswerOption>();
        
        public bool IsOpen => isOpen;
        public int TotalQuestions => allQuestions?.Length ?? 0;
        public int AnsweredCorrectly => answeredQuestions.Count(kvp => IsAnswerCorrect(kvp.Key, kvp.Value));
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Build lookup
            if (allQuestions != null)
            {
                foreach (var q in allQuestions)
                {
                    if (q != null)
                    {
                        questionLookup[q.questionId] = q;
                    }
                }
            }
            
            OnBoardOpened ??= new UnityEvent();
            OnBoardClosed ??= new UnityEvent();
            OnQuestionAnswered ??= new UnityEvent<QuestionSO, bool>();
            OnAllQuestionsAnswered ??= new UnityEvent();
        }
        
        private void Start()
        {
            // Setup buttons
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseBoard);
            
            if (journalButton != null)
                journalButton.onClick.AddListener(OpenJournal);
            
            if (confirmAnswerButton != null)
                confirmAnswerButton.onClick.AddListener(ConfirmSelectedAnswer);
            
            // Initial state - ensure board is closed
            isOpen = false;
            if (boardPanel != null) boardPanel.SetActive(false);
            if (detailPanel != null) detailPanel.SetActive(false);
            HideFeedback();
            
            // Subscribe to clue changes to update available answers (AFTER setting isOpen = false)
            if (ClueManager.Instance != null)
            {
                ClueManager.Instance.OnCluesChanged.AddListener(RefreshQuestionList);
            }
        }
        
        private void OnDestroy()
        {
            if (ClueManager.Instance != null)
            {
                ClueManager.Instance.OnCluesChanged.RemoveListener(RefreshQuestionList);
            }
        }
        
        private bool inputReady = false;
        
        private void Update()
        {
            // Skip first frame - input system can report false positives
            if (!inputReady)
            {
                inputReady = true;
                return;
            }
            
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                if (isOpen)
                    CloseBoard();
                else
                    OpenBoard();
            }
            
            // Close with Escape
            if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseBoard();
            }
        }
        
        #region Public Methods
        
        public void OpenBoard()
        {
            Debug.Log($"[DeductionBoardUI] OpenBoard called. isOpen={isOpen}");
            
            if (isOpen) return;
            
            // Don't open during dialogue
            if (DialogueManager.Instance?.IsShowing == true) return;
            
            // Close other UIs
            JournalUI.Instance?.CloseJournal();
            WorldMapUI.Instance?.CloseMap();
            
            isOpen = true;
            Debug.Log($"[DeductionBoardUI] Opening board, setting isOpen=true");
            
            if (boardPanel != null)
                boardPanel.SetActive(true);
            
            RefreshQuestionList();
            UpdateProgress();
            HideFeedback();
            
            OnBoardOpened?.Invoke();
        }
        
        public void CloseBoard()
        {
            if (!isOpen) return;
            
            isOpen = false;
            
            if (boardPanel != null)
                boardPanel.SetActive(false);
            
            OnBoardClosed?.Invoke();
        }
        
        public void OpenDeductionBoard() => OpenBoard(); // Alias for compatibility
        
        /// <summary>
        /// Reveal a question, bypassing clue requirements.
        /// Used by dialogue triggers.
        /// </summary>
        public void RevealQuestion(QuestionSO question)
        {
            if (question == null) return;
            
            if (revealedQuestions.Add(question.questionId))
            {
                Debug.Log($"[DeductionBoardUI] Question #{question.questionId} revealed via trigger");
                
                if (isOpen)
                {
                    RefreshQuestionList();
                }
            }
        }
        
        /// <summary>
        /// Check if a question is unlocked (either via clues or revealed).
        /// </summary>
        public bool IsQuestionUnlocked(int questionId)
        {
            // Check if revealed via dialogue
            if (revealedQuestions.Contains(questionId))
                return true;
            
            // Check if unlocked via clues (at least one answer available)
            if (questionLookup.TryGetValue(questionId, out var question))
            {
                var foundClueIds = ClueManager.Instance?.FoundClueIds?.ToHashSet() ?? new HashSet<int>();
                return question.IsUnlocked(foundClueIds);
            }
            
            return false;
        }
        
        #endregion
        
        #region Question List
        
        [Header("Layout")]
        [SerializeField] private float entryHeight = 60f;
        [SerializeField] private float entrySpacing = 5f;
        
        public void RefreshQuestionList()
        {
            Debug.Log($"[DeductionBoardUI] RefreshQuestionList called. isOpen={isOpen}");
            
            // Only refresh if board is open
            if (!isOpen)
            {
                Debug.Log($"[DeductionBoardUI] Skipping refresh - board not open");
                return;
            }
            
            // Clear existing
            foreach (var entry in spawnedQuestionEntries)
            {
                if (entry != null) Destroy(entry);
            }
            spawnedQuestionEntries.Clear();
            
            if (questionListContainer == null || questionEntryPrefab == null) return;
            if (allQuestions == null) return;
            
            var foundClueIds = ClueManager.Instance?.FoundClueIds?.ToHashSet() ?? new HashSet<int>();
            
            // Only get questions that are unlocked (at least one answer available)
            var unlockedQuestions = allQuestions
                .Where(q => q != null && q.IsUnlocked(foundClueIds))
                .OrderBy(q => q.category)
                .ThenBy(q => q.displayOrder)
                .ToList();
            
            // Get container width for sizing entries
            RectTransform containerRect = questionListContainer as RectTransform;
            float containerWidth = containerRect != null ? containerRect.rect.width : 300f;
            
            for (int i = 0; i < unlockedQuestions.Count; i++)
            {
                var question = unlockedQuestions[i];
                var entry = Instantiate(questionEntryPrefab, questionListContainer);
                spawnedQuestionEntries.Add(entry);
                
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
                
                var entryComponent = entry.GetComponent<DeductionQuestionEntry>();
                if (entryComponent != null)
                {
                    bool isAnswered = answeredQuestions.ContainsKey(question.questionId);
                    bool isCorrect = isAnswered && IsAnswerCorrect(question.questionId, answeredQuestions[question.questionId]);
                    string currentAnswer = isAnswered ? answeredQuestions[question.questionId] : null;
                    
                    entryComponent.Setup(question, this, true, isAnswered, isCorrect, currentAnswer);
                }
                else
                {
                    SetupQuestionEntryFallback(entry, question, foundClueIds);
                }
            }
            
            // Resize content to fit all entries
            if (containerRect != null)
            {
                float totalHeight = unlockedQuestions.Count * (entryHeight + entrySpacing);
                containerRect.sizeDelta = new Vector2(containerRect.sizeDelta.x, totalHeight);
            }
            
            Debug.Log($"[DeductionBoardUI] Spawned {spawnedQuestionEntries.Count} unlocked questions (of {allQuestions.Length} total)");
            
            // Auto-select first unlocked question if none selected
            if (selectedQuestion == null)
            {
                var firstUnlocked = unlockedQuestions.FirstOrDefault();
                if (firstUnlocked != null)
                {
                    SelectQuestion(firstUnlocked);
                }
            }
            else
            {
                // Refresh the detail panel for currently selected question
                SelectQuestion(selectedQuestion);
            }
        }
        
        private void SetupQuestionEntryFallback(GameObject entry, QuestionSO question, HashSet<int> foundClueIds)
        {
            var button = entry.GetComponent<Button>();
            var text = entry.GetComponentInChildren<TextMeshProUGUI>();
            
            bool isUnlocked = IsQuestionUnlocked(question.questionId);
            bool isAnswered = answeredQuestions.ContainsKey(question.questionId);
            
            if (text != null)
            {
                if (!isUnlocked)
                {
                    text.text = $"<color=#666666>??? (Locked)</color>";
                }
                else if (isAnswered)
                {
                    string answer = answeredQuestions[question.questionId];
                    bool correct = IsAnswerCorrect(question.questionId, answer);
                    string color = correct ? "#00FF00" : "#FFFF00";
                    text.text = $"<color={color}>{question.questionText.Replace("___", answer)}</color>";
                }
                else
                {
                    text.text = question.questionText;
                }
            }
            
            if (button != null)
            {
                button.interactable = isUnlocked;
                var capturedQuestion = question;
                button.onClick.AddListener(() => SelectQuestion(capturedQuestion));
            }
        }
        
        #endregion
        
        #region Question Selection & Answers
        
        public void SelectQuestion(QuestionSO question)
        {
            if (question == null) return;
            
            // Check if unlocked
            if (!IsQuestionUnlocked(question.questionId))
            {
                Debug.Log($"[DeductionBoardUI] Question {question.questionId} is locked");
                return;
            }
            
            selectedQuestion = question;
            
            // Update detail panel
            if (detailPanel != null)
                detailPanel.SetActive(true);
            
            if (selectedQuestionText != null)
                selectedQuestionText.text = question.questionText;
            
            if (questionDescriptionText != null)
                questionDescriptionText.text = question.additionalDescription;
            
            // Populate dropdown with available answers
            PopulateAnswerDropdown(question);
            
            HideFeedback();
        }
        
        private void PopulateAnswerDropdown(QuestionSO question)
        {
            if (answerDropdown == null) return;
            
            var foundClueIds = ClueManager.Instance?.FoundClueIds?.ToHashSet() ?? new HashSet<int>();
            
            // Get available answers (shuffled)
            currentAnswerOptions = question.GetShuffledAvailableAnswers(foundClueIds);
            
            // Clear and populate dropdown
            answerDropdown.ClearOptions();
            
            var options = new List<string> { "Select an answer..." };
            options.AddRange(currentAnswerOptions.Select(a => a.answerText));
            
            answerDropdown.AddOptions(options);
            
            // If already answered, select that answer
            if (answeredQuestions.TryGetValue(question.questionId, out string currentAnswer))
            {
                int index = currentAnswerOptions.FindIndex(a => a.answerText == currentAnswer);
                if (index >= 0)
                {
                    answerDropdown.value = index + 1; // +1 for "Select an answer..."
                }
            }
            else
            {
                answerDropdown.value = 0;
            }
        }
        
        private void ConfirmSelectedAnswer()
        {
            if (selectedQuestion == null) return;
            if (answerDropdown == null) return;
            
            int selectedIndex = answerDropdown.value - 1; // -1 for "Select an answer..."
            
            if (selectedIndex < 0 || selectedIndex >= currentAnswerOptions.Count)
            {
                Debug.Log("[DeductionBoardUI] No answer selected");
                return;
            }
            
            string answer = currentAnswerOptions[selectedIndex].answerText;
            SubmitAnswer(answer);
        }
        
        public void SubmitAnswer(string answer)
        {
            if (selectedQuestion == null) return;
            
            // Store answer
            answeredQuestions[selectedQuestion.questionId] = answer;
            
            // Check if correct
            bool isCorrect = selectedQuestion.IsCorrectAnswer(answer);
            
            // Show feedback
            ShowFeedback(isCorrect);
            
            // Fire event
            OnQuestionAnswered?.Invoke(selectedQuestion, isCorrect);
            
            // Update UI
            RefreshQuestionList();
            UpdateProgress();
            
            // Set flag on correct
            if (isCorrect && !string.IsNullOrEmpty(selectedQuestion.setFlagOnCorrect))
            {
                GameManager.Instance?.SetFlag(selectedQuestion.setFlagOnCorrect);
            }
            
            // Trigger dialogue on answer (close board first so dialogue shows)
            DialogueSequenceSO dialogueToPlay = isCorrect 
                ? selectedQuestion.dialogueOnCorrect 
                : selectedQuestion.dialogueOnWrong;
            
            if (dialogueToPlay != null)
            {
                // Delay slightly so feedback shows first
                StartCoroutine(PlayDialogueAfterDelay(dialogueToPlay, 0.5f));
            }
            
            // Check if all questions answered correctly
            if (AnsweredCorrectly == TotalQuestions)
            {
                OnAllQuestionsAnswered?.Invoke();
            }
            
            Debug.Log($"[DeductionBoardUI] Answered Q{selectedQuestion.questionId}: {answer} - {(isCorrect ? "CORRECT" : "INCORRECT")}");
        }
        
        private System.Collections.IEnumerator PlayDialogueAfterDelay(DialogueSequenceSO dialogue, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Close board before showing dialogue
            CloseBoard();
            
            yield return null; // Wait a frame
            
            DialogueManager.Instance?.ShowDialogueSequence(dialogue);
        }
        
        #endregion
        
        #region Feedback
        
        private void ShowFeedback(bool correct)
        {
            if (correctFeedback != null)
                correctFeedback.SetActive(correct);
            
            if (incorrectFeedback != null)
                incorrectFeedback.SetActive(!correct);
            
            if (feedbackText != null)
            {
                feedbackText.text = correct ? "Correct!" : "Hmm, that doesn't seem right...";
                feedbackText.color = correct ? Color.green : Color.yellow;
            }
            
            // Auto-hide feedback after delay
            StartCoroutine(HideFeedbackAfterDelay());
        }
        
        private System.Collections.IEnumerator HideFeedbackAfterDelay()
        {
            yield return new WaitForSeconds(feedbackDuration);
            HideFeedback();
        }
        
        private void HideFeedback()
        {
            if (correctFeedback != null) correctFeedback.SetActive(false);
            if (incorrectFeedback != null) incorrectFeedback.SetActive(false);
        }
        
        #endregion
        
        #region Progress
        
        private void UpdateProgress()
        {
            int total = TotalQuestions;
            int correct = AnsweredCorrectly;
            int answered = answeredQuestions.Count;
            
            if (progressText != null)
            {
                progressText.text = $"Progress: {correct}/{total} correct ({answered} answered)";
            }
            
            if (progressSlider != null)
            {
                progressSlider.maxValue = total;
                progressSlider.value = correct;
            }
        }
        
        #endregion
        
        #region Helpers
        
        private bool IsAnswerCorrect(int questionId, string answer)
        {
            if (questionLookup.TryGetValue(questionId, out var question))
            {
                return question.IsCorrectAnswer(answer);
            }
            return false;
        }
        
        private void OpenJournal()
        {
            CloseBoard();
            JournalUI.Instance?.OpenJournal();
        }
        
        #endregion
        
        #region Save/Load
        
        [System.Serializable]
        public class DeductionSaveData
        {
            public Dictionary<int, string> answers = new Dictionary<int, string>();
            public List<int> revealedQuestionIds = new List<int>();
        }
        
        public DeductionSaveData GetSaveData()
        {
            return new DeductionSaveData
            {
                answers = new Dictionary<int, string>(answeredQuestions),
                revealedQuestionIds = revealedQuestions.ToList()
            };
        }
        
        public void LoadSaveData(DeductionSaveData data)
        {
            answeredQuestions.Clear();
            revealedQuestions.Clear();
            
            if (data != null)
            {
                if (data.answers != null)
                {
                    foreach (var kvp in data.answers)
                    {
                        answeredQuestions[kvp.Key] = kvp.Value;
                    }
                }
                
                if (data.revealedQuestionIds != null)
                {
                    foreach (var id in data.revealedQuestionIds)
                    {
                        revealedQuestions.Add(id);
                    }
                }
            }
            
            if (isOpen)
            {
                RefreshQuestionList();
                UpdateProgress();
            }
        }
        
        // Legacy methods for backwards compatibility
        public Dictionary<int, string> GetAnswersForSave()
        {
            return new Dictionary<int, string>(answeredQuestions);
        }
        
        public void LoadAnswers(Dictionary<int, string> answers)
        {
            answeredQuestions.Clear();
            
            if (answers != null)
            {
                foreach (var kvp in answers)
                {
                    answeredQuestions[kvp.Key] = kvp.Value;
                }
            }
            
            if (isOpen)
            {
                RefreshQuestionList();
                UpdateProgress();
            }
        }
        
        public void ClearAllAnswers()
        {
            answeredQuestions.Clear();
            revealedQuestions.Clear();
            
            if (isOpen)
            {
                RefreshQuestionList();
                UpdateProgress();
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Component for question entries in the deduction board.
    /// </summary>
    public class DeductionQuestionEntry : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject lockedIcon;
        [SerializeField] private GameObject answeredIcon;
        [SerializeField] private GameObject correctIcon;
        
        [Header("Colors")]
        [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private Color unlockedColor = Color.white;
        [SerializeField] private Color answeredColor = new Color(1f, 1f, 0.5f);
        [SerializeField] private Color correctColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color incorrectTextColor = new Color(1f, 0.5f, 0.5f);
        [SerializeField] private Color correctTextColor = new Color(0.5f, 1f, 0.5f);
        
        private QuestionSO question;
        private DeductionBoardUI board;
        
        public void Setup(QuestionSO question, DeductionBoardUI board, bool isUnlocked, bool isAnswered, bool isCorrect, string currentAnswer)
        {
            this.question = question;
            this.board = board;
            
            // Set question text
            if (questionText != null)
            {
                if (!isUnlocked)
                {
                    questionText.text = "???";
                    questionText.color = lockedColor;
                }
                else if (isAnswered && !string.IsNullOrEmpty(currentAnswer))
                {
                    questionText.text = question.questionText.Replace("___", $"<b>{currentAnswer}</b>");
                    questionText.color = isCorrect ? correctTextColor : incorrectTextColor;
                }
                else
                {
                    questionText.text = question.questionText;
                    questionText.color = unlockedColor;
                }
            }
            
            // Set status
            if (statusText != null)
            {
                if (!isUnlocked)
                    statusText.text = "Find more clues to unlock";
                else if (isCorrect)
                    statusText.text = "✓ Solved";
                else if (isAnswered)
                    statusText.text = "? Review your answer";
                else
                    statusText.text = "Click to answer";
            }
            
            // Set icons
            if (lockedIcon != null) lockedIcon.SetActive(!isUnlocked);
            if (answeredIcon != null) answeredIcon.SetActive(isAnswered && !isCorrect);
            if (correctIcon != null) correctIcon.SetActive(isCorrect);
            
            // Set background color
            if (backgroundImage != null)
            {
                if (!isUnlocked)
                    backgroundImage.color = lockedColor;
                else if (isCorrect)
                    backgroundImage.color = correctColor;
                else if (isAnswered)
                    backgroundImage.color = answeredColor;
                else
                    backgroundImage.color = unlockedColor;
            }
            
            // Setup button
            if (button != null)
            {
                button.interactable = isUnlocked;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
            }
        }
        
        private void OnClick()
        {
            board?.SelectQuestion(question);
        }
    }
}