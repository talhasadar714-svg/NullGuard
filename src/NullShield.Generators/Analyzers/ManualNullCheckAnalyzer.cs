using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NullShield.Generators.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ManualNullCheckAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.ManualNullCheckCanBeSimplified);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            if (methodDeclaration.Body == null)
                return;

            foreach (var statement in methodDeclaration.Body.Statements)
            {
                // We only check the beginning of the method. Once we see a statement
                // that is not a null check, we can stop, assuming guards are at the top.
                string? checkedParameter = null;
                Location? diagnosticLocation = null;

                if (IsArgumentNullExceptionThrowIfNull(statement, out checkedParameter))
                {
                    diagnosticLocation = statement.GetLocation();
                }
                else if (IsIfNullThrowArgumentNullException(statement, out checkedParameter))
                {
                    diagnosticLocation = statement.GetLocation();
                }
                else
                {
                    // Not a guard statement, stop analyzing further statements in this method
                    break;
                }

                if (checkedParameter != null && diagnosticLocation != null)
                {
                    // Verify that the checked variable is actually a parameter of this method
                    var parameter = methodDeclaration.ParameterList.Parameters
                        .FirstOrDefault(p => p.Identifier.Text == checkedParameter);

                    if (parameter != null)
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ManualNullCheckCanBeSimplified,
                            diagnosticLocation,
                            checkedParameter);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static bool IsArgumentNullExceptionThrowIfNull(StatementSyntax statement, out string? parameterName)
        {
            parameterName = null;

            if (statement is ExpressionStatementSyntax expressionStatement &&
                expressionStatement.Expression is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Name.Identifier.Text == "ThrowIfNull" &&
                    memberAccess.Expression is IdentifierNameSyntax identifierName &&
                    identifierName.Identifier.Text == "ArgumentNullException")
                {
                    if (invocation.ArgumentList.Arguments.Count > 0)
                    {
                        var firstArg = invocation.ArgumentList.Arguments[0].Expression;
                        if (firstArg is IdentifierNameSyntax argName)
                        {
                            parameterName = argName.Identifier.Text;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsIfNullThrowArgumentNullException(StatementSyntax statement, out string? parameterName)
        {
            parameterName = null;

            if (statement is IfStatementSyntax ifStatement)
            {
                // Check condition: `x == null` or `x is null`
                if (IsCheckNullExpression(ifStatement.Condition, out parameterName))
                {
                    // Check body: `throw new ArgumentNullException(...)`
                    StatementSyntax throwStatement = ifStatement.Statement;

                    // If it's a block, look at the first statement
                    if (throwStatement is BlockSyntax block)
                    {
                        if (block.Statements.Count != 1) return false;
                        throwStatement = block.Statements[0];
                    }

                    if (throwStatement is ThrowStatementSyntax throwSyntax && throwSyntax.Expression != null)
                    {
                        if (throwSyntax.Expression is ObjectCreationExpressionSyntax objectCreation &&
                            objectCreation.Type is IdentifierNameSyntax typeName &&
                            typeName.Identifier.Text == "ArgumentNullException")
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsCheckNullExpression(ExpressionSyntax expression, out string? parameterName)
        {
            parameterName = null;

            if (expression is BinaryExpressionSyntax binaryExpr && binaryExpr.IsKind(SyntaxKind.EqualsExpression))
            {
                if (binaryExpr.Right.IsKind(SyntaxKind.NullLiteralExpression) && binaryExpr.Left is IdentifierNameSyntax leftId)
                {
                    parameterName = leftId.Identifier.Text;
                    return true;
                }
                if (binaryExpr.Left.IsKind(SyntaxKind.NullLiteralExpression) && binaryExpr.Right is IdentifierNameSyntax rightId)
                {
                    parameterName = rightId.Identifier.Text;
                    return true;
                }
            }
            else if (expression is IsPatternExpressionSyntax isPatternExpr && isPatternExpr.Pattern is ConstantPatternSyntax constPattern && constPattern.Expression.IsKind(SyntaxKind.NullLiteralExpression))
            {
                if (isPatternExpr.Expression is IdentifierNameSyntax exprId)
                {
                    parameterName = exprId.Identifier.Text;
                    return true;
                }
            }

            return false;
        }
    }
}
