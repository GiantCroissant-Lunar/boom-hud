using UnityEditor;
using UnityEngine;

namespace DA_Assets.DM
{
    [CustomEditor(typeof(DependencyItem))]
    public class DependencyItemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            DependencyItem item = (DependencyItem)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(DependencyManagerLocKey.label_current_status.Localize(), EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle(DependencyManagerLocKey.label_is_enabled.Localize(), item.IsEnabled);

                // Resolve path from GUID at display time — never serialized.
                string resolvedPath = string.IsNullOrEmpty(item.ScriptGuid)
                    ? "-"
                    : AssetDatabase.GUIDToAssetPath(item.ScriptGuid);

                EditorGUILayout.TextField(DependencyManagerLocKey.label_script_path.Localize(), resolvedPath);
                EditorGUILayout.TextField(DependencyManagerLocKey.label_status.Localize(), item.StatusLabel ?? DependencyManager.PathNotFound);
                EditorGUILayout.Toggle(DependencyManagerLocKey.label_removed_manually.Localize(), item.DisabledManually);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(DependencyManagerLocKey.label_check_dependency_now.Localize()))
            {
                DependencyManager.CheckSingleItem(item);
            }
        }
    }
}
