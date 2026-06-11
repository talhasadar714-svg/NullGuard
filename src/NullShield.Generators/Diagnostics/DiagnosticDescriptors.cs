// Copyright (c) NullShield Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;

namespace NullShield.Generators;

/// <summary>
/// Centralised repository of all <see cref="DiagnosticDescriptor"/> instances emitted
/// by the NullShield Roslyn Source Generator.
/// </summary>
/// <remarks>
/// <list type="table">
///   <listheader>
///     <term>ID</term>
///     <description>Meaning</description>
///   </listheader>
///   <item>
///     <term>NS0001</term>
///     <description><c>MitigationStrategy.None</c> produces no guard code.</description>
///   </item>
///   <item>
///     <term>NS0002</term>
///     <description>Supplied <c>LoggerType</c> has no compatible static log method; falling back to <c>Trace.WriteLine</c>.</description>
///   </item>
///   <item>
///     <term>NS0003</term>
///     <description>Class decorated with <c>[NullShield]</c> must be declared <c>partial</c>.</description>
///   </item>
/// </list>
/// </remarks>
internal static class DiagnosticDescriptors
{
    private const string Category = "NullShield";

    // -------------------------------------------------------------------------
    // NS0001 — Strategy is None
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emitted when a developer specifies <c>MitigationStrategy.None</c>, which
    /// produces no guard code and is equivalent to not applying the attribute at all.
    /// </summary>
    internal static readonly DiagnosticDescriptor StrategyIsNone = new(
        id: "NS0001",
        title: "NullShield attribute has no effect",
        messageFormat: "[NullShield] on '{0}' uses MitigationStrategy.None, which generates no guard code. " +
                       "Specify at least one strategy flag (DefaultInstance, ShortCircuit, or TraceAndSkip).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "MitigationStrategy.None is the zero-value of the flags enum and disables all code generation. " +
            "Remove the attribute or choose an active strategy.",
        helpLinkUri: "https://github.com/NullShield/NullShield/docs/diagnostics/NS0001.md");

    // -------------------------------------------------------------------------
    // NS0002 — Incompatible LoggerType
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emitted when the type supplied to <c>LoggerType</c> does not expose a
    /// compatible static log method.  The generator falls back to
    /// <c>System.Diagnostics.Trace.WriteLine</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor IncompatibleLoggerType = new(
        id: "NS0002",
        title: "NullShield LoggerType has no compatible log method",
        messageFormat: "Logger type '{0}' specified on '{1}' does not expose a compatible static " +
                       "Log(string), WriteLine(string), or Write(string) method. " +
                       "NullShield will fall back to System.Diagnostics.Trace.WriteLine.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "NullShield requires the custom logger type to expose at least one of: " +
            "static void Log(string), static void WriteLine(string), or static void Write(string).",
        helpLinkUri: "https://github.com/NullShield/NullShield/docs/diagnostics/NS0002.md");

    // -------------------------------------------------------------------------
    // NS0003 — Class must be partial
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emitted when <c>[NullShield]</c> or <c>[NotNull]</c> is applied to a type that is not declared
    /// <c>partial</c>.  The generator cannot emit the guard file without partial support.
    /// </summary>
    internal static readonly DiagnosticDescriptor ClassMustBePartial = new(
        id: "NS0003",
        title: "NullShield-decorated type must be partial",
        messageFormat: "Type '{0}' uses NullShield features but is not declared 'partial'. " +
                       "NullShield cannot inject guard code into a non-partial type. " +
                       "Add the 'partial' modifier.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "The NullShield source generator emits guard code using C# partial class syntax. " +
            "The decorated type must therefore be declared 'partial'.",
        helpLinkUri: "https://github.com/NullShield/NullShield/docs/diagnostics/NS0003.md");

    // -------------------------------------------------------------------------
    // Phase 3 Analyzer Diagnostics (NS10xx)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emitted when <c>[NotNull]</c> is applied to a parameter that is explicitly typed as nullable.
    /// </summary>
    internal static readonly DiagnosticDescriptor RedundantNotNull = new(
        id: "NS1000",
        title: "Redundant [NotNull] on nullable reference type",
        messageFormat: "Parameter '{0}' is marked with [NotNull] but its type '{1}' is explicitly nullable. " +
                       "Either remove the [NotNull] attribute or make the type non-nullable.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "Applying [NotNull] to a nullable reference type creates a contradictory contract. " +
            "The compiler will enforce null-safety, but the type signature implies nulls are allowed.",
        helpLinkUri: "https://github.com/NullShield/NullShield/docs/diagnostics/NS1000.md");

    /// <summary>
    /// Emitted when a manual null check can be replaced by a <c>[NotNull]</c> attribute.
    /// </summary>
    internal static readonly DiagnosticDescriptor ManualNullCheckCanBeSimplified = new(
        id: "NS1001",
        title: "Manual null check can be simplified",
        messageFormat: "The manual null check for parameter '{0}' can be replaced by the [NotNull] attribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description:
            "NullShield can automatically generate this null check. Replace the manual throw statement with the [NotNull] attribute.",
        helpLinkUri: "https://github.com/NullShield/NullShield/docs/diagnostics/NS1001.md");

    /// <summary>
    /// Emitted once per compilation to report the total number of guard clauses injected.
    /// </summary>
    internal static readonly DiagnosticDescriptor NullShieldCompilationSummary = new(
        id: "NS1002",
        title: "NullShield Compilation Summary",
        messageFormat: "NullShield successfully injected {0} guard clauses across {1} methods/constructors",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description:
            "Provides a summary metric of the total defensive code generated by NullShield in the current compilation.",
        helpLinkUri: "https://github.com/NullShield/NullShield/docs/diagnostics/NS1002.md");

    // -------------------------------------------------------------------------
    // NS0004 — Unresolvable custom exception type
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emitted when the type supplied in the <c>NullGuard_ExceptionType</c> MSBuild property
    /// cannot be resolved within the current compilation.  The generator falls back to
    /// <c>ArgumentNullException</c>.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnresolvableExceptionType = new(
        id: "NS0004",
        title: "NullShield custom exception type could not be resolved",
        messageFormat: "The custom exception type '{0}' specified in NullGuard_ExceptionType " +
                       "could not be resolved. Falling back to ArgumentNullException.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The type specified in the NullGuard_ExceptionType MSBuild property must be a " +
            "fully-qualified type name that is available in the current compilation. " +
            "Ensure the type is referenced and the name is correct.",
        helpLinkUri: "https://github.com/NullShield/NullShield/docs/diagnostics/NS0004.md");
}
