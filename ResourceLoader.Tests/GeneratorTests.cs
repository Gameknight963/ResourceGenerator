using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace ResourceLoader.Tests;

public sealed class GeneratorTests
{
    [Fact]
    public async Task HappyPath_GeneratesPropertiesForKnownExtensions()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Defaults;

            namespace TestNamespace;

            [ResourceFolder("Resources", nameof(_resources))]
            [UseDefaultLoaders]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
            """;

        (ImmutableArray<Diagnostic> diagnostics, string? generatedSource) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "test.txt", "data.bin" });

        // No errors
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Source was generated
        Assert.NotNull(generatedSource);

        // Contains expected properties
        Assert.Contains("public static string Test", generatedSource);
        Assert.Contains("public static byte[] Data", generatedSource);

        // Contains static loader instances
        Assert.Contains("private static readonly", generatedSource);
    }

    [Fact]
    public async Task MissingMember_EmitsRL0007()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Defaults;

            namespace TestNamespace;

            [ResourceFolder("Resources", "nonExistentField")]
            [UseDefaultLoaders]
            public partial class TestMod { }
            """;

        (ImmutableArray<Diagnostic> diagnostics, string? _) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "test.txt" });

        Assert.Contains(diagnostics, d => d.Id == "RL0007");
    }

    [Fact]
    public async Task NonStaticMember_EmitsRL0006()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Defaults;

            namespace TestNamespace;

            [ResourceFolder("Resources", nameof(_resources))]
            [UseDefaultLoaders]
            public partial class TestMod
            {
                private string _resources = "some/path";
            }
            """;

        (ImmutableArray<Diagnostic> diagnostics, string? _) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "test.txt" });

        Assert.Contains(diagnostics, d => d.Id == "RL0006");
    }

    [Fact]
    public async Task MissingFolder_EmitsRL0002()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Defaults;

            namespace TestNamespace;

            [ResourceFolder("NonExistentFolder", nameof(_resources))]
            [UseDefaultLoaders]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
            """;

        (ImmutableArray<Diagnostic> diagnostics, string? _) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "test.txt" });

        Assert.Contains(diagnostics, d => d.Id == "RL0002");
    }

    [Fact]
    public async Task NoLoaderRegistered_EmitsRL0003()
    {
        string source = """
            using ResourceLoader.Attributes;

            namespace TestNamespace;

            [ResourceFolder("Resources", nameof(_resources))]
            public partial class Test
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? _) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "unknown.xyz" });

        Assert.Contains(diagnostics, d => d.Id == "RL0003");
    }

    [Fact]
    public async Task DirectLoader_WithOverride_WinsOverBundleLoader()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Core;

            namespace TestNamespace;

            [HandlesExtensions(".txt")]
            public class BundleTextLoader : IResourceLoader<string>
            {
                public string Load(string fullPath) => "bundle";
            }

            [HandlesExtensions(".txt")]
            public class DirectTextLoader : IResourceLoader<int>
            {
                public int Load(string fullPath) => 0;
            }

            [LoaderBundle]
            [RegisterLoader(typeof(BundleTextLoader))]
            public sealed class TestBundleAttribute : System.Attribute { }

            [ResourceFolder("Resources", nameof(_resources))]
            [TestBundle]
            [RegisterLoader(typeof(DirectTextLoader), overrideBundle: true)]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? generatedSource) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "test.txt" });

        Assert.NotNull(generatedSource);
        Assert.Contains("int?", generatedSource);
        Assert.DoesNotContain("string?", generatedSource);
        Assert.DoesNotContain(diagnostics, d => d.Id == "RL0004");
    }

    [Fact]
    public async Task DirectLoader_WithoutOverride_EmitsRL0008()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Core;

            namespace TestNamespace;

            [HandlesExtensions(".txt")]
            public class BundleTextLoader : IResourceLoader<string>
            {
                public string Load(string fullPath) => "bundle";
            }

            [HandlesExtensions(".txt")]
            public class DirectTextLoader : IResourceLoader<int>
            {
                public int Load(string fullPath) => 0;
            }

            [LoaderBundle]
            [RegisterLoader(typeof(BundleTextLoader))]
            public sealed class TestBundleAttribute : System.Attribute { }

            [ResourceFolder("Resources", nameof(_resources))]
            [TestBundle]
            [RegisterLoader(typeof(DirectTextLoader))]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? _) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "test.txt" });

        Assert.Contains(diagnostics, d => d.Id == "RL0008");
    }

    [Fact]
    public async Task TransitiveLoader_WithWarnIfTransitive_EmitsRL0005()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Core;

            namespace TestNamespace;

            [HandlesExtensions(".txt")]
            [WarnIfTransitive]
            public class InnerTextLoader : IResourceLoader<string>
            {
                public string Load(string fullPath) => string.Empty;
            }

            [LoaderBundle]
            [RegisterLoader(typeof(InnerTextLoader))]
            public sealed class InnerBundleAttribute : System.Attribute { }

            [LoaderBundle]
            [RegisterLoader(typeof(InnerBundleAttribute))]
            public sealed class OuterBundleAttribute : System.Attribute { }

            [ResourceFolder("Resources", nameof(_resources))]
            [OuterBundle]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? _) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "test.txt" });

        Assert.Contains(diagnostics, d => d.Id == "RL0005");
    }

    [Fact]
    public async Task TransitiveLoader_WithoutWarnIfTransitive_NoRL0005()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Core;

            namespace TestNamespace;

            [HandlesExtensions(".txt")]
            public class InnerTextLoader : IResourceLoader<string>
            {
                public string Load(string fullPath) => string.Empty;
            }

            [LoaderBundle]
            [RegisterLoader(typeof(InnerTextLoader))]
            public sealed class InnerBundleAttribute : System.Attribute { }

            [LoaderBundle]
            [RegisterLoader(typeof(InnerBundleAttribute))]
            public sealed class OuterBundleAttribute : System.Attribute { }

            [ResourceFolder("Resources", nameof(_resources))]
            [OuterBundle]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? _) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "test.txt" });

        Assert.DoesNotContain(diagnostics, d => d.Id == "RL0005");
    }

    [Fact]
    public async Task DirectLoader_WithoutOverride_PartialConflict_EmitsRL0008ForConflictingExtensionOnly()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Core;

            namespace TestNamespace;

            [HandlesExtensions(".txt")]
            public class BundleTextLoader : IResourceLoader<string>
            {
                public string Load(string fullPath) => string.Empty;
            }

            [HandlesExtensions(".txt", ".md")]
            public class DirectTextLoader : IResourceLoader<int>
            {
                public int Load(string fullPath) => 0;
            }

            [LoaderBundle]
            [RegisterLoader(typeof(BundleTextLoader))]
            public sealed class TestBundleAttribute : System.Attribute { }

            [ResourceFolder("Resources", nameof(_resources))]
            [TestBundle]
            [RegisterLoader(typeof(DirectTextLoader))]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? generatedSource) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "test.txt", "readme.md" });

        // RL0008 for .txt conflict
        Assert.Contains(diagnostics, d => d.Id == "RL0008");

        // .md should still be generated since no conflict there
        Assert.NotNull(generatedSource);
        Assert.Contains("Readme", generatedSource);
    }

    [Fact]
    public async Task HiddenFiles_AreSkipped()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Defaults;

            namespace TestNamespace;

            [ResourceFolder("Resources", nameof(_resources))]
            [UseDefaultLoaders]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? generatedSource) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { ".hidden", "visible.txt" });

        Assert.NotNull(generatedSource);
        Assert.DoesNotContain("Hidden", generatedSource);
        Assert.Contains("Visible", generatedSource);
        Assert.DoesNotContain(diagnostics, d => d.Id == "RL0003" && d.GetMessage().Contains(".hidden"));
    }

    [Fact]
    public async Task FileWithNoExtension_IsHandledByWildcardLoader()
    {
        string source = """
        using ResourceLoader.Attributes;
        using ResourceLoader.Defaults;

        namespace TestNamespace;

        [ResourceFolder("Resources", nameof(_resources))]
        [UseDefaultLoaders]
        public partial class TestMod
        {
            private static string _resources = "some/path";
        }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? generatedSource) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "noextension" });

        Assert.NotNull(generatedSource);
        Assert.Contains("Noextension", generatedSource);
        Assert.DoesNotContain(diagnostics, d => d.Id == "RL0003");
    }

    [Fact]
    public async Task FileNameCollision_EmitsDiagnostic()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Defaults;

            namespace TestNamespace;

            [ResourceFolder("Resources", nameof(_resources))]
            [UseDefaultLoaders]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? generatedSource) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "foo.txt", "foo.json" });

        int occurrences = generatedSource!.Split("public static string Foo =>").Length - 1;
        Assert.Equal(1, occurrences);

        // should emit RL0009
        Assert.Contains(diagnostics, d => d.Id == "RL0009");
    }

    [Fact]
    public async Task SubdirectoryFiles_NotIncluded_WhenNotRecursive()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Defaults;

            namespace TestNamespace;

            [ResourceFolder("Resources", nameof(_resources))]
            [UseDefaultLoaders]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? generatedSource) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "top.txt" },
            subDirectoryFiles: new Dictionary<string, string[]> { { "sub", new[] { "nested.txt" } } });

        Assert.NotNull(generatedSource);
        Assert.Contains("Top", generatedSource);
        Assert.DoesNotContain("Nested", generatedSource);
    }

    [Fact]
    public async Task SubdirectoryFiles_GenerateNestedClasses_WhenRecursive()
    {
        string source = """
            using ResourceLoader.Attributes;
            using ResourceLoader.Defaults;

            namespace TestNamespace;

            [ResourceFolder("Resources", nameof(_resources), recursive: true)]
            [UseDefaultLoaders]
            public partial class TestMod
            {
                private static string _resources = "some/path";
            }
        """;

        (ImmutableArray<Diagnostic> diagnostics, string? generatedSource) = await GeneratorTestHelper.RunGenerator(
            source,
            fileNames: new[] { "top.txt" },
            subDirectoryFiles: new Dictionary<string, string[]> { { "sub", new[] { "nested.txt" } } });

        Assert.NotNull(generatedSource);
        Assert.Contains("Top", generatedSource);
        Assert.Contains("public static class Sub", generatedSource);
        Assert.Contains("Nested", generatedSource);
    }
}