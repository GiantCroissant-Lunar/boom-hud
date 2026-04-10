#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DA_Assets.FCU.Editor
{
    /// <summary>
    /// Patches Unity UI Builder so that "Match Game View" is enabled by default.
    /// Applied globally every time the Unity Editor starts.
    /// Uses reflection to access internal Unity.UI.Builder APIs.
    /// </summary>
    [InitializeOnLoad]
    internal static class UiBuilderMatchGameViewPatch
    {
        static UiBuilderMatchGameViewPatch()
        {
            // Defer until Editor is fully initialized.
            EditorApplication.delayCall += Apply;
        }

        private static void Apply()
        {
            // Try to find the active UI Builder window (if open).
            var builderWindowType = Type.GetType("Unity.UI.Builder.Builder, UnityEditor.UIBuilderModule");
            if (builderWindowType == null)
            {
                // UI Builder module not loaded — nothing to patch.
                return;
            }

            // Find the static canvas settings stored in BuilderDocument.
            // The "matchGameView" flag lives on the Builder's canvas element.
            var builderDocumentType = Type.GetType("Unity.UI.Builder.BuilderDocument, UnityEditor.UIBuilderModule");
            if (builderDocumentType == null) return;

            // BuilderDocument is a ScriptableObject singleton accessible via "instance".
            var instanceProp = builderDocumentType.GetProperty(
                "instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            object document = instanceProp?.GetValue(null);
            if (document == null) return;

            // document.settings.CanvasTheme / CanvasMatchGameView
            var settingsProp = builderDocumentType.GetProperty(
                "settings",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            object settings = settingsProp?.GetValue(document);
            if (settings == null) return;

            var matchField = settings.GetType().GetField(
                "matchGameView",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (matchField == null)
            {
                // Try property instead of field.
                var matchProp = settings.GetType().GetProperty(
                    "MatchGameView",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (matchProp != null && matchProp.CanWrite)
                {
                    matchProp.SetValue(settings, true);
                    Debug.Log("[FCU] UI Builder: Match Game View enabled.");
                }
                else
                {
                    Debug.LogWarning("[FCU] UI Builder: could not find matchGameView field/property.");
                }

                return;
            }

            matchField.SetValue(settings, true);
            Debug.Log("[FCU] UI Builder: Match Game View enabled.");
        }
    }
}
#endif
