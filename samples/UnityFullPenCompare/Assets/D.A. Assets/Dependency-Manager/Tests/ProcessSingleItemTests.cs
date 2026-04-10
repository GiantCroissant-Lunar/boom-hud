using NUnit.Framework;
using UnityEditor;

namespace DA_Assets.DM.Tests
{
    public class ProcessSingleItemTests
    {
        [Test]
        public void ProcessSingleItem_EmptyFields_ReturnsFalse()
        {
            var item = TestHelper.CreateTempItem("process_empty", "", "");

            bool result = DependencyManager.ProcessSingleItem(item);

            Assert.IsFalse(result);
        }

        [Test]
        public void ProcessSingleItem_KnownType_EnablesDependency()
        {
            var item = TestHelper.CreateTempItem(
                "process_enable",
                "UnityEngine.UI.Image, UnityEngine.UI",
                "TEST_PROCESS_ENABLE");

            bool changed = DependencyManager.ProcessSingleItem(item);

            Assert.IsTrue(changed, "Status should change for a known type");
            Assert.IsTrue(item.IsEnabled, "Item should be enabled");
            Assert.AreNotEqual("Not found", item.ScriptPath);
        }

        [Test]
        public void ProcessSingleItem_UnknownType_DisablesDependency()
        {
            var item = TestHelper.CreateTempItem(
                "process_disable",
                "Fake.NonExistent.Type12345, FakeAssembly",
                "TEST_PROCESS_DISABLE");

            // First enable it manually to verify it gets disabled.
            item.IsEnabled = true;
            item.ScriptPath = "Previously found";
            EditorUtility.SetDirty(item);

            bool changed = DependencyManager.ProcessSingleItem(item);

            Assert.IsTrue(changed, "Status should change when type disappears");
            Assert.IsFalse(item.IsEnabled, "Item should be disabled");
        }

        [Test]
        public void ProcessSingleItem_DisabledManually_StaysDisabled()
        {
            var item = TestHelper.CreateTempItem(
                "process_manual_disable",
                "UnityEngine.UI.Image, UnityEngine.UI",
                "TEST_PROCESS_MANUAL");

            item.DisabledManually = true;
            EditorUtility.SetDirty(item);

            DependencyManager.ProcessSingleItem(item);

            Assert.IsFalse(item.IsEnabled, "Item should stay disabled when DisabledManually is true");
        }

        [Test]
        public void ProcessSingleItem_NoChange_ReturnsFalse()
        {
            var item = TestHelper.CreateTempItem(
                "process_no_change",
                "Fake.NonExistent.Type99999, FakeAssembly",
                "TEST_PROCESS_NOCHANGE");

            // First call to set initial state.
            DependencyManager.ProcessSingleItem(item);

            // Second call — nothing changed.
            bool result = DependencyManager.ProcessSingleItem(item);

            Assert.IsFalse(result, "Second call with same state should return false");
        }

        [TearDown]
        public void TearDown()
        {
            TestHelper.CleanupTempAssets();
        }
    }
}
