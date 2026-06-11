// Copyright (c) NullShield Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    /// Fully-qualified metadata name of <c>NotNullAttribute</c>.
    /// </summary>
    private const string NotNullAttributeFullName =
        "NullShield.Core.Attributes.NotNullAttribute";

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
        // Stage 0: Global configuration (MSBuild properties)
        // Reads NullGuard_ExceptionType and NullGuard_MessageTemplate from
        // the consumer's .csproj / Directory.Build.props via Roslyn's
        // AnalyzerConfigOptionsProvider.  The resulting record is cached by
        // value equality — if the user hasn't changed their config, all
        // downstream stages are skipped.
        // -----------------------------------------------------------------
        IncrementalValueProvider<NullShieldGlobalOptions> globalOptionsProvider =
            context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue(
                    "build_property.NullGuard_ExceptionType", out string? exceptionType);
                provider.GlobalOptions.TryGetValue(
                    "build_property.NullGuard_MessageTemplate", out string? messageTemplate);

                return new NullShieldGlobalOptions(
                    ExceptionType: string.IsNullOrWhiteSpace(exceptionType)
                        ? "ArgumentNullException"
                        : exceptionType!.Trim(),
                    MessageTemplate: string.IsNullOrWhiteSpace(messageTemplate)
                        ? null
                        : messageTemplate!.Trim());
            });

        // -----------------------------------------------------------------
        // Stage 1: Syntax filter
        // ForAttributeWithMetadataName is the recommended incremental API.
        // It performs a fast, pre-computed index lookup keyed on the attribute's
        // simple name ("NullShieldAttribute") and then validates the full
        // metadata name, keeping the hot path allocation-free.
        // -----------------------------------------------------------------
        IncrementalValuesProvider<NullShieldTarget?> methodTargetProvider =
            context.SyntaxProvider
                   .ForAttributeWithMetadataName(
                       fullyQualifiedMetadataName: NullShieldAttributeFullName,
                       predicate: static (node, _) => IsSupportedSyntaxNode(node),
                       transform: static (ctx, ct) => ExtractTarget(ctx, ct))
                   .Where(static target => target is not null);

        IncrementalValuesProvider<NullShieldTarget?> parameterTargetProvider =
            context.SyntaxProvider
                   .ForAttributeWithMetadataName(
                       fullyQualifiedMetadataName: NotNullAttributeFullName,
                       predicate: static (node, _) => IsSupportedParameterNode(node),
                       transform: static (ctx, ct) => ExtractParameterTarget(ctx, ct))
                   .Where(static target => target is not null);

        IncrementalValuesProvider<NullShieldTarget> allTargetsProvider =
            methodTargetProvider
                .Collect()
                .Combine(parameterTargetProvider.Collect())
                .SelectMany(static (pair, _) => pair.Left.Concat(pair.Right).Where(static t => t is not null).Select(static t => t!).Distinct());

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

        IncrementalValuesProvider<Diagnostic> parameterDiagnosticsProvider =
            context.SyntaxProvider
                   .ForAttributeWithMetadataName(
                       fullyQualifiedMetadataName: NotNullAttributeFullName,
                       predicate: static (node, _) => IsSupportedParameterNode(node),
                       transform: static (ctx, ct) => ExtractParameterDiagnostics(ctx, ct))
                   .SelectMany(static (diags, _) => diags);

        context.RegisterSourceOutput(diagnosticsProvider,
            static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

        context.RegisterSourceOutput(parameterDiagnosticsProvider,
            static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

        // -----------------------------------------------------------------
        // Stage 3: Source output
        // Combines per-target data with global options, then emits a guard
        // class for each decorated method.  The Combine ensures global
        // options changes also trigger re-emission.
        // -----------------------------------------------------------------
        IncrementalValuesProvider<(NullShieldTarget Target, NullShieldGlobalOptions Options)> combined =
            allTargetsProvider
                .Combine(globalOptionsProvider)
                .Select(static (pair, _) => (Target: pair.Left, Options: pair.Right));

        context.RegisterSourceOutput(combined,
            static (spc, data) => EmitGuardClass(spc, data.Target, data.Options));

        // -----------------------------------------------------------------
        // Stage 4: Compilation Metrics
        // -----------------------------------------------------------------
        IncrementalValueProvider<ImmutableArray<NullShieldTarget>> collectedTargets = allTargetsProvider.Collect();

        context.RegisterSourceOutput(collectedTargets, static (spc, targets) =>
        {
            if (targets.IsDefaultOrEmpty) return;

            int methodsCount = targets.Length;
            int guardsCount = targets.Sum(static t => t.Parameters.Length);

            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.NullShieldCompilationSummary,
                Location.None,
                guardsCount,
                methodsCount);

            spc.ReportDiagnostic(diagnostic);
        });
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

    /// <summary>
    /// Returns <see langword="true"/> for ParameterSyntax whose parent's parent is
    /// a ClassDeclarationSyntax, StructDeclarationSyntax, or RecordDeclarationSyntax,
    /// indicating a primary constructor parameter.
    /// </summary>
    private static bool IsSupportedParameterNode(SyntaxNode node) =>
        node is ParameterSyntax { Parent.Parent: ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax };

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

        var parameters = ImmutableArray.CreateBuilder<(string, string)>();
        if (targetSymbol is IMethodSymbol methodSymbol)
        {
            foreach (var p in methodSymbol.Parameters)
            {
                parameters.Add((p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
        }

        return new NullShieldTarget(
            SymbolName: targetSymbol.Name,
            FullyQualifiedName: targetSymbol.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat),
            ContainingNamespace: targetSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            ContainingTypeName: targetSymbol.ContainingType?.Name ?? string.Empty,
            IsClass: isClass,
            MitigationStrategyValue: strategyValue,
            LoggerTypeFullName: loggerType?.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat),
            ForceGuard: forceGuard,
            IsPrimaryConstructor: false,
            Parameters: parameters.ToImmutable());
    }

    /// <summary>
    /// Extracts a <see cref="NullShieldTarget"/> for a primary constructor from a parameter decorated with [NotNull].
    /// </summary>
    private static NullShieldTarget? ExtractParameterTarget(
        GeneratorAttributeSyntaxContext context,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not IParameterSymbol parameterSymbol)
            return null;

        if (parameterSymbol.ContainingSymbol is not IMethodSymbol constructorMethod || constructorMethod.MethodKind != MethodKind.Constructor)
            return null;

        INamedTypeSymbol targetSymbol = constructorMethod.ContainingType;

        bool isClass = targetSymbol.TypeKind == TypeKind.Class;
        int strategyValue = 0; // Default MitigationStrategy.ThrowException
        bool forceGuard = false;
        string? loggerTypeFullName = null;

        foreach (var attr in targetSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == NullShieldAttributeFullName)
            {
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int sv)
                    strategyValue = sv;

                foreach (KeyValuePair<string, TypedConstant> namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "LoggerType" && namedArg.Value.Value is ITypeSymbol ts)
                        loggerTypeFullName = ts.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (namedArg.Key == "ForceGuard" && namedArg.Value.Value is bool forceValue)
                        forceGuard = forceValue;
                }
                break;
            }
        }

        var parameters = ImmutableArray.CreateBuilder<(string, string)>();
        foreach (var p in constructorMethod.Parameters)
        {
            if (p.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == NotNullAttributeFullName))
            {
                parameters.Add((p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            }
        }

        return new NullShieldTarget(
            SymbolName: "PrimaryConstructor",
            FullyQualifiedName: targetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ".PrimaryConstructor",
            ContainingNamespace: targetSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            ContainingTypeName: targetSymbol.Name,
            IsClass: isClass,
            MitigationStrategyValue: strategyValue,
            LoggerTypeFullName: loggerTypeFullName,
            ForceGuard: forceGuard,
            IsPrimaryConstructor: true,
            Parameters: parameters.ToImmutable());
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

        // NS0001: Invalid strategy.
        if (attributeData.ConstructorArguments.Length > 0 &&
            attributeData.ConstructorArguments[0].Value is int val &&
            (val < 0 || val > 2))
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

    /// <summary>
    /// Produces compile-time diagnostics for primary constructor parameters.
    /// </summary>
    private static ImmutableArray<Diagnostic> ExtractParameterDiagnostics(
        GeneratorAttributeSyntaxContext context,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = ImmutableArray.CreateBuilder<Diagnostic>();

        if (context.TargetSymbol is IParameterSymbol parameterSymbol &&
            parameterSymbol.ContainingSymbol is IMethodSymbol constructorMethod &&
            constructorMethod.MethodKind == MethodKind.Constructor)
        {
            var targetNode = context.TargetNode.Parent?.Parent;
            if (targetNode is TypeDeclarationSyntax typeDecl && !typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                builder.Add(Diagnostic.Create(
                    DiagnosticDescriptors.ClassMustBePartial,
                    context.TargetNode.GetLocation(),
                    constructorMethod.ContainingType.Name));
            }
        }

        return builder.ToImmutable();
    }

    // -------------------------------------------------------------------------
    // Source output (Stage 3)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emits a per-target guard class source file, using the global configuration
    /// to determine the exception type and message format.
    /// </summary>
    private static void EmitGuardClass(
        SourceProductionContext spc,
        NullShieldTarget target,
        NullShieldGlobalOptions options)
    {
        string hintName = BuildHintName(target);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");

        if (!string.IsNullOrEmpty(target.ContainingNamespace))
        {
            sb.AppendLine($"namespace {target.ContainingNamespace}");
            sb.AppendLine("{");
        }

        string className = $"{target.ContainingTypeName}_{target.SymbolName}";

        sb.AppendLine($"    public static class NullShield_Guard_{className}");
        sb.AppendLine("    {");

        var paramList = string.Join(", ",
            target.Parameters.Select(p => $"{p.Type} {p.Name}"));
        sb.AppendLine($"        public static void ValidateParameters({paramList})");
        sb.AppendLine("        {");

        foreach (var p in target.Parameters)
        {
            if (target.MitigationStrategyValue == 0) // ThrowException
            {
                EmitThrowStatement(sb, p.Name, options);
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");

        if (target.IsPrimaryConstructor)
        {
            sb.AppendLine();
            sb.AppendLine($"    public partial class {target.ContainingTypeName}");
            sb.AppendLine("    {");
            
            var paramNames = string.Join(", ", target.Parameters.Select(p => p.Name));
            sb.AppendLine($"        private readonly int __nullShieldGuard_init = NullShield_Guard_{className}.ValidateParameters({paramNames});");
            // Add a simple return 0 method to ValidateParameters if IsPrimaryConstructor so it can be assigned to int.
            
            sb.AppendLine("    }");
        }

        if (!string.IsNullOrEmpty(target.ContainingNamespace))
        {
            sb.AppendLine("}");
        }

        // Fix ValidateParameters signature to return int for primary constructors
        if (target.IsPrimaryConstructor)
        {
            sb.Replace($"public static void ValidateParameters({paramList})", $"public static int ValidateParameters({paramList})");
            sb.Replace("        }", "            return 0;\n        }");
        }

        spc.AddSource(hintName, SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    // -------------------------------------------------------------------------
    // Emit helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emits a null-check + throw statement for a single parameter,
    /// respecting the global exception type and optional message template.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> accumulating the generated source.</param>
    /// <param name="paramName">The name of the parameter being guarded.</param>
    /// <param name="options">The project-wide configuration determining exception type and message format.</param>
    private static void EmitThrowStatement(
        StringBuilder sb,
        string paramName,
        NullShieldGlobalOptions options)
    {
        string exceptionType = options.ExceptionType;
        string constructorArgs;

        if (options.MessageTemplate is not null)
        {
            // Format the template: replace {0} with the parameter name.
            string message = options.MessageTemplate.Replace("{0}", paramName);

            if (exceptionType == "ArgumentNullException")
            {
                // ArgumentNullException(string paramName, string message)
                constructorArgs = $"nameof({paramName}), \"{message}\"";
            }
            else
            {
                // Custom exception: assume (string message) constructor.
                constructorArgs = $"\"{message}\"";
            }
        }
        else
        {
            // No message template — use nameof(param) as the sole argument.
            constructorArgs = $"nameof({paramName})";
        }

        sb.AppendLine(
            $"            if ({paramName} == null) throw new {exceptionType}({constructorArgs});");
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
