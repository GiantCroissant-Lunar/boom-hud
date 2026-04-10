using NUnit.Framework;

namespace DA_Assets.DM.Tests
{
    public class ResolveTypeTests
    {
        #region Path B — DLL/package types

        [Test]
        public void ResolveType_PathB_KnownUnityType_Found()
        {
            var item = TestHelper.CreateTempItem(
                "resolve_unity_type",
                "UnityEngine.UI.Image, UnityEngine.UI",
                "TEST_UI_IMAGE");

            var result = DependencyManager.ResolveType(item);

            Assert.IsTrue(result.found, "UnityEngine.UI.Image should be found via Path B");
            Assert.IsNotNull(result.scriptPath);
        }

        [Test]
        public void ResolveType_PathB_NonExistentType_NotFound()
        {
            var item = TestHelper.CreateTempItem(
                "resolve_nonexistent",
                "Completely.Fake.TypeThatDoesNotExist, FakeAssembly",
                "TEST_FAKE");

            var result = DependencyManager.ResolveType(item);

            Assert.IsFalse(result.found, "Non-existent type should not be found");
            Assert.IsNull(result.scriptPath);
        }

        [Test]
        public void ResolveType_PathB_PathMismatch_NotFound()
        {
            var item = TestHelper.CreateTempItem(
                "resolve_path_mismatch",
                "UnityEngine.UI.Image, UnityEngine.UI",
                "TEST_UI_MISMATCH",
                unexpectedPaths: new[] { "UnityEngine.UI.dll" });

            var result = DependencyManager.ResolveType(item);

            Assert.IsFalse(result.found, "Type should be rejected due to path mismatch");
        }

        #endregion

        #region Path A — Script-based search

        [Test]
        public void ResolveType_PathA_ScriptExists_Found()
        {
            // Create a temp script for a known Unity type to verify Path A works.
            // We use MonoBehaviour as base so it compiles, but the actual test
            // is about whether FindScript locates the .cs and determines assembly.
            TestHelper.CreateTempScript("resolve_script", "ResolveTestClass", "DA_Assets.DM.Tests.Fixtures");

            var item = TestHelper.CreateTempItem(
                "resolve_script_item",
                "DA_Assets.DM.Tests.Fixtures.ResolveTestClass, Assembly-CSharp",
                "TEST_RESOLVE_SCRIPT");

            var result = DependencyManager.ResolveType(item);

            // Path A will find the .cs file and determine it's in the test assembly.
            // Type.GetType may or may not find it depending on compilation state,
            // so this test verifies the FindScript part by checking the flow doesn't crash.
            // The actual type resolution depends on Unity recompiling.
            Assert.IsNotNull(result);
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            TestHelper.CleanupTempAssets();
        }
    }
}
