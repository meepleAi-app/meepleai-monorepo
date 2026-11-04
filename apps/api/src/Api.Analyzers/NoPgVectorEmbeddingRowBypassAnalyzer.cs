using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Api.Analyzers;

/// <summary>
/// <b>MAI007</b>: una riga di <c>pgvector_embeddings</c> si costruisce solo con
/// <c>PgVectorEmbeddingEntity.ForChunk</c> (#4384).
/// </summary>
/// <remarks>
/// <para>
/// La regola «collega la riga al suo <c>text_chunks</c>» era scritta in ogni scrittore, e tre su quattro
/// la dimenticavano: <c>source_chunk_id</c> restava nullo, e il braccio vettoriale perdeva in silenzio il
/// boost di heading e la localizzazione della citazione. <c>ForChunk</c> chiede il collegamento come
/// argomento; questa regola impedisce di aggirarlo costruendo l'entità a mano.
/// </para>
/// <para>
/// Lavora sulle operazioni, non sul testo: riconosce <c>new PgVectorEmbeddingEntity { … }</c>, il nome
/// completo e il <c>new()</c> tipizzato dal contesto. L'unico posto ammesso è l'entità stessa, cioè
/// <c>ForChunk</c>. I test possono costruirla: l'analizzatore è collegato solo al progetto Api.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoPgVectorEmbeddingRowBypassAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MAI007";

    /// <summary>Il nome completo dell'entità EF di <c>pgvector_embeddings</c>.</summary>
    private const string EntityFqn = "Api.Infrastructure.Entities.KnowledgeBase.PgVectorEmbeddingEntity";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Build pgvector_embeddings rows with PgVectorEmbeddingEntity.ForChunk",
        messageFormat:
            "Constructing PgVectorEmbeddingEntity directly can leave source_chunk_id null. " +
            "Use PgVectorEmbeddingEntity.ForChunk(...), which asks which text_chunks row the embedding belongs to (#4384).",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "A pgvector_embeddings row without source_chunk_id silently loses the heading boost and the " +
            "citation location, which the vector arm reads through a LEFT JOIN on text_chunks. ForChunk takes " +
            "the link as a required argument; constructing the entity elsewhere bypasses it.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Type is not { } type || type.OriginalDefinition.ToDisplayString() != EntityFqn)
        {
            return;
        }

        // L'entità stessa (ForChunk) è l'unico posto ammesso.
        for (var symbol = context.ContainingSymbol; symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (symbol is INamedTypeSymbol named && named.OriginalDefinition.ToDisplayString() == EntityFqn)
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation()));
    }
}
