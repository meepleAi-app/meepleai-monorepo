using System;
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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

namespace Api.Analyzers;

/// <summary>
/// Provides a single in-IDE quick-fix (lightbulb) for MAI001 / MAI002 / MAI003:
/// wraps the offending log argument with the appropriate
/// <c>Api.Infrastructure.Security.DataMasking.Mask*</c> call.
///
/// Mapping (best-effort from diagnostic message; defaults to <c>MaskString</c>):
///   - email / emailAddress       → MaskEmail
///   - ipaddress / remoteip / clientip → MaskIpAddress
///   - jwt / jwttoken / bearertoken / token → MaskJwt (best-effort; tokens may be
///     opaque, in which case the developer can change to MaskString manually)
///   - password / phone / ssn / fallback → MaskString
///
/// For MAI002 (typed VO), appends <c>.Value</c> to the argument because the
/// canonical mask methods all accept <c>string?</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NoPiiInLogCodeFixProvider))]
[Shared]
public sealed class NoPiiInLogCodeFixProvider : CodeFixProvider
{
    private const string DataMaskingNamespace = "Api.Infrastructure.Security";
    private const string DataMaskingTypeName = "DataMasking";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
        NoPiiInLogAnalyzer.DiagnosticId,
        NoPiiInLogAnalyzer.TypedVoDiagnosticId,
        NoPiiInLogAnalyzer.HeuristicNameDiagnosticId);

    // BatchFixer enables "Fix all in document/project/solution" for the same diagnostic.
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var argument = FindArgumentExpression(root, diagnostic.Location.SourceSpan);
        if (argument is null)
        {
            return;
        }

        var maskMethod = DetermineMaskMethod(diagnostic);
        var needsValueAccessor = string.Equals(
            diagnostic.Id,
            NoPiiInLogAnalyzer.TypedVoDiagnosticId,
            StringComparison.Ordinal);

        var title = $"Wrap with DataMasking.{maskMethod}";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                ct => ApplyWrapAsync(context.Document, root, argument, maskMethod, needsValueAccessor, ct),
                equivalenceKey: $"NoPiiInLog/Wrap/{maskMethod}/{needsValueAccessor}"),
            diagnostic);
    }

    private static ExpressionSyntax? FindArgumentExpression(SyntaxNode root, TextSpan span)
    {
        var node = root.FindNode(span, getInnermostNodeForTie: true);

        // The diagnostic is anchored on the argument expression itself. Walk up to find
        // the nearest ExpressionSyntax (the argument), skipping IdentifierName / member
        // access wrappers.
        var current = node;
        while (current is not null)
        {
            if (current is ExpressionSyntax expression
                && current.Parent is not ArgumentSyntax)
            {
                // If we've already reached the top-level expression for the arg, stop.
                if (current.Parent is ExpressionSyntax || current.Parent is ArgumentListSyntax)
                {
                    return expression;
                }
            }

            if (current is ArgumentSyntax argument)
            {
                return argument.Expression;
            }

            current = current.Parent;
        }

        return node as ExpressionSyntax;
    }

    private static string DetermineMaskMethod(Diagnostic diagnostic)
    {
        var lowered = diagnostic.GetMessage().ToLowerInvariant();

        // Order matters: more specific patterns first.
        if (Contains(lowered, "ipaddress") || Contains(lowered, "remoteip") || Contains(lowered, "clientip"))
        {
            return "MaskIpAddress";
        }

        if (Contains(lowered, "email"))
        {
            return "MaskEmail";
        }

        if (Contains(lowered, "jwt") || Contains(lowered, "token") || Contains(lowered, "bearer"))
        {
            return "MaskJwt";
        }

        // Password, Phone, SSN, etc. — generic fallback. Developer can swap to a
        // specialized method (e.g. MaskJwt for JWT vs MaskString for opaque) if needed.
        return "MaskString";
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;

    private static Task<Document> ApplyWrapAsync(
        Document document,
        SyntaxNode root,
        ExpressionSyntax originalArgument,
        string maskMethod,
        bool needsValueAccessor,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken; // syntax transforms here are synchronous; the Task<>
                               // signature is mandated by CodeAction.Create.
        ExpressionSyntax wrappedArgument = originalArgument.WithoutTrivia();

        if (needsValueAccessor)
        {
            wrappedArgument = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                wrappedArgument,
                SyntaxFactory.IdentifierName("Value"));
        }

        var maskInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(DataMaskingTypeName),
                SyntaxFactory.IdentifierName(maskMethod)),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(wrappedArgument))))
            .WithLeadingTrivia(originalArgument.GetLeadingTrivia())
            .WithTrailingTrivia(originalArgument.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);

        var newRoot = root.ReplaceNode(originalArgument, maskInvocation);
        newRoot = EnsureUsingDirective(newRoot);

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static SyntaxNode EnsureUsingDirective(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        var hasUsing = compilationUnit.Usings.Any(u =>
            string.Equals(u.Name?.ToString(), DataMaskingNamespace, StringComparison.Ordinal));

        if (hasUsing)
        {
            return compilationUnit;
        }

        var newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(DataMaskingNamespace))
            .WithAdditionalAnnotations(Formatter.Annotation);

        return compilationUnit.AddUsings(newUsing);
    }
}
