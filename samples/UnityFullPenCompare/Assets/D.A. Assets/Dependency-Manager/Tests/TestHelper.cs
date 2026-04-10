using System.IO;
using UnityEditor;
using UnityEngine;

namespace DA_Assets.DM.Tests
{
    /// <summary>
    /// Utilities for creating and cleaning up temporary assets used in DependencyManager tests.
    /// All temp assets are created under Assets/_DMTestTemp/ and must be cleaned up in TearDown.
    /// Delegates to DependencyManager methods where possible to avoid duplicating production logic.
    /// </summary>
    public static class TestHelper
    {
        public const string TempRoot = "Assets/_DMTestTemp";

        /// <summary>
        /// Ensures the temp root directory exists.
        /// </summary>
        public static void EnsureTempDirectory()
        {
            string fullPath = Path.GetFullPath(TempRoot);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                AssetDatabase.ImportAsset(TempRoot);
            }
        }

        /// <summary>
        /// Ensures a subdirectory under temp root exists.
        /// Returns the asset-relative path (e.g. "Assets/_DMTestTemp/Sub").
        /// </summary>
        public static string EnsureSubDirectory(string subDir)
        {
            string assetPath = $"{TempRoot}/{subDir}";
            string fullPath = Path.GetFullPath(assetPath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                AssetDatabase.ImportAsset(assetPath);
            }
            return assetPath;
        }

        /// <summary>
        /// Creates a temp DependencyItem ScriptableObject asset.
        /// </summary>
        public static DependencyItem CreateTempItem(
            string name,
            string typeAndAssembly,
            string symbol,
            string generatedAsmdefName = null,
            string[] expectedPaths = null,
            string[] unexpectedPaths = null)
        {
            EnsureTempDirectory();

            var item = ScriptableObject.CreateInstance<DependencyItem>();
            item.TypeAndAssembly = typeAndAssembly;
            item.ScriptingDefineSymbol = symbol;
            item.GeneratedAsmdefName = generatedAsmdefName;
            item.ExpectedPaths = expectedPaths;
            item.UnexpectedPaths = unexpectedPaths;

            string assetPath = $"{TempRoot}/{name}.asset";
            AssetDatabase.CreateAsset(item, assetPath);
            AssetDatabase.SaveAssets();
            return item;
        }

        /// <summary>
        /// Creates a temp .cs script file with the given namespace and type name.
        /// Returns the asset-relative path.
        /// </summary>
        public static string CreateTempScript(string subDir, string typeName, string namespaceName)
        {
            string dir = EnsureSubDirectory(subDir);
            string content = $"namespace {namespaceName}\n{{\n    public class {typeName} : UnityEngine.MonoBehaviour {{ }}\n}}\n";
            string filePath = Path.GetFullPath($"{dir}/{typeName}.cs");
            File.WriteAllText(filePath, content);

            string assetPath = $"{dir}/{typeName}.cs";
            AssetDatabase.ImportAsset(assetPath);
            return assetPath;
        }

        /// <summary>
        /// Creates a temp .asmdef file using DependencyManager.CreateGeneratedAsmdef.
        /// Returns the asset-relative path.
        /// </summary>
        public static string CreateTempAsmdef(string subDir, string assemblyName)
        {
            string dir = EnsureSubDirectory(subDir);
            return DependencyManager.CreateGeneratedAsmdef(dir, assemblyName);
        }

        /// <summary>
        /// Deletes all temp assets. Must be called in TearDown.
        /// </summary>
        public static void CleanupTempAssets()
        {
            string fullPath = Path.GetFullPath(TempRoot);
            if (Directory.Exists(fullPath))
            {
                AssetDatabase.DeleteAsset(TempRoot);
            }

            AssetDatabase.Refresh();
        }
    }
}
