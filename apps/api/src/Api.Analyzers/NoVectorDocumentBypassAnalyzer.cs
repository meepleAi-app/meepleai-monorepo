using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Api.Analyzers;

/// <summary>
/// Flags two anti-patterns that undermine the #2244 / P234 structural refactor:
///
///   - <b>MAI004</b>: <c>IPdfIndexingPipeline? pipeline = null</c> constructor parameter.
///     The pipeline is mandatory; making it nullable invites a fallback branch that
///     bypasses the domain (the BLOCKER caught in #2244 Task 4 spec review).
///
///   - <b>MAI005</b>: <c>new VectorDocumentEntity { ... }</c> object initializer outside
///     <c>KnowledgeBaseMappers</c>. The only legitimate construction of the EF persistence
///     entity is from the domain aggregate via the mapper; any direct construction in an
///     Application/Commands or Application/Services file bypasses the
///     <c>VectorDocument</c> aggregate root and silently drops the
///     <c>VectorDocumentIndexedEvent</c> the constructor raises.
///
/// Memory pattern: <c>P234 domain-event-bypass-via-ef-entity</c>.
/// ADR-063 (scoped injection in background tasks) covers the mandatory-injection rule.
///
/// Both rules are deliberately conservative — they target a narrow, well-known
/// pair of types and produce a single Warning per occurrence. False-positive risk is
/// low because the type names are unique to this codebase.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoVectorDocumentBypassAnalyzer : DiagnosticAnalyzer
{
    public const string NullablePipelineDiagnosticId = "MAI004";
    public const string EntityBypassDiagnosticId = "MAI005";

    private const string Category = "Architecture";
    private const string HelpLinkUri =
        "https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/docs/for-claude/architecture/adr/adr-063-scoped-injection-in-background-tasks.md";

    /// <summary>
    /// Fully-qualified name of the pipeline interface. MAI004 fires when this type
    /// appears as a nullable ctor parameter with a <c>null</c> default value.
    /// </summary>
    private const string PipelineInterfaceFqn =
        "Api.BoundedContexts.KnowledgeBase.Application.Services.IPdfIndexingPipeline";

    /// <summary>
    /// Fully-qualified name of the EF persistence entity. MAI005 fires when this type
    /// is constructed via <c>new</c> outside the legitimate mapper.
    /// </summary>
    private const string VectorDocumentEntityFqn =
        "Api.Infrastructure.Entities.VectorDocumentEntity";

    /// <summary>
    /// Fully-qualified names of the legitimate construction sites. Object creations inside
    /// these types are allowed:
    /// <list type="bullet">
    ///   <item><c>KnowledgeBaseMappers</c> — domain → EF mapping for round-trip (legitimate by
    ///     pattern definition).</item>
    ///   <item><c>PdfIndexingPipeline</c> — the single-owner ingestion pipeline introduced by
    ///     #2244 / PR #2278. Builds the EF entity directly for fine-grained control of
    ///     fields not on the domain (EmbeddingModel, IndexingStatus="processing" vs
    ///     "completed", etc.). A future refactor may migrate this site to call
    ///     <c>domain.ToEntity()</c> at which point this allowlist entry can be removed.</item>
    /// </list>
    /// </summary>
    private static readonly ImmutableHashSet<string> AllowedConstructionSites =
        ImmutableHashSet.CreateRange(System.StringComparer.Ordinal, new[]
        {
            "Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers.KnowledgeBaseMappers",
            "Api.BoundedContexts.KnowledgeBase.Application.Services.PdfIndexingPipeline",
        });

    private static readonly DiagnosticDescriptor Mai004Rule = new(
        NullablePipelineDiagnosticId,
        title: "IPdfIndexingPipeline must be a mandatory, non-nullable ctor parameter",
        messageFormat:
            "IPdfIndexingPipeline parameter '{0}' is nullable with a null default; " +
            "this enables a fallback branch that bypasses the VectorDocument domain " +
            "and drops VectorDocumentIndexedEvent (#2244 Task 4 BLOCKER pattern). " +
            "Make the parameter required and update test fixtures to pass a mock. " +
            "See ADR-063.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The structural fix in #2244 closed a domain-event-bypass anti-pattern (P234). " +
            "A nullable IPdfIndexingPipeline parameter with a null default value invites a " +
            "later refactor to add an `else { new VectorDocumentEntity {...} }` fallback, " +
            "re-introducing the bypass. Forbid the pattern at the analyzer level.",
        helpLinkUri: HelpLinkUri);

    private static readonly DiagnosticDescriptor Mai005Rule = new(
        EntityBypassDiagnosticId,
        title: "Construct VectorDocument via the domain aggregate, not new VectorDocumentEntity directly",
        messageFormat:
            "`new VectorDocumentEntity {{ ... }}` outside KnowledgeBaseMappers bypasses the " +
            "VectorDocument domain aggregate. Use VectorDocument.Create(...) + " +
            "IVectorDocumentRepository.AddAsync, or — for the EF mapper round-trip — call " +
            "domain.ToEntity(). Memory pattern: P234.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "VectorDocument is an aggregate root whose constructor raises " +
            "VectorDocumentIndexedEvent. Writing the EF persistence entity directly via " +
            "`new VectorDocumentEntity { ... }` skips the domain ctor, silently drops the " +
            "event, and breaks downstream handlers that flip shared_games.has_knowledge_base " +
            "(the original #2244 / epic #2242 symptom). The only legitimate construction site " +
            "is KnowledgeBaseMappers.ToEntity, which maps an existing domain aggregate.",
        helpLinkUri: HelpLinkUri);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Mai004Rule, Mai005Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        var parameter = (ParameterSyntax)context.Node;

        // MAI004 only fires on constructor parameters — not records / method params,
        // not lambdas. We could broaden later but mandatory injection only matters
        // where DI does the wiring (constructors).
        if (parameter.Parent is not ParameterListSyntax parameterList ||
            parameterList.Parent is not ConstructorDeclarationSyntax)
        {
            return;
        }

        // Must have an explicit null default. `IPdfIndexingPipeline? pipeline` without
        // `= null` is rare in practice; the BLOCKER pattern is specifically the
        // null-default that paves the way for the else-branch fallback.
        if (parameter.Default is null ||
            parameter.Default.Value is not LiteralExpressionSyntax literal ||
            !literal.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return;
        }

        // Must be IPdfIndexingPipeline by FQN. Use the semantic model so we resolve
        // type aliases and using directives.
        var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type!, context.CancellationToken);
        var symbol = typeInfo.Type;
        if (symbol is null)
        {
            return;
        }

        // The annotated type may be a nullable form (T? for reference types under
        // nullable context). Compare the underlying type FQN.
        var underlyingFqn = symbol.OriginalDefinition.ToDisplayString();

        if (underlyingFqn != PipelineInterfaceFqn)
        {
            return;
        }

        // Only flag if the type is actually annotated nullable (T?). A plain `T = null`
        // doesn't compile under nullable context, but in legacy code with nullable
        // disabled we still want to flag the null-default pattern.
        var isNullableReferenceType =
            parameter.Type is NullableTypeSyntax ||
            symbol.NullableAnnotation == NullableAnnotation.Annotated;

        if (!isNullableReferenceType)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Mai004Rule,
            parameter.GetLocation(),
            parameter.Identifier.Text));
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        // Resolve the type being constructed via the constructor symbol. Using
        // GetSymbolInfo on the ObjectCreation node returns the constructor as IMethodSymbol;
        // its ContainingType is the type we care about. This is more reliable than
        // GetTypeInfo(creation.Type) which can return null for IdentifierNameSyntax cases.
        var ctorSymbol = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol as IMethodSymbol;
        var symbol = ctorSymbol?.ContainingType ??
                     context.SemanticModel.GetTypeInfo(creation.Type, context.CancellationToken).Type;
        if (symbol is null)
        {
            return;
        }

        var fqn = symbol.OriginalDefinition.ToDisplayString();

        if (fqn != VectorDocumentEntityFqn)
        {
            return;
        }

        // Walk ancestors to find the enclosing TypeDeclarationSyntax. If we're inside
        // KnowledgeBaseMappers, the construction is legitimate (mapper round-trip).
        var enclosingType = creation.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        if (enclosingType is null)
        {
            // Top-level statement or something unusual — bypass.
            // Treat as outside-the-mapper and report.
        }
        else
        {
            var enclosingSymbol = context.SemanticModel.GetDeclaredSymbol(enclosingType, context.CancellationToken);
            if (enclosingSymbol is not null)
            {
                var enclosingFqn = enclosingSymbol.OriginalDefinition.ToDisplayString();
                if (AllowedConstructionSites.Contains(enclosingFqn))
                {
                    return; // legitimate construction site
                }
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Mai005Rule,
            creation.GetLocation()));
    }
}
