using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ResourceLoader.Generator
{
    [Generator]
    public sealed class ResourceLoaderGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find all classes that have at least one attribute (cheap pre-filter)
            // then check if any of those attributes is [ResourceFolder]
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context
                .SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
                    transform: static (ctx, _) => GetClassWithResourceFolder(ctx))
                .Where(static c => c is not null)!;

            // Combine with compilation so we can use semantic model later
            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> combined =
                context.CompilationProvider.Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(combined, static (ctx, source) => Execute(ctx, source.Item1, source.Item2));
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
            Compilation compilation,
            ImmutableArray<ClassDeclarationSyntax> classes)
        {
            if (classes.IsDefaultOrEmpty) return;

            foreach (ClassDeclarationSyntax classDecl in classes)
            {
                // Just emit a comment for now to prove it works
                string namespaceName = GetNamespace(classDecl);
                string className = classDecl.Identifier.Text;

                string source = $$"""
                // ResourceLoaderGenerator found: {{namespaceName}}.{{className}}
                """;

                ctx.AddSource($"{className}.g.cs", source);
            }
        }

        private static string GetNamespace(ClassDeclarationSyntax classDecl)
        {
            SyntaxNode? parent = classDecl.Parent;
            while (parent != null)
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
