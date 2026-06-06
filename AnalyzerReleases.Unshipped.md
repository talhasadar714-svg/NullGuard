; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NS0001  | NullShield | Warning  | MitigationStrategy.None produces no guard code.
NS0002  | NullShield | Warning  | Incompatible LoggerType falls back to Trace.WriteLine.
NS0003  | NullShield | Error    | NullShield-decorated class must be declared partial.
