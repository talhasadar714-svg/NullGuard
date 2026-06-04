// Copyright (c) NullShield Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using NullShield.Core.Enums;

namespace NullShield.Core.Attributes;

/// <summary>
/// Marks a class or method for compile-time null-safety analysis and guard-code
/// generation by the NullShield Roslyn Source Generator.
/// </summary>
/// <remarks>
/// <para>
/// When applied to a <b>class</b>, all public and internal methods within that class
/// inherit the configured <see cref="Strategy"/> unless individually overridden by
/// their own <see cref="NullShieldAttribute"/>.
/// </para>
/// <para>
/// When applied to a <b>method</b>, only that method's nullable parameters are guarded.
/// </para>
/// <para>
/// The containing type must be declared as <c>partial</c> so that the generator can
/// emit the guard scaffolding in a separate, generated file without modifying the
/// original source.
/// </para>
/// </remarks>
/// <example>
/// Protect an entire class with the <see cref="MitigationStrategy.ShortCircuit"/> strategy:
/// <code>
/// [NullShield(MitigationStrategy.ShortCircuit)]
/// public partial class OrderService
/// {
///     public void Submit(Order? order) { /* guard injected by generator */ }
/// }
/// </code>
///
/// Protect a single method while overriding with a combined strategy and custom logger:
/// <code>
/// [NullShield(
///     MitigationStrategy.DefaultInstance | MitigationStrategy.TraceAndSkip,
///     LoggerType = typeof(AppLogger))]
/// public partial void Render(string? title, IEnumerable&lt;Item&gt;? items) { }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    Inherited = false,
    AllowMultiple = false)]
public sealed class NullShieldAttribute : Attribute
{
    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initializes a new instance of <see cref="NullShieldAttribute"/> with the
    /// specified mitigation strategy.
    /// </summary>
    /// <param name="strategy">
    /// The <see cref="MitigationStrategy"/> (or bitwise combination thereof) that the
    /// generator will apply to null-unsafe parameters detected on the decorated symbol.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown at runtime if <paramref name="strategy"/> equals
    /// <see cref="MitigationStrategy.None"/>, which would render the attribute a no-op.
    /// The generator also emits a compile-time warning (NS0001) for this case.
    /// </exception>
    public NullShieldAttribute(MitigationStrategy strategy)
    {
        if (strategy == MitigationStrategy.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(strategy),
                "NullShieldAttribute requires at least one MitigationStrategy flag to be set. " +
                "Using MitigationStrategy.None produces no guard code and is equivalent to " +
                "not applying the attribute at all.");
        }

        Strategy = strategy;
    }

    // -------------------------------------------------------------------------
    // Required Properties
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets the mitigation strategy (or bitwise combination of strategies) that the
    /// NullShield generator will apply to all guarded parameters on the decorated symbol.
    /// </summary>
    /// <value>
    /// A <see cref="MitigationStrategy"/> flags value.  Defaults to
    /// <see cref="MitigationStrategy.DefaultInstance"/> when constructed with that flag.
    /// </value>
    public MitigationStrategy Strategy { get; }

    // -------------------------------------------------------------------------
    // Optional Properties
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets an optional custom logger type that the generator will wire into
    /// generated <see cref="MitigationStrategy.TraceAndSkip"/> guard code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The supplied type must expose a static method matching one of the following
    /// signatures (in priority order):
    /// </para>
    /// <list type="number">
    ///   <item><description><c>static void Log(string message)</c></description></item>
    ///   <item><description><c>static void WriteLine(string message)</c></description></item>
    ///   <item><description><c>static void Write(string message)</c></description></item>
    /// </list>
    /// <para>
    /// If no compatible method is found, the generator falls back to
    /// <c>System.Diagnostics.Trace.WriteLine</c> and emits a compile-time warning
    /// (NS0002) identifying the incompatible logger type.
    /// </para>
    /// <para>
    /// This property has no effect when <see cref="Strategy"/> does not include
    /// <see cref="MitigationStrategy.TraceAndSkip"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [NullShield(MitigationStrategy.TraceAndSkip, LoggerType = typeof(MyAppLogger))]
    /// public partial void Process(Payload? payload) { }
    /// </code>
    /// </example>
    public Type? LoggerType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the generated guard code should be
    /// emitted even when the compiler's nullable analysis has already determined that
    /// a parameter cannot be null at the call site.
    /// </summary>
    /// <remarks>
    /// Setting this to <see langword="true"/> is useful in mixed-nullable codebases or
    /// when consuming assemblies that were compiled without nullable annotations.
    /// Defaults to <see langword="false"/>.
    /// </remarks>
    public bool ForceGuard { get; set; }
}
