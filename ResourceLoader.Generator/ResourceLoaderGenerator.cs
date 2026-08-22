using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ResourceLoader.Generator
{
    [Generator]
    public sealed class ResourceLoaderGenerator : IIncrementalGenerator
    {
        private sealed record LoaderInfo(
            string LoaderTypeName,
            string ReturnTypeName,
            bool IsTransitive,
            bool WarnIfTransitive);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<INamedTypeSymbol> classSymbols = context
                .SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
                    transform: static (ctx, _) => GetClassWithResourceFolder(ctx))
                .Where(static s => s is not null)!;

            IncrementalValueProvider<string?> projectDir = context
                .AnalyzerConfigOptionsProvider
                .Select(static (provider, _) =>
                {
                    provider.GlobalOptions.TryGetValue("build_property.projectdir", out string? dir);
                    return dir;
                });

            IncrementalValuesProvider<(INamedTypeSymbol Symbol, string? ProjectDir)> combined =
                classSymbols.Combine(projectDir);

            context.RegisterSourceOutput(combined, static (ctx, source) =>
                Execute(ctx, source.Symbol, source.ProjectDir));
        }

        private static INamedTypeSymbol? GetClassWithResourceFolder(GeneratorSyntaxContext ctx)
        {
            ClassDeclarationSyntax classDecl = (ClassDeclarationSyntax)ctx.Node;

            if (ctx.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
                return null;

            foreach (AttributeData attr in classSymbol.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == "ResourceLoader.Attributes.ResourceFolderAttribute")
                    return classSymbol;
            }

            return null;
        }

        private static void Execute(
            SourceProductionContext ctx,
            INamedTypeSymbol classSymbol,
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

            AttributeData? resourceFolderAttr = classSymbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "ResourceLoader.Attributes.ResourceFolderAttribute");

            if (resourceFolderAttr is null) return;

            string scanPath = (string)resourceFolderAttr.ConstructorArguments[0].Value!;
            string runtimePath = (string)resourceFolderAttr.ConstructorArguments[1].Value!;
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

            Dictionary<string, LoaderInfo> loaderMap = ResolveLoaders(classSymbol, ctx);
            string[] files = Directory.GetFiles(fullScanPath);
            string className = classSymbol.Name;
            string namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            // Collect which loader types are actually used
            HashSet<string> usedLoaders = new();
            System.Text.StringBuilder properties = new();

            foreach (string file in files)
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                string fileName = Path.GetFileNameWithoutExtension(file);
                string fieldName = SanitizeName(fileName);
                string backingFieldName = "_" + char.ToLower(fieldName[0]) + fieldName.Substring(1);
                string fullFileName = Path.GetFileName(file);

                // Try specific extension first, then wildcard
                if (!loaderMap.TryGetValue(extension, out LoaderInfo? loader))
                    loaderMap.TryGetValue("*", out loader);

                if (loader is null)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "RL0003",
                            "No loader registered",
                            "No loader registered for extension '{0}' (file: '{1}')",
                            "ResourceLoader",
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true),
                        Location.None,
                        extension,
                        fullFileName));
                    continue;
                }

                if (loader.WarnIfTransitive && loader.IsTransitive)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "RL0005",
                            "Transitive loader",
                            "Loader '{0}' for file '{1}' was pulled in transitively. Consider registering it explicitly.",
                            "ResourceLoader",
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true),
                        Location.None,
                        loader.LoaderTypeName,
                        fullFileName));
                }

                usedLoaders.Add(loader.LoaderTypeName);

                properties.AppendLine($"    private static {loader.ReturnTypeName}? {backingFieldName};");
                properties.AppendLine($"    public static {loader.ReturnTypeName} {fieldName} =>");
                properties.AppendLine($"        {backingFieldName} ??= _{GetLoaderFieldName(loader.LoaderTypeName)}.Load(System.IO.Path.Combine({runtimePath}, \"{fullFileName}\"));");
                properties.AppendLine();
            }

            // Emit static loader instances for each used loader
            System.Text.StringBuilder loaderFields = new();
            foreach (string loaderTypeName in usedLoaders)
                loaderFields.AppendLine($"    private static readonly {loaderTypeName} _{GetLoaderFieldName(loaderTypeName)} = new {loaderTypeName}();");

            string source = $$"""
                namespace {{namespaceName}};

                partial class {{className}}
                {
                {{loaderFields}}
                {{properties}}}
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

        private static string GetLoaderFieldName(string fullyQualifiedTypeName)
        {
            string simpleName = fullyQualifiedTypeName.Split('.').Last();
            return char.ToLower(simpleName[0]) + simpleName.Substring(1);
        }

        private static void RegisterLoader(
            INamedTypeSymbol loaderType,
            Dictionary<string, LoaderInfo> result,
            bool isTransitive,
            SourceProductionContext ctx)
        {
            // Find IResourceLoader<T> implementation
            INamedTypeSymbol? loaderInterface = loaderType.AllInterfaces.FirstOrDefault(i =>
                i.IsGenericType &&
                i.ConstructedFrom.ToDisplayString() == "ResourceLoader.Attributes.IResourceLoader<T>");

            if (loaderInterface is null) return;

            string returnTypeName = loaderInterface.TypeArguments[0].ToDisplayString();
            string loaderTypeName = loaderType.ToDisplayString();

            bool warnIfTransitive = loaderType.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "ResourceLoader.Attributes.WarnIfTransitiveAttribute");

            // Find [HandlesExtensions]
            AttributeData? handlesAttr = loaderType.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "ResourceLoader.Attributes.HandlesExtensionsAttribute");

            if (handlesAttr is null) return;

            IEnumerable<string> extensions = handlesAttr.ConstructorArguments[0]
                .Values.Select(v => (string)v.Value!);

            foreach (string ext in extensions)
            {
                if (result.TryGetValue(ext, out LoaderInfo existing))
                {
                    // Collision - warn and keep existing (direct wins, then first bundle wins)
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "RL0004",
                            "Loader collision",
                            "Multiple loaders registered for extension '{0}': '{1}' and '{2}'. '{1}' will be used.",
                            "ResourceLoader",
                            DiagnosticSeverity.Warning,
                            isEnabledByDefault: true),
                        Location.None,
                        ext, existing.LoaderTypeName, loaderTypeName));
                    continue;
                }

                result[ext] = new LoaderInfo(loaderTypeName, returnTypeName, isTransitive, warnIfTransitive);
            }
        }

        private static Dictionary<string, LoaderInfo> ResolveLoaders(
            INamedTypeSymbol classSymbol,
            SourceProductionContext ctx)
        {
            Dictionary<string, LoaderInfo> result = new();

            // Collect direct [RegisterLoader] attributes
            foreach (AttributeData attr in classSymbol.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == "ResourceLoader.Attributes.RegisterLoaderAttribute")
                {
                    if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol loaderType) continue;
                    RegisterLoader(loaderType, result, isTransitive: false, ctx);
                }
                // Check if it's a bundle attribute
                else if (attr.AttributeClass?.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString() == "ResourceLoader.Attributes.LoaderBundleAttribute") == true)
                {
                    ProcessBundle(attr.AttributeClass!, result, isTransitive: false, ctx);
                }
            }

            return result;
        }

        private static void ProcessBundle(
            INamedTypeSymbol bundleSymbol,
            Dictionary<string, LoaderInfo> result,
            bool isTransitive,
            SourceProductionContext ctx)
        {
            foreach (AttributeData attr in bundleSymbol.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == "ResourceLoader.Attributes.RegisterLoaderAttribute")
                {
                    if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol loaderType) continue;

                    // Check if this loader is itself a bundle
                    bool isBundleLoader = loaderType.GetAttributes().Any(a =>
                        a.AttributeClass?.ToDisplayString() == "ResourceLoader.Attributes.LoaderBundleAttribute");

                    if (isBundleLoader)
                        ProcessBundle(loaderType, result, isTransitive: true, ctx);
                    else
                        RegisterLoader(loaderType, result, isTransitive, ctx);
                }
            }
        }
    }
}
