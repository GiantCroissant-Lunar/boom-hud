using System.Collections.Generic;
using NUnit.Framework;

namespace DA_Assets.DM.Tests
{
    public class DefineManagementTests
    {
        private List<string> _originalDefines;

        [SetUp]
        public void SetUp()
        {
            // Save original defines to restore after each test.
            _originalDefines = DependencyManager.GetDefines();
        }

        [Test]
        public void GetDefines_ReturnsNonNullList()
        {
            var defines = DependencyManager.GetDefines();
            Assert.IsNotNull(defines);
        }

        [Test]
        public void SetDefines_AddsAndRetrievesSymbol()
        {
            var currentDefines = DependencyManager.GetDefines();
            string testSymbol = "DM_TEST_DEFINE_XYZ_12345";

            currentDefines.Add(testSymbol);
            DependencyManager.SetDefines(currentDefines);

            var updatedDefines = DependencyManager.GetDefines();
            Assert.IsTrue(updatedDefines.Contains(testSymbol), "Added symbol should be present");
        }

        [Test]
        public void ApplyDefines_EnabledItem_AddsSymbol()
        {
            string testSymbol = "DM_TEST_APPLY_ADD_12345";
            var item = TestHelper.CreateTempItem(
                "define_add", "X, Y", testSymbol);
            item.IsEnabled = true;
            UnityEditor.EditorUtility.SetDirty(item);

            DependencyManager.ApplyDefines(new List<DependencyItem> { item });

            var defines = DependencyManager.GetDefines();
            Assert.IsTrue(defines.Contains(testSymbol), "Symbol should be added for enabled item");
        }

        [Test]
        public void ApplyDefines_DisabledItem_RemovesSymbol()
        {
            string testSymbol = "DM_TEST_APPLY_REMOVE_12345";

            // First add the symbol.
            var adds = DependencyManager.GetDefines();
            adds.Add(testSymbol);
            DependencyManager.SetDefines(adds);

            // Create a disabled item for that symbol.
            var item = TestHelper.CreateTempItem(
                "define_remove", "X, Y", testSymbol);
            item.IsEnabled = false;
            UnityEditor.EditorUtility.SetDirty(item);

            DependencyManager.ApplyDefines(new List<DependencyItem> { item });

            var defines = DependencyManager.GetDefines();
            Assert.IsFalse(defines.Contains(testSymbol), "Symbol should be removed for disabled item");
        }

        [Test]
        public void ApplyDefines_PreservesOtherDefines()
        {
            string foreignSymbol = "DM_TEST_FOREIGN_DEFINE_12345";

            // Add a foreign symbol not managed by any item.
            var adds = DependencyManager.GetDefines();
            adds.Add(foreignSymbol);
            DependencyManager.SetDefines(adds);

            // Apply defines with an unrelated item.
            var item = TestHelper.CreateTempItem(
                "define_preserve", "X, Y", "DM_TEST_UNRELATED_12345");
            item.IsEnabled = false;
            UnityEditor.EditorUtility.SetDirty(item);

            DependencyManager.ApplyDefines(new List<DependencyItem> { item });

            var defines = DependencyManager.GetDefines();
            Assert.IsTrue(defines.Contains(foreignSymbol), "Foreign symbol should be preserved");
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original defines.
            DependencyManager.SetDefines(_originalDefines);
            TestHelper.CleanupTempAssets();
        }
    }
}
