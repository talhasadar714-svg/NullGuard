using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NullShield.Generators.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualNullCheckCodeFixProvider)), Shared]
    public sealed class ManualNullCheckCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("NS1001");

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the statement that triggered the diagnostic
            var statement = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<StatementSyntax>();
            if (statement == null) return;

            // Find the method declaration
            var methodDeclaration = statement.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (methodDeclaration == null) return;

            // The diagnostic message contains the parameter name in its message arguments
            // We can also just extract it from the method declaration by checking the statement again, 
            // but the diagnostic properties or arguments are easier. 
            // Wait, we didn't pass properties. We can just parse the parameter name from the statement,
            // or pass it via properties. Let's extract it from the diagnostic's format.
            // Actually, we can just find the parameter symbol or syntax using the string we parsed before.
            string? paramName = null;
            if (statement is ExpressionStatementSyntax)
            {
                // ThrowIfNull(x)
                var invocation = (InvocationExpressionSyntax)((ExpressionStatementSyntax)statement).Expression;
                paramName = ((IdentifierNameSyntax)invocation.ArgumentList.Arguments[0].Expression).Identifier.Text;
            }
            else if (statement is IfStatementSyntax ifStmt)
            {
                if (ifStmt.Condition is BinaryExpressionSyntax binExpr)
                {
                    if (binExpr.Left is IdentifierNameSyntax id) paramName = id.Identifier.Text;
                    else if (binExpr.Right is IdentifierNameSyntax id2) paramName = id2.Identifier.Text;
                }
                else if (ifStmt.Condition is IsPatternExpressionSyntax isPattern && isPattern.Expression is IdentifierNameSyntax id)
                {
                    paramName = id.Identifier.Text;
                }
            }

            if (paramName == null) return;

            var parameterSyntax = methodDeclaration.ParameterList.Parameters.FirstOrDefault(p => p.Identifier.Text == paramName);
            if (parameterSyntax == null) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Replace with [NotNull] attribute on '{paramName}'",
                    createChangedDocument: c => ReplaceWithAttributeAsync(context.Document, root, statement, parameterSyntax, methodDeclaration, c),
                    equivalenceKey: nameof(ManualNullCheckCodeFixProvider)),
                diagnostic);
        }

        private async Task<Document> ReplaceWithAttributeAsync(
            Document document,
            SyntaxNode root,
            StatementSyntax statementToRemove,
            ParameterSyntax parameterSyntax,
            MethodDeclarationSyntax methodDeclaration,
            CancellationToken cancellationToken)
        {
            // Create [NotNull] attribute
            var notNullName = SyntaxFactory.IdentifierName("NotNull");
            var attribute = SyntaxFactory.Attribute(notNullName);
            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
                                             .WithTrailingTrivia(SyntaxFactory.Space);

            // Add the attribute to the parameter
            var newParameterSyntax = parameterSyntax.AddAttributeLists(attributeList);

            // Ensure the class is partial and has [NullShield] if needed.
            // For simplicity, we just add [NullShield] to the method.
            // If the method or class already has [NullShield], we don't need to add it.
            // Let's check if the method or class has [NullShield].
            bool hasNullShield = methodDeclaration.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString().Contains("NullShield")));
            
            var classDeclaration = methodDeclaration.Parent as ClassDeclarationSyntax;
            if (!hasNullShield && classDeclaration != null)
            {
                hasNullShield = classDeclaration.AttributeLists.Any(al => al.Attributes.Any(a => a.Name.ToString().Contains("NullShield")));
            }

            MethodDeclarationSyntax newMethodDeclaration = methodDeclaration;

            if (!hasNullShield)
            {
                var nullShieldAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("NullShield"));
                var nsAttributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(nullShieldAttribute))
                                                   .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
                newMethodDeclaration = newMethodDeclaration.AddAttributeLists(nsAttributeList);
            }

            // Replace parameter
            newMethodDeclaration = newMethodDeclaration.ReplaceNode(
                newMethodDeclaration.ParameterList.Parameters.First(p => p.Identifier.Text == parameterSyntax.Identifier.Text),
                newParameterSyntax);

            // Remove the manual check statement
            if (newMethodDeclaration.Body != null)
            {
                var statementInNewMethod = newMethodDeclaration.Body.Statements.FirstOrDefault(s => s.IsEquivalentTo(statementToRemove));
                if (statementInNewMethod != null)
                {
                    newMethodDeclaration = newMethodDeclaration.WithBody(newMethodDeclaration.Body.RemoveNode(statementInNewMethod, SyntaxRemoveOptions.KeepNoTrivia));
                }
            }

            var newRoot = root.ReplaceNode(methodDeclaration, newMethodDeclaration);

            // Ensure using NullShield.Core.Attributes; is present
            var compilationUnit = newRoot as CompilationUnitSyntax;
            if (compilationUnit != null && !compilationUnit.Usings.Any(u => u.Name.ToString() == "NullShield.Core.Attributes"))
            {
                var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("NullShield.Core.Attributes"))
                                                  .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
                newRoot = compilationUnit.AddUsings(usingDirective);
            }

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
