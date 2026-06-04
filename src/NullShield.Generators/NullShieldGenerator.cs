// Copyright (c) NullShield Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NullShield.Generators;

/// <summary>
/// The NullShield Roslyn Incremental Source Generator.
/// </summary>
/// <remarks>
/// <para>
/// This generator uses <see cref="IncrementalGeneratorInitializationContext.SyntaxProvider"/>
/// with <c>ForAttributeWithMetadataName</c> — the recommended, cache-friendly API introduced
/// in Microsoft.CodeAnalysis 4.3.1.  It avoids the legacy <c>ISyntaxReceiver</c> pattern and
/// participates fully in Roslyn's incremental caching model, ensuring that the generator
/// pipeline only re-executes for nodes that have actually changed.
/// </para>
/// <para>
/// <b>Pipeline overview (Phase 2 skeleton):</b>
/// <list type="number">
///   <item><description>
///     <b>Syntax filter</b> — <c>ForAttributeWithMetadataName</c> efficiently narrows the
///     full compilation down to only those class or method declarations that carry
///     <c>[NullShield]</c>.
///   </description></item>
///   <item><description>
///     <b>Semantic transform</b> — Extracts a lightweight, value-equatable
///     <see cref="NullShieldTarget"/> record from each decorated symbol.  The record's
///     value-equality is critical: Roslyn uses it to determine whether downstream stages
///     need to re-execute after an incremental change.
///   </description></item>
///   <item><description>
///     <b>Source output</b> — <c>RegisterSourceOutput</c> emits a placeholder comment file
///     per target in Phase 2.  The full guard-code emitter is implemented in Phase 3
///     (<c>SourceEmitter.cs</c>).
///   </description></item>
/// </list>
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class NullShieldGenerator : IIncrementalGenerator
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fully-qualified metadata name of <c>NullShieldAttribute</c>.
    /// This string is used by <c>ForAttributeWithMetadataName</c> to perform an
    /// efficient, index-backed lookup without scanning the entire syntax tree.
    /// </summary>
    private const string NullShieldAttributeFullName =
        "NullShield.Core.Attributes.NullShieldAttribute";

    /// <summary>
    /// Fully-qualified metadata name of <c>MitigationStrategy</c>.
    /// Used during semantic extraction to resolve the enum constant values.
    /// </summary>
    private const string MitigationStrategyFullName =
        "NullShield.Core.Enums.MitigationStrategy";

    // -------------------------------------------------------------------------
    // IIncrementalGenerator
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // -----------------------------------------------------------------
        // Stage 1: Syntax filter
        // ForAttributeWithMetadataName is the recommended incremental API.
        // It performs a fast, pre-computed index lookup keyed on the attribute's
        // simple name ("NullShieldAttribute") and then validates the full
        // metadata name, keeping the hot path allocation-free.
        // -----------------------------------------------------------------
        IncrementalValuesProvider<NullShieldTarget?> targetProvider =
            context.SyntaxProvider
                   .ForAttributeWithMetadataName(
                       fullyQualifiedMetadataName: NullShieldAttributeFullName,
                       predicate: static (node, _) => IsSupportedSyntaxNode(node),
                       transform: static (ctx, ct) => ExtractTarget(ctx, ct))
                   .Where(static target => target is not null);

        // -----------------------------------------------------------------
        // Stage 2: Collect diagnostics separately so they can be reported
        // independently of source output.  This prevents a diagnostic-only
        // change from invalidating the cached source output step.
        // -----------------------------------------------------------------
        IncrementalValuesProvider<Diagnostic> diagnosticsProvider =
            context.SyntaxProvider
                   .ForAttributeWithMetadataName(
                       fullyQualifiedMetadataName: NullShieldAttributeFullName,
                       predicate: static (node, _) => IsSupportedSyntaxNode(node),
                       transform: static (ctx, ct) => ExtractDiagnostics(ctx, ct))
                   .SelectMany(static (diags, _) => diags);

        context.RegisterSourceOutput(diagnosticsProvider,
            static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

        // -----------------------------------------------------------------
        // Stage 3: Source output
        // Phase 2 emits a lightweight placeholder per target.
        // The full SourceEmitter (Phase 3) will replace this stub.
        // -----------------------------------------------------------------
        context.RegisterSourceOutput(targetProvider,
            static (spc, target) => EmitPlaceholder(spc, target!));
    }

    // -------------------------------------------------------------------------
    // Syntax predicate (Stage 1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns <see langword="true"/> for the syntax node kinds that
    /// <see cref="NullShieldAttributeFullName"/> is permitted to decorate:
    /// class declarations and method declarations.
    /// </summary>
    /// <remarks>
    /// This predicate runs on every attribute application site in the compilation
    /// and must be allocation-free and branch-minimal.
    /// </remarks>
    private static bool IsSupportedSyntaxNode(SyntaxNode node) =>
        node is ClassDeclarationSyntax or MethodDeclarationSyntax;

    // -------------------------------------------------------------------------
    // Semantic transform (Stage 2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts a <see cref="NullShieldTarget"/> from a decorated symbol's
    /// <see cref="GeneratorAttributeSyntaxContext"/>.
    /// </summary>
    /// <param name="context">
    /// Provides access to the decorated <see cref="ISymbol"/> and its
    /// <see cref="AttributeData"/> with semantic type information already resolved.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for IDE responsiveness.</param>
    /// <returns>
    /// A populated <see cref="NullShieldTarget"/>, or <see langword="null"/> if the
    /// attribute data cannot be semantically resolved (e.g., missing Core reference).
    /// </returns>
    private static NullShieldTarget? ExtractTarget(
        GeneratorAttributeSyntaxContext context,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ISymbol targetSymbol = context.TargetSymbol;
        AttributeData attributeData = context.Attributes[0];

        // Resolve the Strategy constructor argument (first positional argument).
        if (attributeData.ConstructorArguments.Length == 0)
        {
            return null;
        }

        TypedConstant strategyArg = attributeData.ConstructorArguments[0];
        if (strategyArg.Value is not int strategyValue)
        {
            return null;
        }

        // Resolve optional LoggerType named argument.
        ITypeSymbol? loggerType = null;
        foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == "LoggerType" && namedArg.Value.Value is ITypeSymbol typeSymbol)
            {
                loggerType = typeSymbol;
                break;
            }
        }

        // Resolve optional ForceGuard named argument.
        bool forceGuard = false;
        foreach (KeyValuePair<string, TypedConstant> namedArg in attributeData.NamedArguments)
        {
            if (namedArg.Key == "ForceGuard" && namedArg.Value.Value is bool forceValue)
            {
                forceGuard = forceValue;
                break;
            }
        }

        bool isClass = targetSymbol is INamedTypeSymbol;

        return new NullShieldTarget(
            SymbolName: targetSymbol.Name,
            FullyQualifiedName: targetSymbol.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat),
            ContainingNamespace: targetSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            IsClass: isClass,
            MitigationStrategyValue: strategyValue,
            LoggerTypeFullName: loggerType?.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat),
            ForceGuard: forceGuard);
    }

    // -------------------------------------------------------------------------
    // Diagnostic extraction (Stage 2 — separate pipeline branch)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Produces compile-time diagnostics for invalid or degenerate attribute usages
    /// detected during the incremental pipeline's semantic transform stage.
    /// </summary>
    private static ImmutableArray<Diagnostic> ExtractDiagnostics(
        GeneratorAttributeSyntaxContext context,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = ImmutableArray.CreateBuilder<Diagnostic>();
        AttributeData attributeData = context.Attributes[0];

        // NS0001: Strategy is None (no-op attribute usage).
        if (attributeData.ConstructorArguments.Length > 0 &&
            attributeData.ConstructorArguments[0].Value is int val &&
            val == 0)
        {
            Location location = context.TargetNode.GetLocation();
            builder.Add(Diagnostic.Create(
                DiagnosticDescriptors.StrategyIsNone,
                location,
                context.TargetSymbol.Name));
        }

        // NS0003: [NullShield] applied to a non-partial class — generator cannot emit.
        if (context.TargetSymbol is INamedTypeSymbol &&
            context.TargetNode is ClassDeclarationSyntax classDecl &&
            !classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            builder.Add(Diagnostic.Create(
                DiagnosticDescriptors.ClassMustBePartial,
                context.TargetNode.GetLocation(),
                context.TargetSymbol.Name));
        }

        return builder.ToImmutable();
    }

    // -------------------------------------------------------------------------
    // Source output (Stage 3 — Phase 2 placeholder)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emits a per-target placeholder source file.
    /// The full emitter (<c>SourceEmitter.cs</c>) will replace this in Phase 3.
    /// </summary>
    private static void EmitPlaceholder(SourceProductionContext spc, NullShieldTarget target)
    {
        string hintName = BuildHintName(target);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// NullShield Source Generator — Phase 2 Placeholder");
        sb.AppendLine($"// Target  : {target.FullyQualifiedName}");
        sb.AppendLine($"// Kind    : {(target.IsClass ? "Class" : "Method")}");
        sb.AppendLine($"// Strategy: 0x{target.MitigationStrategyValue:X2} ({DescribeStrategy(target.MitigationStrategyValue)})");
        if (target.LoggerTypeFullName is not null)
        {
            sb.AppendLine($"// Logger  : {target.LoggerTypeFullName}");
        }
        sb.AppendLine("//");
        sb.AppendLine("// Full guard-code emission will be implemented in Phase 3 (SourceEmitter.cs).");

        spc.AddSource(hintName, SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a collision-free hint name for the generated source file.
    /// Roslyn uses this as the virtual filename in the generated sources pane.
    /// </summary>
    private static string BuildHintName(NullShieldTarget target)
    {
        // Replace generic arity markers and dots to produce a valid filename.
        string sanitized = target.FullyQualifiedName
            .Replace("global::", string.Empty)
            .Replace('.', '_')
            .Replace('<', '[')
            .Replace('>', ']');

        return $"NullShield.{sanitized}.g.cs";
    }

    /// <summary>
    /// Produces a human-readable description of combined <c>MitigationStrategy</c> flags
    /// for the placeholder comment, without taking a hard dependency on the enum type at
    /// generator compile time.
    /// </summary>
    private static string DescribeStrategy(int value)
    {
        if (value == 0) return "None";

        var parts = new System.Collections.Generic.List<string>(3);
        if ((value & 1) != 0) parts.Add("DefaultInstance");
        if ((value & 2) != 0) parts.Add("ShortCircuit");
        if ((value & 4) != 0) parts.Add("TraceAndSkip");
        if (parts.Count == 0) parts.Add($"Unknown(0x{value:X2})");

        return string.Join(" | ", parts);
    }
}
