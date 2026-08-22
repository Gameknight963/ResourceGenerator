using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;
using System.Linq;



namespace ResourceLoader.Generator
{
    [Generator]
    public sealed class ResourceLoaderGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context
                .SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
                    transform: static (ctx, _) => GetClassWithResourceFolder(ctx))
                .Where(static c => c is not null)!;

            IncrementalValueProvider<string?> projectDir = context
                .AnalyzerConfigOptionsProvider
                .Select(static (provider, _) =>
                {
                    provider.GlobalOptions.TryGetValue("build_property.projectdir", out string? dir);
                    return dir;
                });

            IncrementalValuesProvider<(ClassDeclarationSyntax Class, string? ProjectDir)> combined =
                classDeclarations.Combine(projectDir);

            context.RegisterSourceOutput(combined, static (ctx, source) =>
                Execute(ctx, source.Item1, source.Item2));
        }

        private static ClassDeclarationSyntax? GetClassWithResourceFolder(GeneratorSyntaxContext ctx)
        {
            ClassDeclarationSyntax classDecl = (ClassDeclarationSyntax)ctx.Node;

            foreach (AttributeListSyntax attributeList in classDecl.AttributeLists)
            {
                foreach (AttributeSyntax attribute in attributeList.Attributes)
                {
                    if (ctx.SemanticModel.GetSymbolInfo(attribute).Symbol is not IMethodSymbol attributeSymbol)
                        continue;

                    string fullName = attributeSymbol.ContainingType.ToDisplayString();
                    if (fullName == "ResourceLoader.Attributes.ResourceFolderAttribute")
                        return classDecl;
                }
            }

            return null;
        }

        private static void Execute(
            SourceProductionContext ctx,
            ClassDeclarationSyntax classDecl,
            string? projectDir)
        {
            if (projectDir is null)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "RL0001",
                        "Could not determine project directory",
                        "ResourceLoader could not determine the project directory",
                        "ResourceLoader",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
                return;
            }

            // Get the ScanPath value from the attribute
            AttributeSyntax? attribute = classDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.Name.ToString().Contains("ResourceFolder"));

            if (attribute?.ArgumentList?.Arguments.Count < 2)
                return;

            string scanPath = attribute!.ArgumentList!.Arguments[0]
                .Expression.ToString().Trim('"');
            string runtimePath = attribute.ArgumentList.Arguments[1]
                .Expression.ToString().Trim('"');

            string fullScanPath = Path.Combine(projectDir, scanPath);

            if (!Directory.Exists(fullScanPath))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "RL0002",
                        "Resources folder not found",
                        "ResourceLoader could not find the folder '{0}'",
                        "ResourceLoader",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None,
                    fullScanPath));
                return;
            }

            string[] files = Directory.GetFiles(fullScanPath);
            string className = classDecl.Identifier.Text;
            string namespaceName = GetNamespace(classDecl);

            System.Text.StringBuilder fields = new();
            System.Text.StringBuilder loadCalls = new();

            foreach (string file in files)
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                string fileName = Path.GetFileNameWithoutExtension(file);
                string fieldName = SanitizeName(fileName);
                string? typeName = extension switch
                {
                    ".png" or ".jpg" or ".jpeg" => "UnityEngine.Texture2D",
                    ".mp3" or ".wav" or ".ogg" => "UnityEngine.AudioClip",
                    _ => null
                };

                if (typeName is null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "RL0003",
                            "Unknown file extension",
                            "No loader registered for extension '{0}' (file: '{1}')",
                            "ResourceLoader",
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true),
                        Location.None,
                        extension,
                        Path.GetFileName(file)));
                    continue;
                }

                fields.AppendLine($"    public {typeName} {fieldName} {{ get; private set; }}");
                loadCalls.AppendLine($"        {fieldName} = Load{typeName.Split('.').Last()}({runtimePath}, \"{Path.GetFileName(file)}\");");
            }

            string source = $$"""
                // <auto-generated/>
                namespace {{namespaceName}};

                partial class {{className}}
                {
                {{fields}}
                    private void LoadResources()
                    {
                {{loadCalls}}    }
                }
                """;

            ctx.AddSource($"{className}.g.cs", source);
        }

        private static string SanitizeName(string fileName)
        {
            System.Text.StringBuilder sb = new();
            bool capitalizeNext = true;

            foreach (char c in fileName)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(capitalizeNext ? char.ToUpper(c) : c);
                    capitalizeNext = false;
                }
                else
                {
                    capitalizeNext = true;
                }
            }

            // Prefix with _ if starts with digit
            if (sb.Length > 0 && char.IsDigit(sb[0]))
                sb.Insert(0, '_');

            return sb.ToString();
        }

        private static string GetNamespace(ClassDeclarationSyntax classDecl)
        {
            SyntaxNode? parent = classDecl.Parent;
            while (parent is not null)
            {
                if (parent is NamespaceDeclarationSyntax ns)
                    return ns.Name.ToString();
                if (parent is FileScopedNamespaceDeclarationSyntax fns)
                    return fns.Name.ToString();
                parent = parent.Parent;
            }
            return "global";
        }
    }
}
