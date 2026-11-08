using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Query to perform vector/hybrid search.
/// Issue #2051: Supports document filtering via DocumentIds.
/// Issue #563: Optional <see cref="QueryVector"/> lets callers pass a pre-computed
/// embedding to skip the duplicate embedding call inside <c>SearchQueryHandler</c>.
/// When null, the handler computes the embedding itself (legacy/default path).
/// Phase D (D6): Optional <see cref="QueryRoleHint"/> lets the retrieval layer bias the
/// re-ranker toward chunks whose <c>role_tags</c> overlap with the user's classified intent.
/// Default <see cref="GameBookRole.None"/> = no-op (back-compat).
/// Issue #4294: <see cref="Access"/> is required — the sender says who is asking, or that it has
/// already passed the RAG gate.
/// </summary>
internal record SearchQuery(
    Guid GameId,
    string Query,
    SearchAccess Access,
    int TopK = 5,
    double MinScore = 0.55, // Adjusted for mxbai-embed-large
    string SearchMode = "hybrid", // "vector", "keyword", "hybrid"
    string Language = "en",
    IReadOnlyList<Guid>? PdfDocumentIds = null, // #2051, #4243: restrict to these PDFs (pdf_documents.Id, never vector_documents.Id); null or empty = all
                                                // Issue #563: Pre-computed query embedding to avoid duplicate generation.
                                                // Caller is responsible for ensuring the vector matches Query + Language and was produced
                                                // by the same embedding model the search handler would use. Null = handler computes its own.
    IReadOnlyList<float>? QueryVector = null,
    // Phase D — RAG role-aware (D6): user intent classification routed to retrieval re-ranker.
    // Chunks whose role_tags overlap with this hint receive a fixed score boost during RRF fusion.
    // None = legacy/no-op behavior.
    GameBookRole QueryRoleHint = GameBookRole.None,
    // #4168 (5e-c-1): i pesi della fusione ibrida, dal profilo dell'agente. I default sono quelli del
    // seed, che la fusione aveva scritti nel codice.
    float VectorWeight = RrfFusionDomainService.DefaultVectorWeight,
    float KeywordWeight = RrfFusionDomainService.DefaultKeywordWeight
) : IQuery<List<SearchResultDto>>;
