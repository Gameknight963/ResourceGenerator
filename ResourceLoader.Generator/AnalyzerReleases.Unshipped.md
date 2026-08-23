; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Unreleased

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
RL0001 | ResourceLoader | Error | Could not determine project directory
RL0002 | ResourceLoader | Error | Resources folder not found
RL0003 | ResourceLoader | Warning | No loader registered
RL0004 | ResourceLoader | Warning | Loader collision
RL0005 | ResourceLoader | Warning | Transitive loader
RL0006 | ResourceLoader | Error | Runtime path member must be static
RL0007 | ResourceLoader | Error | Runtime path member not found
RL0008 | ResourceLoader | Error | Loader override required