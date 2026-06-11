// Copyright (c) NullShield Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace NullShield.Generators;

/// <summary>
/// Lightweight, value-equatable model representing a single symbol decorated with
/// <c>[NullShield]</c>, as extracted by the incremental generator pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Value equality (provided automatically by C# records) is critical for Roslyn's
/// incremental caching model.  Roslyn compares successive pipeline outputs using
/// <see cref="object.Equals(object?)"/>; if the record compares equal to its previous
/// version, downstream stages are skipped entirely — giving NullShield its
/// "fluid IDE performance during live typing" guarantee.
/// </para>
/// <para>
/// All members are plain value types or <see cref="string"/>, which are inherently
/// value-equatable.  No <see cref="Microsoft.CodeAnalysis.ISymbol"/> or
/// <see cref="Microsoft.CodeAnalysis.SyntaxNode"/> references are stored here to
/// prevent stale-symbol bugs between incremental pipeline runs.
/// </para>
/// </remarks>
/// <param name="SymbolName">
/// The unqualified name of the decorated class or method (e.g., <c>"OrderService"</c>).
/// </param>
/// <param name="FullyQualifiedName">
/// The fully-qualified display name of the symbol, formatted with
/// <c>SymbolDisplayFormat.FullyQualifiedFormat</c>
/// (e.g., <c>"global::Acme.Orders.OrderService"</c>).
/// </param>
/// <param name="ContainingNamespace">
/// The containing namespace display string, or <see cref="string.Empty"/> for the
/// global namespace.
/// </param>
/// <param name="IsClass">
/// <see langword="true"/> if the decorated symbol is a class; <see langword="false"/>
/// if it is a method.
/// </param>
/// <param name="MitigationStrategyValue">
/// The raw integer value of the <c>MitigationStrategy</c> flags combination, as
/// extracted from the attribute's constructor argument.  Stored as an <see cref="int"/>
/// to avoid a compile-time dependency on the Core enum type within the generator
/// assembly (which would cause reference resolution issues in the Roslyn host process).
/// </param>
/// <param name="LoggerTypeFullName">
/// The fully-qualified display name of the optional custom logger type, or
/// <see langword="null"/> if not specified.
/// </param>
/// <param name="ForceGuard">
/// Mirrors <c>NullShieldAttribute.ForceGuard</c>; when <see langword="true"/>, guard
/// code is emitted even for parameters the compiler has proven non-null.
/// </param>
internal sealed record NullShieldTarget(
    string SymbolName,
    string FullyQualifiedName,
    string ContainingNamespace,
    string ContainingTypeName,
    bool IsClass,
    int MitigationStrategyValue,
    string? LoggerTypeFullName,
    bool ForceGuard,
    bool IsPrimaryConstructor,
    System.Collections.Immutable.ImmutableArray<(string Name, string Type)> Parameters);
