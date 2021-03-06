﻿namespace ContracsReSharperInterop
{
    using System.Collections.Immutable;
    using System.Composition;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeActions;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    using TomsToolbox.Core;

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ContractForNotNullCodeFixProvider)), Shared]
    internal class ContractForNotNullCodeFixProvider : CodeFixProvider
    {
        private const string UsingDirectiveName = "System.Diagnostics.Contracts";
        private const string Title = "Add Contract for [NotNull] annotation";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ContractForNotNullAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;

                var syntaxNode = root.FindNode(diagnosticSpan);
                if (syntaxNode == null)
                    return;

                var codeAction = CodeAction.Create(Title, c => AddContractAsync(context.Document, syntaxNode, c), Title);

                context.RegisterCodeFix(codeAction, diagnostic);
            }
        }

        private static async Task<Document> AddContractAsync([NotNull] Document document, [NotNull] SyntaxNode node, CancellationToken cancellationToken)
        {
            // ReSharper disable once PossibleNullReferenceException
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) as CompilationUnitSyntax;
            if (root == null)
                return document;

            // ReSharper disable once PossibleNullReferenceException
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var hasUsingDirective = root.HasUsingDirective(node, UsingDirectiveName);

            var newRoot = node.TryCast().Returning<CompilationUnitSyntax>()
                .When<ParameterSyntax>(syntax => AddRequires(root, semanticModel, syntax))
                .When<MethodDeclarationSyntax>(syntax => AddEnsures(root, semanticModel, syntax))
                .When<FieldDeclarationSyntax>(syntax => AddInvariant(root, semanticModel, syntax))
                .Else(syntax => root);

            Debug.Assert(newRoot != null);

            if (!hasUsingDirective)
            {
                newRoot = newRoot.AddUsingDirective(UsingDirectiveName);
            }

            return document.WithSyntaxRoot(newRoot);
        }

        private static CompilationUnitSyntax AddRequires(CompilationUnitSyntax root, SemanticModel semanticModel, [NotNull] ParameterSyntax parameterSyntax)
        {
            var methodSyntax = parameterSyntax.Parent?.Parent as BaseMethodDeclarationSyntax;
            if (methodSyntax == null)
                return root;

            var body = methodSyntax.Body;
            if (body == null)
                return AddRequiresOnContractClass(root, semanticModel, methodSyntax, parameterSyntax);

            var parametersBefore = methodSyntax.ParameterList.Parameters.TakeWhile(p => p != parameterSyntax).Select(p => p.Identifier.Text).ToArray();

            var statements = methodSyntax.Body.Statements;

            var index = statements
                .OfType<ExpressionStatementSyntax>()
                .Select(s => s.Expression)
                .OfType<InvocationExpressionSyntax>()
                .TakeWhile(s => s.Expression.IsContractExpression(ContractCategory.Requires))
                .Select(e => e.GetNotNullArgumentIdentifierSyntax<IdentifierNameSyntax>())
                .Where(s => s != null)
                .Select(s => semanticModel.GetSymbolInfo(s).Symbol)
                .Select(s => s?.Name)
                .TakeWhile(n => parametersBefore.Contains(n))
                .Count();

            var statementSyntax = SyntaxFactory.ParseStatement($"Contract.Requires({parameterSyntax.Identifier.Text} != null);\r\n")
                .WithLeadingTrivia(statements.FirstOrDefault()?.GetLeadingTrivia());

            statements = statements.Insert(index, statementSyntax);

            return root.ReplaceNode(methodSyntax.Body, methodSyntax.Body.WithStatements(statements));
        }

        private static CompilationUnitSyntax AddEnsures(CompilationUnitSyntax root, SemanticModel semanticModel, [NotNull] MethodDeclarationSyntax methodSyntax)
        {
            var body = methodSyntax.Body;
            if (body == null)
                return AddEnsuresOnContractClass(root, semanticModel, methodSyntax);

            var statements = body.Statements;

            var index = statements
                .Select(s => (s as ExpressionStatementSyntax)?.Expression as InvocationExpressionSyntax)
                .TakeWhile(s => (s?.Expression as MemberAccessExpressionSyntax).IsContractExpression(ContractCategory.Requires))
                .Count();

            var statementSyntax = SyntaxFactory.ParseStatement($"Contract.Ensures(Contract.Result<{methodSyntax.ReturnType}>() != null);\r\n")
                .WithLeadingTrivia(statements.FirstOrDefault()?.GetLeadingTrivia());

            statements = statements.Insert(index, statementSyntax);

            return root.ReplaceNode(body, body.WithStatements(statements));
        }

        private static CompilationUnitSyntax AddEnsuresOnContractClass([NotNull] CompilationUnitSyntax root, [NotNull] SemanticModel semanticModel, MethodDeclarationSyntax methodSyntax)
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);

            var contractClass = methodSymbol?.GetContractClass();

            methodSyntax = root.GetSyntaxNode<MethodDeclarationSyntax>(contractClass?.FindImplementingMemberOnDerivedClass(methodSymbol));

            if (methodSyntax?.Body != null)
            {
                return AddEnsures(root, semanticModel, methodSyntax);
            }

            return root;
        }

        private static CompilationUnitSyntax AddRequiresOnContractClass(CompilationUnitSyntax root, SemanticModel semanticModel, BaseMethodDeclarationSyntax methodSyntax, ParameterSyntax parameterSyntax)
        {
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);

            var contractClass = methodSymbol?.GetContractClass();

            var parameterIndex = methodSyntax.ParameterList.Parameters.IndexOf(parameterSyntax);

            methodSyntax = root.GetSyntaxNode<MethodDeclarationSyntax>(contractClass?.FindImplementingMemberOnDerivedClass(methodSymbol));

            if (methodSyntax?.Body != null)
            {
                return AddRequires(root, semanticModel, methodSyntax.ParameterList.Parameters[parameterIndex]);
            }

            return root;
        }

        private static CompilationUnitSyntax AddInvariant(CompilationUnitSyntax root, SemanticModel semanticModel, [NotNull] FieldDeclarationSyntax fieldSyntax)
        {
            var classDeclaration = fieldSyntax.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            var invariantMethod = classDeclaration?.ChildNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.AttributeLists.ContainsAttribute("ContractInvariantMethod"));

            var statements = fieldSyntax.Declaration.Variables
                .Select(variable => SyntaxFactory.ParseStatement($"            Contract.Invariant({variable.Identifier.Text} != null);\r\n"))
                .ToArray();

            return root.ReplaceNode(invariantMethod, invariantMethod.AddBodyStatements(statements));
        }
    }
}