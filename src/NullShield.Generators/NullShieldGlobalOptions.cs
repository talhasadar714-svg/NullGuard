// Copyright (c) NullShield Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace NullShield.Generators;

/// <summary>
/// Immutable, value-equatable model holding project-wide NullShield configuration
/// extracted from MSBuild properties via Roslyn's <c>AnalyzerConfigOptionsProvider.GlobalOptions</c>.
/// </summary>
/// <remarks>
/// <para>
/// Value equality (provided automatically by C# records) is critical for Roslyn's
/// incremental caching model.  If the user hasn't changed their <c>.csproj</c> configuration
/// between builds, Roslyn will compare this record equal to its previous version and skip
/// all downstream pipeline stages — keeping IDE responsiveness optimal.
/// </para>
/// <para>
/// The two supported MSBuild properties are:
/// <list type="bullet">
///   <item><c>NullGuard_ExceptionType</c> — fully-qualified or simple name of the exception to throw.</item>
///   <item><c>NullGuard_MessageTemplate</c> — a <c>string.Format</c>-style template where <c>{0}</c> is the parameter name.</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="ExceptionType">
/// The exception type name to use in generated guard clauses.
/// Defaults to <c>"ArgumentNullException"</c> when the MSBuild property is absent or empty.
/// </param>
/// <param name="MessageTemplate">
/// An optional <c>string.Format</c>-style message template where <c>{0}</c> is replaced
/// with the guarded parameter name at generation time.  When <c>null</c>, the generator
/// uses the default <c>nameof(param)</c> style without a custom message.
/// </param>
internal sealed record NullShieldGlobalOptions(
    string ExceptionType,
    string? MessageTemplate);
