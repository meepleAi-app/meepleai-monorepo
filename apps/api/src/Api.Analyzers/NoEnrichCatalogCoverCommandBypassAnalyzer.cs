using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Api.Analyzers;

/// <summary>
/// <b>MAI006</b>: flags <c>new EnrichCatalogCoverCommand(...)</c> constructed outside
/// <c>WikidataCoverEnrichmentRunner</c>.
///
/// After #3369 all four Wikidata cover-enrichment triggers (M9 scheduler, M12 admin,
/// batch, F2 bulk-retry) route through <c>WikidataCoverEnrichmentRunner.EnrichAndRecordAsync</c>,
/// the single source of truth that persists a <c>WikidataCoverEnrichmentAttempt</c> row,
/// applies the DEC-3j retry/dead-letter policy and broadcasts SSE. The runner is the ONLY
/// legitimate place to construct + dispatch the raw <c>EnrichCatalogCoverCommand</c>; any other
/// construction site re-introduces the batch-bypass class of bug (#3369) — the command runs but
/// records no attempt, gets no retry classification and emits no SSE.
///
/// This rule is deliberately conservative: it targets a single well-known command type and
/// produces one Warning per occurrence. False-positive risk is low because the type name is
/// unique to this codebase and the sole allowed site is explicitly allow-listed.
///
/// Tracker: umbrella #3373 (decision D2).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoEnrichCatalogCoverCommandBypassAnalyzer : DiagnosticAnalyzer
{
    public const string RunnerBypassDiagnosticId = "MAI006";

    private const string Category = "Architecture";
    private const string HelpLinkUri =
        "https://github.com/meepleAi-app/meepleai-monorepo/issues/3373";

    /// <summary>
    /// Fully-qualified name of the M8 command. MAI006 fires when this type is
    /// constructed via <c>new</c> outside the allow-listed runner.
    /// </summary>
    private const string EnrichCatalogCoverCommandFqn =
        "Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover.EnrichCatalogCoverCommand";

    /// <summary>
    /// Fully-qualified name of the single legitimate construction site — the SSOT runner.
    /// Object creations inside this type are allowed.
    /// </summary>
    private const string RunnerFqn =
        "Api.BoundedContexts.SharedGameCatalog.Application.Services.WikidataCoverEnrichmentRunner";

    private static readonly DiagnosticDescriptor Mai006Rule = new(
        RunnerBypassDiagnosticId,
        title: "Dispatch EnrichCatalogCoverCommand through WikidataCoverEnrichmentRunner, not directly",
        messageFormat:
            "`new EnrichCatalogCoverCommand(...)` outside WikidataCoverEnrichmentRunner bypasses the " +
            "enrich+record SSOT — the command runs but records no WikidataCoverEnrichmentAttempt, gets " +
            "no DEC-3j retry/dead-letter classification and emits no SSE (the #3369 batch-bypass bug). " +
            "Call IWikidataCoverEnrichmentRunner.EnrichAndRecordAsync instead.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "All four Wikidata cover-enrichment triggers route through WikidataCoverEnrichmentRunner " +
            "(#3369), which is the single source of truth for attempt-log + retry/dead-letter + SSE. " +
            "Constructing and dispatching the raw EnrichCatalogCoverCommand anywhere else silently drops " +
            "that machinery. The only legitimate construction site is the runner itself.",
        helpLinkUri: HelpLinkUri);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Mai006Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        // Resolve the constructed type via the constructor symbol (more reliable than
        // GetTypeInfo(creation.Type) for IdentifierNameSyntax cases).
        var ctorSymbol = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol as IMethodSymbol;
        var symbol = ctorSymbol?.ContainingType ??
                     context.SemanticModel.GetTypeInfo(creation.Type, context.CancellationToken).Type;
        if (symbol is null)
        {
            return;
        }

        if (symbol.OriginalDefinition.ToDisplayString() != EnrichCatalogCoverCommandFqn)
        {
            return;
        }

        // Walk ancestors to find the enclosing type. Construction inside the runner is legitimate.
        var enclosingType = creation.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (enclosingType is not null)
        {
            var enclosingSymbol = context.SemanticModel.GetDeclaredSymbol(enclosingType, context.CancellationToken);
            if (enclosingSymbol is not null &&
                enclosingSymbol.OriginalDefinition.ToDisplayString() == RunnerFqn)
            {
                return; // legitimate construction site (the SSOT runner)
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(Mai006Rule, creation.GetLocation()));
    }
}
