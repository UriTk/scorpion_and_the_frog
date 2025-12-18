using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PointClickDetective
{
    public class DialogueSequenceGenerator : EditorWindow
    {
        private TextAsset csvFile;
        private string outputFolder = "Assets/ScriptableObjects/Dialogue";
        private Dictionary<string, SpeakerSO> speakerLookup = new Dictionary<string, SpeakerSO>();
        
        [MenuItem("Tools/Point Click Detective/Generate Dialogue from CSV")]
        public static void ShowWindow()
        {
            GetWindow<DialogueSequenceGenerator>("Dialogue Generator");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Dialogue Sequence Generator", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "CSV Format:\n" +
                "SequenceID, LineIndex, Speaker, SpeakerDisplayName, Dialogue, Portrait, " +
                "TriggerFlag, TriggerScene, TriggerOnInteractable, TriggerConditionFlag, " +
                "TriggerConditionNotFlag, IsThought, AutoAdvance, TypewriterSpeed",
                MessageType.Info);
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Load Speakers from Project"))
            {
                LoadSpeakers();
            }
            
            GUILayout.Label($"Loaded Speakers: {speakerLookup.Count}");
            
            GUILayout.Space(10);
            
            EditorGUI.BeginDisabledGroup(csvFile == null);
            if (GUILayout.Button("Generate Dialogue Sequences"))
            {
                GenerateDialogueSequences();
            }
            EditorGUI.EndDisabledGroup();
        }
        
        private void LoadSpeakers()
        {
            speakerLookup.Clear();
            
            string[] guids = AssetDatabase.FindAssets("t:SpeakerSO");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SpeakerSO speaker = AssetDatabase.LoadAssetAtPath<SpeakerSO>(path);
                if (speaker != null && !string.IsNullOrEmpty(speaker.speakerName))
                {
                    speakerLookup[speaker.speakerName.ToLower()] = speaker;
                    Debug.Log($"Loaded speaker: {speaker.speakerName}");
                }
            }
            
            Debug.Log($"Loaded {speakerLookup.Count} speakers");
        }
        
        private void GenerateDialogueSequences()
        {
            if (csvFile == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a CSV file", "OK");
                return;
            }
            
            // Ensure output folder exists
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                string[] folders = outputFolder.Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    string newPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = newPath;
                }
            }
            
            // Parse CSV
            var csvLines = ParseCSV(csvFile.text);
            if (csvLines.Count <= 1)
            {
                EditorUtility.DisplayDialog("Error", "CSV file is empty or has no data rows", "OK");
                return;
            }
            
            // Group by SequenceID
            var sequences = new Dictionary<string, List<List<string>>>();
            
            for (int i = 1; i < csvLines.Count; i++) // Skip header
            {
                var row = csvLines[i];
                if (row.Count < 5) continue;
                
                string sequenceId = row[0].Trim();
                if (string.IsNullOrEmpty(sequenceId)) continue;
                
                if (!sequences.ContainsKey(sequenceId))
                {
                    sequences[sequenceId] = new List<List<string>>();
                }
                sequences[sequenceId].Add(row);
            }
            
            int created = 0;
            int updated = 0;
            
            foreach (var kvp in sequences)
            {
                string sequenceId = kvp.Key;
                var dialogueRows = kvp.Value.OrderBy(r => int.TryParse(r[1], out int idx) ? idx : 0).ToList();
                
                // Check if sequence already exists
                string assetPath = $"{outputFolder}/Dialogue_{sequenceId}.asset";
                DialogueSequenceSO sequence = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(assetPath);
                
                bool isNew = sequence == null;
                if (isNew)
                {
                    sequence = ScriptableObject.CreateInstance<DialogueSequenceSO>();
                }
                
                // Parse trigger conditions from first row for completion flag
                var firstRow = dialogueRows[0];
                if (firstRow.Count > 6 && !string.IsNullOrEmpty(firstRow[6]))
                    sequence.setFlagOnComplete = firstRow[6].Trim();
                
                // Create dialogue lines
                sequence.lines = new List<DialogueLine>();
                
                foreach (var row in dialogueRows)
                {
                    var line = new DialogueLine();
                    
                    // Speaker (column 2)
                    string speakerName = row.Count > 2 ? row[2].Trim().ToLower() : "";
                    if (!string.IsNullOrEmpty(speakerName) && speakerLookup.TryGetValue(speakerName, out SpeakerSO speaker))
                    {
                        line.speaker = speaker;
                    }
                    
                    // Dialogue text (column 4)
                    line.text = row.Count > 4 ? row[4].Trim() : "";
                    
                    // SetFlagOnShow (column 6) - for individual lines after first
                    if (row.Count > 6 && !string.IsNullOrEmpty(row[6]) && row != firstRow)
                    {
                        line.setFlagOnShow = row[6].Trim();
                    }
                    
                    // TriggerSceneChange (column 7)
                    if (row.Count > 7 && !string.IsNullOrEmpty(row[7]))
                    {
                        line.triggerSceneChange = row[7].Trim();
                    }
                    
                    // RequiresFlag (column 9)
                    if (row.Count > 9 && !string.IsNullOrEmpty(row[9]))
                    {
                        line.requiresFlag = row[9].Trim();
                    }
                    
                    // SkipIfFlag (column 10)
                    if (row.Count > 10 && !string.IsNullOrEmpty(row[10]))
                    {
                        line.skipIfFlag = row[10].Trim();
                    }
                    
                    sequence.lines.Add(line);
                }
                
                if (isNew)
                {
                    AssetDatabase.CreateAsset(sequence, assetPath);
                    created++;
                }
                else
                {
                    EditorUtility.SetDirty(sequence);
                    updated++;
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Complete", 
                $"Created {created} new sequences\nUpdated {updated} existing sequences", "OK");
        }
        
        private List<List<string>> ParseCSV(string csvText)
        {
            var result = new List<List<string>>();
            var lines = csvText.Split('\n');
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var row = new List<string>();
                bool inQuotes = false;
                string currentField = "";
                
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    
                    if (c == '"')
                    {
                        if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentField += '"';
                            i++;
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                        }
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        row.Add(currentField);
                        currentField = "";
                    }
                    else if (c != '\r')
                    {
                        currentField += c;
                    }
                }
                
                row.Add(currentField);
                result.Add(row);
            }
            
            return result;
        }
    }
}