using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using BoomHud.Mvvm.Generators;
using Xunit;

namespace BoomHud.Tests.Unit.Mvvm;

public sealed class MvvmAdapterGeneratorTests
{
    [Fact]
    public void CustomFlavor_GenerateConcreteViewModels_EmitsPropertyChangedAndProperties()
    {
        const string source = @"using BoomHud.Mvvm;

namespace Sample
{
    public interface IStatusBarViewModel
    {
        string? Health { get; }
        string? Mana { get; }
    }

    [BoomHudViewModelFor(""StatusBar"")]
    public partial class StatusBarViewModel
    {
    }
}
";

        var options = new Dictionary<string, string>
        {
            ["build_property.BoomHudMvvmFlavor"] = "Custom",
            ["build_property.BoomHudMvvmGenerateConcreteViewModels"] = "true",
        };

        var result = RunGenerator(source, options, out var outputCompilation);

        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var run = Assert.Single(result.Results);
        Assert.Contains(run.GeneratedSources, s => s.HintName.EndsWith("StatusBarViewModel.BoomHudMvvm.Custom.g.cs", StringComparison.Ordinal));

        var generated = run.GeneratedSources.First(s => s.HintName.EndsWith("StatusBarViewModel.BoomHudMvvm.Custom.g.cs", StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("INotifyPropertyChanged", generated);
        Assert.True(
            generated.Contains("public string? Health", StringComparison.Ordinal) ||
            generated.Contains("public string Health", StringComparison.Ordinal) ||
            generated.Contains("public System.String? Health", StringComparison.Ordinal) ||
            generated.Contains("public System.String Health", StringComparison.Ordinal) ||
            generated.Contains("public global::System.String? Health", StringComparison.Ordinal) ||
            generated.Contains("public global::System.String Health", StringComparison.Ordinal));
        Assert.True(
            generated.Contains("public string? Mana", StringComparison.Ordinal) ||
            generated.Contains("public string Mana", StringComparison.Ordinal) ||
            generated.Contains("public System.String? Mana", StringComparison.Ordinal) ||
            generated.Contains("public System.String Mana", StringComparison.Ordinal) ||
            generated.Contains("public global::System.String? Mana", StringComparison.Ordinal) ||
            generated.Contains("public global::System.String Mana", StringComparison.Ordinal));
    }

    [Fact]
    public void ReactiveUiFlavor_GenerateConcreteViewModels_EmitsRaiseAndSetIfChangedProperties()
    {
        const string source = @"using BoomHud.Mvvm;

namespace ReactiveUI
{
    public class ReactiveObject
    {
        protected bool RaiseAndSetIfChanged<T>(ref T field, T value)
        {
            field = value;
            return true;
        }
    }
}

namespace Sample
{
    public interface IStatusBarViewModel
    {
        string? Health { get; }
    }

    [BoomHudViewModelFor(""StatusBar"")]
    public partial class StatusBarViewModel
    {
    }
}
";

        var options = new Dictionary<string, string>
        {
            ["build_property.BoomHudMvvmFlavor"] = "ReactiveUI",
            ["build_property.BoomHudMvvmGenerateConcreteViewModels"] = "true",
        };

        var result = RunGenerator(source, options, out var outputCompilation);

        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var run = Assert.Single(result.Results);
        Assert.Contains(run.GeneratedSources, s => s.HintName.EndsWith("StatusBarViewModel.BoomHudMvvm.ReactiveUI.g.cs", StringComparison.Ordinal));

        var generated = run.GeneratedSources.First(s => s.HintName.EndsWith("StatusBarViewModel.BoomHudMvvm.ReactiveUI.g.cs", StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("using ReactiveUI;", generated);
        Assert.Contains("ReactiveUI.ReactiveObject", generated);
        Assert.Contains("RaiseAndSetIfChanged", generated);
    }

    [Fact]
    public void CommunityToolkitFlavor_GenerateConcreteViewModels_EmitsSetPropertyProperties()
    {
        const string source = @"using BoomHud.Mvvm;

namespace CommunityToolkit.Mvvm.ComponentModel
{
    public class ObservableObject
    {
        protected bool SetProperty<T>(ref T field, T value)
        {
            field = value;
            return true;
        }
    }
}

namespace Sample
{
    public interface IStatusBarViewModel
    {
        string? Health { get; }
    }

    [BoomHudViewModelFor(""StatusBar"")]
    public partial class StatusBarViewModel
    {
    }
}
";

        var options = new Dictionary<string, string>
        {
            ["build_property.BoomHudMvvmFlavor"] = "CommunityToolkit",
            ["build_property.BoomHudMvvmGenerateConcreteViewModels"] = "true",
        };

        var result = RunGenerator(source, options, out var outputCompilation);

        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var run = Assert.Single(result.Results);
        Assert.Contains(run.GeneratedSources, s => s.HintName.EndsWith("StatusBarViewModel.BoomHudMvvm.CommunityToolkit.g.cs", StringComparison.Ordinal));

        var generated = run.GeneratedSources.First(s => s.HintName.EndsWith("StatusBarViewModel.BoomHudMvvm.CommunityToolkit.g.cs", StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("using CommunityToolkit.Mvvm.ComponentModel;", generated);
        Assert.Contains("CommunityToolkit.Mvvm.ComponentModel.ObservableObject", generated);
        Assert.Contains("SetProperty", generated);
    }

    private static GeneratorDriverRunResult RunGenerator(
        string source,
        IDictionary<string, string> globalOptions,
        out Compilation outputCompilation)
    {
        var parseOptions = CSharpParseOptions.Default;
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var references = new List<MetadataReference>();
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(tpa))
        {
            foreach (var path in tpa.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                references.Add(MetadataReference.CreateFromFile(path));
            }
        }
        else
        {
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(INotifyPropertyChanged).Assembly.Location));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "BoomHud_MvvmAdapterGeneratorTests",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        IIncrementalGenerator generator = new BoomHudMvvmGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
        driver = driver.WithUpdatedAnalyzerConfigOptions(optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out _);
        return driver.GetRunResult();
    }

    internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            private readonly IDictionary<string, string> _backing;

            public TestAnalyzerConfigOptions(IDictionary<string, string> backing)
            {
                _backing = backing;
            }

            public override bool TryGetValue(string key, out string value) =>
                _backing.TryGetValue(key, out value!);
        }

        private readonly AnalyzerConfigOptions _global;
        private static readonly AnalyzerConfigOptions _empty =
            new TestAnalyzerConfigOptions(new Dictionary<string, string>());

        public TestAnalyzerConfigOptionsProvider(IDictionary<string, string> globalOptions)
        {
            _global = new TestAnalyzerConfigOptions(globalOptions ?? new Dictionary<string, string>());
        }

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _empty;
    }
}
