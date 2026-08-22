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
}