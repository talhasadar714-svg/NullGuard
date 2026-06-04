// Copyright (c) NullShield Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace NullShield.Core.Enums;

/// <summary>
/// Defines the compile-time null-mitigation strategies that NullShield applies when
/// generating guard code for decorated classes and methods.
/// </summary>
/// <remarks>
/// This enumeration is decorated with <see cref="System.FlagsAttribute"/> so that
/// strategies can be combined using the bitwise OR operator.  For example:
/// <code>
/// [NullShield(MitigationStrategy.DefaultInstance | MitigationStrategy.TraceAndSkip)]
/// public void Process(string? input) { /* ... */ }
/// </code>
/// </remarks>
[System.Flags]
public enum MitigationStrategy
{
    /// <summary>
    /// No mitigation is applied.  Used as the default/unset value.
    /// </summary>
    None = 0,

    /// <summary>
    /// Falls back to a safe primitive or zero-value instance when a null argument
    /// is detected.
    /// <list type="bullet">
    ///   <item><description>Strings resolve to <see cref="string.Empty"/>.</description></item>
    ///   <item><description>Arrays resolve to the corresponding empty array singleton (e.g., <c>Array.Empty&lt;T&gt;()</c>).</description></item>
    ///   <item><description>Collections resolve to a new empty instance where a parameterless constructor exists.</description></item>
    ///   <item><description>All other reference types are replaced via their parameterless constructor, if available; otherwise <c>default(T)</c> is used.</description></item>
    /// </list>
    /// </summary>
    /// <example>
    /// <code>
    /// [NullShield(MitigationStrategy.DefaultInstance)]
    /// public void Render(string? label) { /* label is never null here */ }
    /// </code>
    /// </example>
    DefaultInstance = 1 << 0, // 1

    /// <summary>
    /// Immediately returns from the protected method—without throwing—when one or more
    /// invariants fail.  Applicable to <c>void</c> methods and methods returning
    /// <c>Task</c> or <c>ValueTask</c>.
    /// </summary>
    /// <remarks>
    /// For non-void methods, combine with <see cref="DefaultInstance"/> to ensure a
    /// safe return value is produced instead of early-returning a null.
    /// </remarks>
    /// <example>
    /// <code>
    /// [NullShield(MitigationStrategy.ShortCircuit)]
    /// public void Save(Order? order) { /* silently no-ops when order is null */ }
    /// </code>
    /// </example>
    ShortCircuit = 1 << 1, // 2

    /// <summary>
    /// Emits a structured diagnostic trace through the configured logger hook and then
    /// skips the unsafe code path entirely.  The trace includes the method name,
    /// parameter name, and the assembly-qualified type of the null argument.
    /// </summary>
    /// <remarks>
    /// When no custom logger type is provided via
    /// <see cref="Attributes.NullShieldAttribute.LoggerType"/>, the generator falls back
    /// to <c>System.Diagnostics.Trace.WriteLine</c> so that output is visible in any
    /// environment without additional dependencies.
    /// </remarks>
    /// <example>
    /// <code>
    /// [NullShield(MitigationStrategy.TraceAndSkip, LoggerType = typeof(MyAppLogger))]
    /// public void Index(string? key) { /* key == null is logged, then skipped */ }
    /// </code>
    /// </example>
    TraceAndSkip = 1 << 2, // 4
}
