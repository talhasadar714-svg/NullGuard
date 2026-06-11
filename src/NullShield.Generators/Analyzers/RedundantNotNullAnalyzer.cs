using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NullShield.Generators.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RedundantNotNullAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(DiagnosticDescriptors.RedundantNotNull);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Parameter);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var parameterSymbol = (IParameterSymbol)context.Symbol;

            // Check if the parameter has [NotNull]
            bool hasNotNullAttribute = parameterSymbol.GetAttributes().Any(attr => 
                attr.AttributeClass?.ToDisplayString() == "NullShield.Core.Attributes.NotNullAttribute");

            if (!hasNotNullAttribute)
            {
                return;
            }

            // Check if the parameter is explicitly typed as nullable (e.g., string?)
            if (parameterSymbol.Type.NullableAnnotation == NullableAnnotation.Annotated)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.RedundantNotNull,
                    parameterSymbol.Locations[0],
                    parameterSymbol.Name,
                    parameterSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
