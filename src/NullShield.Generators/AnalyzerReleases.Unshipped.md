; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NS0001  | NullShield | Warning  | MitigationStrategy.None produces no guard code.
NS0002  | NullShield | Warning  | Incompatible LoggerType falls back to Trace.WriteLine.
NS0003  | NullShield | Error    | NullShield-decorated type must be declared partial.
NS0004  | NullShield | Warning  | Custom exception type could not be resolved; falling back.
NS1000  | NullShield | Warning  | Redundant [NotNull] on nullable reference type.
NS1001  | NullShield | Info     | Manual null check can be simplified.
NS1002  | NullShield | Info     | Compilation metrics summary.
