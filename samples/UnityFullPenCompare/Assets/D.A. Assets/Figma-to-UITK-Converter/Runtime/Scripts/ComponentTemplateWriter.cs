#if FCU_EXISTS
using DA_Assets.FCU.Model;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DA_Assets.FCU
{
    [Serializable]
    public class ComponentTemplateWriter : FcuBase
    {
        /// <summary>
        /// Maps a UITK template variant key to the generated template file.
        /// Only populated for NodeType.INSTANCE / COMPONENT nodes.
        /// </summary>
        private readonly Dictionary<string, (string alias, string templatePath)> _registry
            = new Dictionary<string, (string alias, string templatePath)>();

        /// <summary>
        /// Returns true if a template for this UITK variant is already registered.
        /// Safe to call for any node type - only INSTANCE / COMPONENT nodes are ever registered.
        /// </summary>
        public bool TryGetRegistered(string variantKey, out string alias, out string templatePath)
        {
            alias = null;
            templatePath = null;

            if (string.IsNullOrEmpty(variantKey))
                return false;

            if (!_registry.TryGetValue(variantKey, out var entry))
                return false;

            alias = entry.alias;
            templatePath = entry.templatePath;
            return true;
        }

        /// <summary>
        /// Registers a generated template for the given UITK template variant.
        /// </summary>
        public void Register(string variantKey, string alias, string templatePath)
        {
            if (string.IsNullOrEmpty(variantKey))
                return;

            if (!_registry.ContainsKey(variantKey))
                _registry[variantKey] = (alias, templatePath);
        }

        /// <summary>
        /// Clears all registered templates. Call before each import.
        /// </summary>
        public void Clear()
        {
            _registry.Clear();
            Debug.Log("[UITK] ComponentTemplateWriter registry cleared.");
        }

        /// <summary>True when any templates have been registered.</summary>
        public bool HasAny => _registry.Count > 0;
    }
}
#endif
