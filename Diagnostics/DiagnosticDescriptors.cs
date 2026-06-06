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
    /// Emitted when <c>[NullShield]</c> is applied to a class that is not declared
    /// <c>partial</c>.  The generator cannot emit the guard file without partial support.
    /// </summary>
    internal static readonly DiagnosticDescriptor ClassMustBePartial = new(
        id: "NS0003",
        title: "NullShield-decorated class must be partial",
        messageFormat: "Class '{0}' is decorated with [NullShield] but is not declared 'partial'. " +
                       "NullShield cannot inject guard code into a non-partial class. " +
                       "Add the 'partial' modifier or apply [NullShield] to individual methods instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "The NullShield source generator emits guard code in a separate, generated file using " +
            "C# partial class syntax. The decorated class must therefore be declared 'partial'.",
        helpLinkUri: "https://github.com/NullShield/NullShield/docs/diagnostics/NS0003.md");
}
