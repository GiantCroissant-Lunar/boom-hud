using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DA_Assets.FCU
{
    /// <summary>
    /// Extension methods that add per-field Reset-to-Default support to UITK fields.
    /// Uses inline styles for the modified-indicator (no USS dependency).
    /// </summary>
    internal static class FieldResetExtensions
    {
        private static readonly Color IndicatorColor = new Color(0.25f, 0.56f, 0.87f, 1f); // Blue

        // -----------------------------------------------------------------------
        // BaseField<TValue>: bool, int, float, string, Vector*, etc.
        // -----------------------------------------------------------------------

        public static void AddResetMenu<TSettings, TValue>(
            this BaseField<TValue> field,
            TSettings current,
            TSettings defaults,
            Func<TSettings, TValue> getter,
            Action<TSettings, TValue> setter)
            where TValue : IEquatable<TValue>
        {
            // Apply initial indicator.
            SetModifiedIndicator(field, !EqualityComparer<TValue>.Default.Equals(getter(current), getter(defaults)));

            // Context menu.
            field.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                var defVal = getter(defaults);
                var curVal = getter(current);
                bool isDefault = EqualityComparer<TValue>.Default.Equals(curVal, defVal);

                evt.menu.AppendAction(
                    $"Reset to Default ({FormatValue(defVal)})",
                    _ =>
                    {
                        setter(current, defVal);
                        field.SetValueWithoutNotify(defVal);
                        SetModifiedIndicator(field, false);
                    },
                    isDefault
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
            }));

            // Update indicator on every value change.
            field.RegisterValueChangedCallback(evt =>
            {
                bool modified = !EqualityComparer<TValue>.Default.Equals(evt.newValue, getter(defaults));
                SetModifiedIndicator(field, modified);
            });
        }

        // -----------------------------------------------------------------------
        // EnumField / EnumFlagsField
        // -----------------------------------------------------------------------

        public static void AddResetMenu<TSettings, TEnum>(
            this EnumField field,
            TSettings current,
            TSettings defaults,
            Func<TSettings, TEnum> getter,
            Action<TSettings, TEnum> setter)
            where TEnum : struct, Enum
            => AddEnumResetMenuCore(field, current, defaults, getter, setter);

        public static void AddResetMenu<TSettings, TEnum>(
            this EnumFlagsField field,
            TSettings current,
            TSettings defaults,
            Func<TSettings, TEnum> getter,
            Action<TSettings, TEnum> setter)
            where TEnum : struct, Enum
            => AddEnumResetMenuCore(field, current, defaults, getter, setter);

        private static void AddEnumResetMenuCore<TSettings, TEnum>(
            BaseField<Enum> field,
            TSettings current,
            TSettings defaults,
            Func<TSettings, TEnum> getter,
            Action<TSettings, TEnum> setter)
            where TEnum : struct, Enum
        {
            // Apply initial indicator.
            SetModifiedIndicator(field, !EqualityComparer<TEnum>.Default.Equals(getter(current), getter(defaults)));

            // Context menu.
            field.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                var def = getter(defaults);
                var cur = getter(current);
                bool isDefault = EqualityComparer<TEnum>.Default.Equals(cur, def);

                evt.menu.AppendAction(
                    $"Reset to Default ({FormatValue(def)})",
                    _ =>
                    {
                        setter(current, def);
                        field.SetValueWithoutNotify(def);
                        SetModifiedIndicator(field, false);
                    },
                    isDefault
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
            }));

            // Update indicator on change.
            field.RegisterValueChangedCallback(evt =>
            {
                bool modified = !EqualityComparer<TEnum>.Default.Equals(
                    (TEnum)(object)evt.newValue, getter(defaults));
                SetModifiedIndicator(field, modified);
            });
        }

        // -----------------------------------------------------------------------
        // Section (tab) reset
        // -----------------------------------------------------------------------

        public static void AddSectionResetMenu(this VisualElement header, Action resetAll)
        {
            header.AddManipulator(new ContextualMenuManipulator(evt =>
                evt.menu.AppendAction("Reset Section to Defaults", _ => resetAll())));
        }

        // -----------------------------------------------------------------------
        // DropdownField<string> — e.g. Language selector stored in FcuConfig
        // -----------------------------------------------------------------------

        /// <summary>
        /// Adds reset support to a <see cref="DropdownField"/> that has a string value
        /// but whose backing store may be anything (e.g. FcuConfig property).
        /// </summary>
        public static void AddDropdownResetMenu(
            this DropdownField field,
            Func<string> getter,
            string defaultValue,
            Action<string> setter)
        {
            SetModifiedIndicator(field, getter() != defaultValue);

            field.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                string cur = getter();
                bool isDefault = cur == defaultValue;

                evt.menu.AppendAction(
                    $"Reset to Default ({defaultValue})",
                    _ =>
                    {
                        setter(defaultValue);
                        field.SetValueWithoutNotify(defaultValue);
                        SetModifiedIndicator(field, false);
                    },
                    isDefault
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
            }));

            field.RegisterValueChangedCallback(evt =>
                SetModifiedIndicator(field, evt.newValue != defaultValue));
        }

        // -----------------------------------------------------------------------
        // Folder input (custom VisualElement container with an inner TextField)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Adds a "Reset to Default" context-menu entry and modified-indicator
        /// to a folder-input container produced by uitk.CreateFolderInput().
        /// </summary>
        /// <param name="container">The VisualElement returned by CreateFolderInput.</param>
        /// <param name="getter">Returns the current backing value (re-evaluated each time).</param>
        /// <param name="defaultValue">The default path string.</param>
        /// <param name="setter">Writes the new value to the backing field.</param>
        public static void AddFolderResetMenu(
            this VisualElement container,
            Func<string> getter,
            string defaultValue,
            Action<string> setter)
        {
            // Initial indicator state.
            SetModifiedIndicator(container, getter() != defaultValue);

            container.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                string cur = getter();
                bool isDefault = cur == defaultValue;

                evt.menu.AppendAction(
                    $"Reset to Default ({(string.IsNullOrEmpty(defaultValue) ? "<empty>" : defaultValue)})",
                    _ =>
                    {
                        setter(defaultValue);
                        // Update the inner TextField so the UI reflects the reset.
                        var tf = container.Q<TextField>();
                        tf?.SetValueWithoutNotify(defaultValue);
                        SetModifiedIndicator(container, false);
                    },
                    isDefault
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
            }));

            // Re-evaluate indicator when the inner TextField changes.
            container.RegisterCallback<ChangeEvent<string>>(evt =>
                SetModifiedIndicator(container, evt.newValue != defaultValue), TrickleDown.TrickleDown);
        }

        // -----------------------------------------------------------------------
        // PopupField<string> where backing value ≠ displayed string
        // (e.g. shader: displays name, stores Shader object)
        // -----------------------------------------------------------------------

        /// <summary>
        /// General string-field reset for cases where the backing value is a string
        /// but accessed via a custom getter/setter (e.g. FcuConfig property).
        /// Works with <see cref="TextField"/>, <see cref="PopupField{T}"/>, etc.
        /// </summary>
        public static void AddPopupResetMenu(
            this BaseField<string> field,
            Func<string> getter,
            string defaultValue,
            Action<string> setter)
        {
            // Initial indicator state.
            SetModifiedIndicator(field, getter() != defaultValue);

            field.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                string cur = getter();
                bool isDefault = cur == defaultValue;

                evt.menu.AppendAction(
                    $"Reset to Default ({defaultValue})",
                    _ =>
                    {
                        setter(defaultValue);
                        field.SetValueWithoutNotify(defaultValue);
                        SetModifiedIndicator(field, false);
                    },
                    isDefault
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
            }));

            field.RegisterValueChangedCallback(evt =>
                SetModifiedIndicator(field, evt.newValue != defaultValue));
        }

        // -----------------------------------------------------------------------
        // Inline style indicator — always works, no USS needed
        // -----------------------------------------------------------------------

        /// <summary>
        /// Public entry point for callers that can't use the TValue-generic overload
        /// (e.g. EnumFlagsField with FcuConfig-backed value).
        /// </summary>
        public static void SetModifiedIndicatorPublic(VisualElement field, bool modified)
            => SetModifiedIndicator(field, modified);

        private static void SetModifiedIndicator(VisualElement field, bool modified)
        {
            if (modified)
            {
                field.style.borderLeftColor = IndicatorColor;
                field.style.borderLeftWidth = 3f;
                field.style.paddingLeft = 4f;
            }
            else
            {
                field.style.borderLeftColor = StyleKeyword.Null;
                field.style.borderLeftWidth = StyleKeyword.Null;
                field.style.paddingLeft = StyleKeyword.Null;
            }
        }

        private static string FormatValue<T>(T value) => value switch
        {
            Color c      => $"#{ColorUtility.ToHtmlStringRGBA(c)}",
            float f      => f.ToString("F2"),
            double d     => d.ToString("F2"),
            Vector2 v    => $"({v.x:F1}, {v.y:F1})",
            Vector2Int v => $"({v.x}, {v.y})",
            Vector4 v    => $"({v.x:F1}, {v.y:F1}, {v.z:F1}, {v.w:F1})",
            null         => "null",
            _            => value.ToString()
        };
    }
}
