#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace PointClickDetective.Editor
{
    [CustomEditor(typeof(Interactable))]
    public class InteractableEditor : UnityEditor.Editor
    {
        private SerializedProperty objectId;
        private SerializedProperty displayName;
        private SerializedProperty scorpionData;
        private SerializedProperty frogData;
        private SerializedProperty belongsToSceneId;
        private SerializedProperty onLookedAt;
        private SerializedProperty onClueDiscovered;
        
        private bool showScorpionData = true;
        private bool showFrogData = true;
        private bool showEvents = false;
        
        private void OnEnable()
        {
            objectId = serializedObject.FindProperty("objectId");
            displayName = serializedObject.FindProperty("displayName");
            scorpionData = serializedObject.FindProperty("scorpionData");
            frogData = serializedObject.FindProperty("frogData");
            belongsToSceneId = serializedObject.FindProperty("belongsToSceneId");
            onLookedAt = serializedObject.FindProperty("OnLookedAt");
            onClueDiscovered = serializedObject.FindProperty("OnClueDiscovered");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Interactable Object", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Basic Info
            EditorGUILayout.PropertyField(objectId);
            EditorGUILayout.PropertyField(displayName);
            EditorGUILayout.PropertyField(belongsToSceneId);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Character-Specific Data", EditorStyles.boldLabel);
            
            // Scorpion Data
            EditorGUILayout.Space();
            showScorpionData = EditorGUILayout.Foldout(showScorpionData, "🦂 Scorpion Data", true, EditorStyles.foldoutHeader);
            if (showScorpionData)
            {
                EditorGUI.indentLevel++;
                DrawCharacterData(scorpionData);
                EditorGUI.indentLevel--;
            }
            
            // Frog Data
            EditorGUILayout.Space();
            showFrogData = EditorGUILayout.Foldout(showFrogData, "🐸 Frog Data", true, EditorStyles.foldoutHeader);
            if (showFrogData)
            {
                EditorGUI.indentLevel++;
                DrawCharacterData(frogData);
                EditorGUI.indentLevel--;
            }
            
            // Events
            EditorGUILayout.Space();
            showEvents = EditorGUILayout.Foldout(showEvents, "Events", true, EditorStyles.foldoutHeader);
            if (showEvents)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(onLookedAt);
                EditorGUILayout.PropertyField(onClueDiscovered);
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawCharacterData(SerializedProperty data)
        {
            var isVisible = data.FindPropertyRelative("isVisibleToThisCharacter");
            var conditionalDialogues = data.FindPropertyRelative("conditionalDialogues");
            var lookDialogue = data.FindPropertyRelative("lookAtDialogue");
            var lookPortrait = data.FindPropertyRelative("lookAtPortrait");
            var lookSequence = data.FindPropertyRelative("lookAtSequence");
            var prerequisites = data.FindPropertyRelative("prerequisites");
            var clueOnLook = data.FindPropertyRelative("clueOnLook");
            var triggersSceneChange = data.FindPropertyRelative("triggersSceneChange");
            var targetSceneId = data.FindPropertyRelative("targetSceneId");
            var unlockFlagOnLook = data.FindPropertyRelative("unlockFlagOnLook");
            
            // Visibility
            EditorGUILayout.PropertyField(isVisible, new GUIContent("Visible to Character"));
            
            if (!isVisible.boolValue)
            {
                EditorGUILayout.HelpBox("This object is hidden from this character.", MessageType.Info);
                return;
            }
            
            EditorGUILayout.Space();
            
            // Conditional Dialogues (Priority)
            EditorGUILayout.LabelField("Conditional Dialogues (Checked In Order)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(conditionalDialogues, new GUIContent("Dialogues"), true);
            
            if (conditionalDialogues.arraySize > 0)
            {
                EditorGUILayout.HelpBox("First matching conditional dialogue plays. If none match, falls back to below.", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            // Fallback Section
            EditorGUILayout.LabelField("Fallback Dialogue", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(lookSequence, new GUIContent("Dialogue Sequence"));
            
            bool hasLookSequence = lookSequence.objectReferenceValue != null;
            if (hasLookSequence)
            {
                EditorGUILayout.HelpBox("Using Dialogue Sequence as fallback.", MessageType.Info);
            }
            
            EditorGUI.BeginDisabledGroup(hasLookSequence);
            EditorGUILayout.PropertyField(lookDialogue, new GUIContent("Simple Dialogue"));
            EditorGUILayout.PropertyField(lookPortrait, new GUIContent("Portrait Override"));
            EditorGUILayout.PropertyField(clueOnLook, new GUIContent("Clue Discovered"));
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.PropertyField(unlockFlagOnLook, new GUIContent("Set Flag On Click"));
            
            EditorGUILayout.Space();
            
            // Scene Change
            EditorGUILayout.LabelField("Scene Transition", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(triggersSceneChange);
            if (triggersSceneChange.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(targetSceneId);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Prerequisites
            EditorGUILayout.PropertyField(prerequisites, new GUIContent("Prerequisites"), true);
        }
    }
    
    [CustomEditor(typeof(GameSceneContainer))]
    public class GameSceneContainerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Container", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            DrawDefaultInspector();
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Auto-Set Scene ID on Child Interactables"))
            {
                AutoSetSceneIds();
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void AutoSetSceneIds()
        {
            var container = target as GameSceneContainer;
            if (container == null) return;
            
            var interactables = container.GetComponentsInChildren<Interactable>(true);
            
            var containerSo = new SerializedObject(container);
            var containerSceneId = containerSo.FindProperty("sceneId");
            string sceneId = containerSceneId?.stringValue ?? "";
            
            int count = 0;
            foreach (var interactable in interactables)
            {
                var so = new SerializedObject(interactable);
                var sceneIdProp = so.FindProperty("belongsToSceneId");
                
                if (sceneIdProp != null)
                {
                    sceneIdProp.stringValue = sceneId;
                    so.ApplyModifiedProperties();
                    count++;
                }
            }
            
            Debug.Log($"Set scene ID '{sceneId}' on {count} interactables in {container.name}");
        }
    }
}
#endif