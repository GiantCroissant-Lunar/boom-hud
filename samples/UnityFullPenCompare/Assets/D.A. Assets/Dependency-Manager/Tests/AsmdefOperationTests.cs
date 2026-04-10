using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace DA_Assets.DM.Tests
{
    public class AsmdefOperationTests
    {
        #region ReadAsmdefName

        [Test]
        public void ReadAsmdefName_Valid_ReturnsName()
        {
            string path = TestHelper.CreateTempAsmdef("read_name", "TestAssembly");
            string name = DependencyManager.ReadAsmdefName(path);
            Assert.AreEqual("TestAssembly", name);
        }

        [Test]
        public void ReadAsmdefName_Malformed_ReturnsNull()
        {
            // Intentionally malformed — no "name" field. This is a valid edge case test.
            string dir = TestHelper.EnsureSubDirectory("read_malformed");
            string filePath = Path.GetFullPath($"{dir}/bad.asmdef");
            File.WriteAllText(filePath, "{ \"foo\": \"bar\" }");

            string assetPath = $"{dir}/bad.asmdef";
            string name = DependencyManager.ReadAsmdefName(assetPath);
            Assert.IsNull(name);
        }

        #endregion

        #region AddReferenceToAsmdef

        [Test]
        public void AddReference_ToEmptyRefs_AddsGuid()
        {
            string path = TestHelper.CreateTempAsmdef("add_empty", "AddEmptyTest");
            string fakeGuid = "abcdef1234567890abcdef1234567890";

            bool result = DependencyManager.AddReferenceToAsmdef(path, fakeGuid);

            Assert.IsTrue(result);
            string content = File.ReadAllText(Path.GetFullPath(path));
            Assert.IsTrue(content.Contains($"GUID:{fakeGuid}"));
        }

        [Test]
        public void AddReference_AlreadyExists_ReturnsFalse()
        {
            // Create asmdef, then add reference via DM — so it already exists.
            string path = TestHelper.CreateTempAsmdef("add_exists", "AddExistsTest");
            string fakeGuid = "abcdef1234567890abcdef1234567890";
            DependencyManager.AddReferenceToAsmdef(path, fakeGuid);

            // Second add should return false.
            bool result = DependencyManager.AddReferenceToAsmdef(path, fakeGuid);

            Assert.IsFalse(result);
        }

        [Test]
        public void AddReference_DeduplicatesPlainName()
        {
            // Create target asmdef via DM.
            string targetPath = TestHelper.CreateTempAsmdef("add_dedup_target", "MyPlainAssembly");
            string targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
            Assert.IsFalse(string.IsNullOrWhiteSpace(targetGuid), "Target asmdef should have a valid GUID");

            // Create consumer asmdef with a plain-name reference written manually.
            string consumerDir = TestHelper.EnsureSubDirectory("add_dedup_consumer");
            string consumerFullPath = Path.GetFullPath($"{consumerDir}/ConsumerAssembly.asmdef");
            string consumerJson =
                "{\n" +
                "    \"name\": \"ConsumerAssembly\",\n" +
                "    \"references\": [\n" +
                "        \"MyPlainAssembly\"\n" +
                "    ],\n" +
                "    \"includePlatforms\": [],\n" +
                "    \"excludePlatforms\": []\n" +
                "}";
            File.WriteAllText(consumerFullPath, consumerJson);
            string consumerPath = $"{consumerDir}/ConsumerAssembly.asmdef";
            AssetDatabase.ImportAsset(consumerPath);

            // Act: add by GUID — should remove the plain-name duplicate.
            bool result = DependencyManager.AddReferenceToAsmdef(consumerPath, targetGuid);

            Assert.IsTrue(result);
            string content = File.ReadAllText(consumerFullPath);
            Assert.IsTrue(content.Contains($"GUID:{targetGuid}"), "Should contain GUID reference");
            Assert.IsFalse(content.Contains("\"MyPlainAssembly\""), "Should NOT contain plain-name reference anymore");
        }

        #endregion

        #region RemoveReferenceFromAsmdef

        [Test]
        public void RemoveReference_Exists_RemovesAndReturnsTrue()
        {
            // Create asmdef and add reference via DM first.
            string path = TestHelper.CreateTempAsmdef("remove_exists", "RemoveExistsTest");
            string fakeGuid = "abcdef1234567890abcdef1234567890";
            DependencyManager.AddReferenceToAsmdef(path, fakeGuid);

            bool result = DependencyManager.RemoveReferenceFromAsmdef(path, fakeGuid);

            Assert.IsTrue(result);
            string content = File.ReadAllText(Path.GetFullPath(path));
            Assert.IsFalse(content.Contains($"GUID:{fakeGuid}"));
        }

        [Test]
        public void RemoveReference_NotExists_ReturnsFalse()
        {
            string path = TestHelper.CreateTempAsmdef("remove_not_exists", "RemoveNotExistsTest");

            bool result = DependencyManager.RemoveReferenceFromAsmdef(path, "nonexistentguid000000000000000000");

            Assert.IsFalse(result);
        }

        #endregion

        #region FindNearestAsmdef

        [Test]
        public void FindNearestAsmdef_InSameDir_ReturnsPath()
        {
            string dir = TestHelper.EnsureSubDirectory("nearest_same");
            string asmdefPath = TestHelper.CreateTempAsmdef("nearest_same", "NearestSameTest");

            string scriptPath = $"{dir}/SomeScript.cs";

            string result = DependencyManager.FindNearestAsmdef(scriptPath);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("NearestSameTest.asmdef"));
        }

        [Test]
        public void FindNearestAsmdef_InParentDir_ReturnsPath()
        {
            string parentDir = TestHelper.EnsureSubDirectory("nearest_parent");
            TestHelper.CreateTempAsmdef("nearest_parent", "NearestParentTest");

            string childDir = TestHelper.EnsureSubDirectory("nearest_parent/child");
            string scriptPath = $"{childDir}/SomeScript.cs";

            string result = DependencyManager.FindNearestAsmdef(scriptPath);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("NearestParentTest.asmdef"));
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            TestHelper.CleanupTempAssets();
        }
    }
}
