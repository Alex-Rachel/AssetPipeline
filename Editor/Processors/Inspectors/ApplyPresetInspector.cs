using AssetPipeline.Import;
using UnityEditor;
using UnityEngine;

namespace AssetPipeline.Processors
{
    [CustomEditor(typeof(ApplyPreset))]
    internal class ApplyPresetInspector : AssetProcessorInspector
    {
        SerializedProperty m_Preset;
        Editor m_CachedEditor;

        protected SerializedProperty m_IgnoreBuildSettingsOrderProperty;

        public static readonly GUIContent guiIgnoreBuildSettingsOrder = new GUIContent("忽略BuildSettings顺序");

        protected override void OnEnable()
        {
            m_Preset = serializedObject.FindProperty("preset");
            m_IgnoreBuildSettingsOrderProperty = serializedObject.FindProperty("IgnoreBuildSettingsOrder");
            
            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
#if false
            // EditorGUILayout.HelpBox("This processor will only execute when a new asset is added.", MessageType.Warning);
            // EditorGUILayout.Space();
#else
            EditorGUILayout.PropertyField(m_RunOnImportProperty, DaiGUIContent.runOnEveryImport);
            if (!m_RunOnImportProperty.boolValue)
            {
                EditorGUILayout.HelpBox("This processor will only execute when a new asset is added.\nTo run on every import, tick the Run On Every Import toggle above.", MessageType.Warning);
            }
            
            EditorGUILayout.PropertyField(m_IgnoreBuildSettingsOrderProperty, guiIgnoreBuildSettingsOrder);
            if (!m_IgnoreBuildSettingsOrderProperty.boolValue)
            {
                EditorGUILayout.HelpBox("开启此选型会忽略BuildSetting顺序", MessageType.Warning);
            }
            
            EditorGUILayout.Space();
#endif
            
            if (!m_CachedEditor)
            {
                CreateCachedEditor(m_Preset.objectReferenceValue, System.Type.GetType("UnityEditor.Presets.PresetEditor, UnityEditor"), ref m_CachedEditor);
            }

            m_CachedEditor.OnInspectorGUI();

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}