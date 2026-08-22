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

        private const string ResourceFolderAttributeName = "ResourceLoader.Attributes.ResourceFolderAttribute";
        private const string RegisterLoaderAttributeName = "ResourceLoader.Attributes.RegisterLoaderAttribute";
        private const string LoaderBundleAttributeName = "ResourceLoader.Attributes.LoaderBundleAttribute";
        private const string HandlesExtensionsAttributeName = "ResourceLoader.Attributes.HandlesExtensionsAttribute";
        private const string WarnIfTransitiveAttributeName = "ResourceLoader.Attributes.WarnIfTransitiveAttribute";
        private const string IResourceLoaderName = "ResourceLoader.Attributes.IResourceLoader<T>";

        private static readonly DiagnosticDescriptor MissingProjectDir = new(
            "RL0001",
            "Could not determine project directory",
            "ResourceLoader could not determine the project directory",
            "ResourceLoader",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor ResourceFolderNotFound = new(
            "RL0002",
            "Resources folder not found",
            "ResourceLoader could not find the folder '{0}'",
            "ResourceLoader",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor NoLoaderRegistered = new(
            "RL0003",
            "No loader registered",
            "No loader registered for extension '{0}' (file: '{1}')",
            "ResourceLoader",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor LoaderCollision = new(
            "RL0004",
            "Loader collision",
            "Multiple loaders registered for extension '{0}': '{1}' and '{2}'. '{1}' will be used.",
            "ResourceLoader",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor TransitiveLoader = new(
            "RL0005",
            "Transitive loader",
            "Loader '{0}' for file '{1}' was pulled in transitively. Consider registering it explicitly.",
            "ResourceLoader",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor RuntimePathNotStatic = new(
            "RL0006",
            "Runtime path member must be static",
            "'{0}' must be static because generated resource properties are static",
            "ResourceLoader",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor RuntimePathNotFound = new(
            "RL0007",
            "Runtime path member not found",
            "Could not find member '{0}' on '{1}' or any of its base types",
            "ResourceLoader",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor LoaderOverrideRequired = new(
            "RL0008",
            "Loader override required",
            "Loader '{0}' conflicts with a bundle loader for extension '{1}'. " +
            "Use RegisterLoader(typeof({0}), overrideBundle: true) to override.",
            "ResourceLoader",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

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
                if (attr.AttributeClass?.ToDisplayString() == ResourceFolderAttributeName)
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
                ctx.ReportDiagnostic(Diagnostic.Create(MissingProjectDir, Location.None));
                return;
            }

            AttributeData? resourceFolderAttr = classSymbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == ResourceFolderAttributeName);

            if (resourceFolderAttr is null) return;

            string scanPath = (string)resourceFolderAttr.ConstructorArguments[0].Value!;
            string runtimePath = (string)resourceFolderAttr.ConstructorArguments[1].Value!;
            string fullScanPath = Path.Combine(projectDir, scanPath);

            Location runtimePathLocation = resourceFolderAttr.ApplicationSyntaxReference
                ?.GetSyntax()
                is AttributeSyntax attrSyntax
                    ? attrSyntax.ArgumentList?.Arguments[1].GetLocation() ?? Location.None
                    : Location.None;

            ISymbol? runtimeSymbol = FindMember(classSymbol, runtimePath);
            if (runtimeSymbol is not null && !runtimeSymbol.IsStatic)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    RuntimePathNotStatic,
                    runtimeSymbol.Locations.FirstOrDefault() ?? Location.None,
                    runtimePath));
                return;
            }
            else if (runtimeSymbol is null)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    RuntimePathNotFound,
                    runtimePathLocation ?? Location.None,
                    runtimePath,
                    classSymbol.Name));
                return;
            }

            if (!Directory.Exists(fullScanPath))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    ResourceFolderNotFound,
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
                        NoLoaderRegistered,
                        Location.None,
                        extension,
                        fullFileName));
                    continue;
                }

                if (loader.WarnIfTransitive && loader.IsTransitive)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        TransitiveLoader,
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
                // <auto-generated/>
                #nullable enable

                namespace {{namespaceName}};

                partial class {{className}}
                {
                {{loaderFields}}
                {{properties}}}
                """;

            ctx.AddSource($"{className}.g.cs", source);
        }

        private static ISymbol? FindMember(INamedTypeSymbol classSymbol, string name)
        {
            INamedTypeSymbol? current = classSymbol;
            while (current is not null)
            {
                ISymbol? member = current.GetMembers(name).FirstOrDefault();
                if (member is not null) return member;
                current = current.BaseType;
            }
            return null;
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
                i.ConstructedFrom.ToDisplayString() == IResourceLoaderName);

            if (loaderInterface is null) return;

            string returnTypeName = loaderInterface.TypeArguments[0].ToDisplayString();
            string loaderTypeName = loaderType.ToDisplayString();

            bool warnIfTransitive = loaderType.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == WarnIfTransitiveAttributeName);

            // Find [HandlesExtensions]
            AttributeData? handlesAttr = loaderType.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == HandlesExtensionsAttributeName);

            if (handlesAttr is null) return;

            IEnumerable<string> extensions = handlesAttr.ConstructorArguments[0]
                .Values.Select(v => (string)v.Value!);

            foreach (string ext in extensions)
            {
                if (result.TryGetValue(ext, out LoaderInfo existing))
                {
                    // Collision - warn and keep existing (direct wins, then first bundle wins)
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        LoaderCollision,
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

            // First pass - bundles
            foreach (AttributeData attr in classSymbol.GetAttributes())
            {
                if (attr.AttributeClass?.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString() == LoaderBundleAttributeName) == true)
                {
                    ProcessBundle(attr.AttributeClass!, result, isTransitive: false, ctx);
                }
            }

            // Second pass - direct [RegisterLoader] attributes, overwriting bundle loaders
            foreach (AttributeData attr in classSymbol.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == RegisterLoaderAttributeName)
                {
                    if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol loaderType) continue;
                    bool overrideBundle = (bool)(attr.ConstructorArguments[1].Value ?? false);

                    AttributeData? handlesAttr = loaderType.GetAttributes().FirstOrDefault(a =>
                        a.AttributeClass?.ToDisplayString() == HandlesExtensionsAttributeName);

                    if (handlesAttr is not null)
                    {
                        bool hasConflict = false;
                        foreach (string ext in handlesAttr.ConstructorArguments[0].Values.Select(v => (string)v.Value!))
                        {
                            if (!result.ContainsKey(ext)) continue;

                            if (overrideBundle)
                                result.Remove(ext);
                            else
                            {
                                ctx.ReportDiagnostic(Diagnostic.Create(
                                    LoaderOverrideRequired,
                                    Location.None,
                                    loaderType.Name,
                                    ext));
                                hasConflict = true;
                            }
                        }
                        if (hasConflict) continue;
                    }

                    RegisterLoader(loaderType, result, isTransitive: false, ctx);
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
                if (attr.AttributeClass?.ToDisplayString() == RegisterLoaderAttributeName)
                {
                    if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol loaderType) continue;

                    // Check if this loader is itself a bundle
                    bool isBundleLoader = loaderType.GetAttributes().Any(a =>
                        a.AttributeClass?.ToDisplayString() == LoaderBundleAttributeName);

                    if (isBundleLoader)
                        ProcessBundle(loaderType, result, isTransitive: true, ctx);
                    else
                        RegisterLoader(loaderType, result, isTransitive, ctx);
                }
            }
        }
    }
}
