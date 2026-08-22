using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ResourceLoader.Generator;
using System.Collections.Immutable;

namespace ResourceLoader.Tests
{
    public static class GeneratorTestHelper
    {
        public static async Task<(ImmutableArray<Diagnostic> Diagnostics, string? GeneratedSource)> RunGenerator(
            string source,
            string[] fileNames,
            string scanPath = "Resources")
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string resourceDir = Path.Combine(tempDir, scanPath);
            Directory.CreateDirectory(resourceDir);

            try
            {
                // Create dummy files
                foreach (string fileName in fileNames)
                    File.WriteAllText(Path.Combine(resourceDir, fileName), string.Empty);

                // Set up the compilation
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

                CSharpCompilation compilation = CSharpCompilation.Create(
                    assemblyName: "TestAssembly",
                    syntaxTrees: new[] { syntaxTree },
                    references: GetMetadataReferences(),
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                // Set up the generator
                ResourceLoaderGenerator generator = new();
                CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(
                    generators: new[] { generator.AsSourceGenerator() },
                    optionsProvider: new TestAnalyzerConfigOptionsProvider(tempDir));

                GeneratorDriver result = driver.RunGeneratorsAndUpdateCompilation(
                    compilation,
                    out Compilation outputCompilation,
                    out ImmutableArray<Diagnostic> diagnostics);

                GeneratorDriverRunResult runResult = result.GetRunResult();

                string? generatedSource = runResult.GeneratedTrees
                    .Select(t => t.GetText().ToString())
                    .FirstOrDefault();

                return (diagnostics, generatedSource);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        private static IEnumerable<MetadataReference> GetMetadataReferences()
        {
            string[] runtimeAssemblies = Directory.GetFiles(
                Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                "*.dll");

            return runtimeAssemblies
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a))
                .Concat(new[]
                {
                    MetadataReference.CreateFromFile(
                        typeof(ResourceLoader.Attributes.ResourceFolderAttribute).Assembly.Location),
                    MetadataReference.CreateFromFile(
                        typeof(ResourceLoader.Defaults.BytesLoader).Assembly.Location),
                    MetadataReference.CreateFromFile(
                        typeof(ResourceLoader.Core.IResourceLoader<>).Assembly.Location),
                });
        }
    }
}