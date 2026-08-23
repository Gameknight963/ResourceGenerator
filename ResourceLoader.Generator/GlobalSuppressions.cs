// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "MicrosoftCodeAnalysisCorrectness",
    "RS1035:Do not use APIs banned for analyzers",
    Justification = "File IO is intentional in this generator",
    Scope = "member",
    Target = "~M:ResourceLoader.Generator.ResourceLoaderGenerator.Execute(" +
        "Microsoft.CodeAnalysis.SourceProductionContext," +
        "Microsoft.CodeAnalysis.INamedTypeSymbol," +
        "System.String)")]

[assembly: SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035:Do not use APIs banned for analyzers", Justification = "File IO is intentional in this analyzer", Scope = "member", 
    Target = "~M:ResourceLoader.Generator.ResourceLoaderGenerator.GetFiles" +
    "(System.String,System.Boolean)~" +
    "System.Collections.Generic.IEnumerable{System.ValueTuple{System.String,System.String}}")]
