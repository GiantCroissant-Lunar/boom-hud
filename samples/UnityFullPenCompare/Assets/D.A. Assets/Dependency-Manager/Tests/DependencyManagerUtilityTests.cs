using NUnit.Framework;

namespace DA_Assets.DM.Tests
{
    public class DependencyManagerUtilityTests
    {
        #region ExtractFullTypeName

        [Test]
        public void ExtractFullTypeName_WithAssembly_ReturnsTypeName()
        {
            string result = DependencyManager.ExtractFullTypeName("UnityEngine.UI.Image, UnityEngine.UI");
            Assert.AreEqual("UnityEngine.UI.Image", result);
        }

        [Test]
        public void ExtractFullTypeName_WithoutAssembly_ReturnsAsIs()
        {
            string result = DependencyManager.ExtractFullTypeName("Some.Namespace.MyType");
            Assert.AreEqual("Some.Namespace.MyType", result);
        }

        [Test]
        public void ExtractFullTypeName_Null_ReturnsNull()
        {
            string result = DependencyManager.ExtractFullTypeName(null);
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractFullTypeName_Empty_ReturnsNull()
        {
            string result = DependencyManager.ExtractFullTypeName("");
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractFullTypeName_Whitespace_ReturnsNull()
        {
            string result = DependencyManager.ExtractFullTypeName("   ");
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractFullTypeName_WithExtraSpaces_TrimsProperly()
        {
            string result = DependencyManager.ExtractFullTypeName("  Ns.Type , Assembly  ");
            Assert.AreEqual("Ns.Type", result);
        }

        #endregion

        #region ContainsNamespace

        [Test]
        public void ContainsNamespace_Found_ReturnsTrue()
        {
            string content = "using System;\n\nnamespace Foo.Bar\n{\n    public class Baz { }\n}\n";
            Assert.IsTrue(DependencyManager.ContainsNamespace(content, "Foo.Bar"));
        }

        [Test]
        public void ContainsNamespace_NotFound_ReturnsFalse()
        {
            string content = "using System;\n\nnamespace Other.Namespace\n{\n    public class Baz { }\n}\n";
            Assert.IsFalse(DependencyManager.ContainsNamespace(content, "Foo.Bar"));
        }

        [Test]
        public void ContainsNamespace_PartialMatch_ReturnsFalse()
        {
            string content = "namespace Foo\n{\n    public class Bar { }\n}\n";
            Assert.IsFalse(DependencyManager.ContainsNamespace(content, "Foo.Bar"));
        }

        [Test]
        public void ContainsNamespace_WithExtraWhitespace_ReturnsTrue()
        {
            string content = "namespace   Foo.Bar\n{\n}\n";
            Assert.IsTrue(DependencyManager.ContainsNamespace(content, "Foo.Bar"));
        }

        [Test]
        public void ContainsNamespace_NoNamespace_ReturnsFalse()
        {
            string content = "public class Standalone { }";
            Assert.IsFalse(DependencyManager.ContainsNamespace(content, "Any.Namespace"));
        }

        #endregion

        #region ValidateAssemblyPath

        [Test]
        public void ValidatePath_NoConstraints_ReturnsTrue()
        {
            var item = TestHelper.CreateTempItem("validate_no_constraints", "X, Y", "TEST_SYM");

            bool result = DependencyManager.ValidateAssemblyPath(item, "/any/path/lib.dll");
            Assert.IsTrue(result);
        }

        [Test]
        public void ValidatePath_ExpectedMatch_ReturnsTrue()
        {
            var item = TestHelper.CreateTempItem("validate_expected_match", "X, Y", "TEST_SYM",
                expectedPaths: new[] { "Managed/MyLib.dll" });

            bool result = DependencyManager.ValidateAssemblyPath(item, "C:\\some\\path\\Managed\\MyLib.dll");
            Assert.IsTrue(result);
        }

        [Test]
        public void ValidatePath_ExpectedMismatch_ReturnsFalse()
        {
            var item = TestHelper.CreateTempItem("validate_expected_mismatch", "X, Y", "TEST_SYM",
                expectedPaths: new[] { "Managed/MyLib.dll" });

            bool result = DependencyManager.ValidateAssemblyPath(item, "C:\\other\\path\\Wrong.dll");
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidatePath_UnexpectedMatch_ReturnsFalse()
        {
            var item = TestHelper.CreateTempItem("validate_unexpected_match", "X, Y", "TEST_SYM",
                unexpectedPaths: new[] { "Plastic/Newtonsoft.Json.dll" });

            bool result = DependencyManager.ValidateAssemblyPath(item, "C:\\editor\\Plastic\\Newtonsoft.Json.dll");
            Assert.IsFalse(result);
        }

        [Test]
        public void ValidatePath_ExpectedOk_UnexpectedNo_ReturnsTrue()
        {
            var item = TestHelper.CreateTempItem("validate_combined", "X, Y", "TEST_SYM",
                expectedPaths: new[] { "Packages/MyLib.dll" },
                unexpectedPaths: new[] { "Plastic/MyLib.dll" });

            bool result = DependencyManager.ValidateAssemblyPath(item, "C:\\project\\Packages\\MyLib.dll");
            Assert.IsTrue(result);
        }

        [TearDown]
        public void TearDown()
        {
            TestHelper.CleanupTempAssets();
        }

        #endregion
    }
}
