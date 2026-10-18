using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ProbeRetrieval;

/// <summary>
/// #4482: fa il recupero della chat per un gioco e lo restituisce intero, senza chiamare il modello.
/// </summary>
/// <remarks>
/// <para>
/// I passi sono quelli di <see cref="AnswerPipeline"/> fino al recupero: la versione pubblicata del
/// profilo, letta dallo stesso lettore; la lingua da <see cref="AnswerLanguageResolver"/> con la domanda e
/// l'<c>Accept-Language</c>; il recupero di <see cref="AnswerRetrieval"/> con i numeri del profilo, senza
/// filtro per documento e senza un vettore calcolato prima. La chat filtra per documento solo quando il
/// client sceglie i documenti, e una misura per gioco li vuole tutti.
/// </para>
/// <para>
/// Ciò che la chat fa e la sonda no: la cache delle risposte (che salterebbe il recupero), il controllo che
/// i documenti siano pronti, la quota, le regole verificate, il modello. Nessuno di questi cambia i chunk
/// recuperati. Il recupero conta nelle metriche come quello della chat (esiti dei bracci, ripieghi del
/// reranker): sono chiamate vere ai servizi veri.
/// </para>
/// </remarks>
internal sealed class ProbeRetrievalQueryHandler : IQueryHandler<ProbeRetrievalQuery, RetrievalProbeDto>
{
    /// <summary>
    /// L'<c>Accept-Language</c> quando la richiesta non ne porta uno: le domande di riferimento sono in
    /// italiano, e un utente italiano lo manda.
    /// </summary>
    internal const string DefaultAcceptLanguage = "it";

    private readonly IPublishedAgentProfileReader _profileReader;
    private readonly ILanguageDetectionService _languageDetection;
    private readonly AnswerRetrieval _retrieval;
    private readonly ILogger<ProbeRetrievalQueryHandler> _logger;

    public ProbeRetrievalQueryHandler(
        IPublishedAgentProfileReader profileReader,
        ILanguageDetectionService languageDetection,
        AnswerRetrieval retrieval,
        ILogger<ProbeRetrievalQueryHandler> logger)
    {
        _profileReader = profileReader ?? throw new ArgumentNullException(nameof(profileReader));
        _languageDetection = languageDetection ?? throw new ArgumentNullException(nameof(languageDetection));
        _retrieval = retrieval ?? throw new ArgumentNullException(nameof(retrieval));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RetrievalProbeDto> Handle(ProbeRetrievalQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await ReadProfileAsync(cancellationToken).ConfigureAwait(false);

        var acceptLanguage = string.IsNullOrWhiteSpace(request.AcceptLanguage)
            ? DefaultAcceptLanguage
            : request.AcceptLanguage.Trim();
        var language = AnswerLanguageResolver.Resolve(_languageDetection, request.Question, acceptLanguage);

        var retrievalRequest = new AnswerRetrievalRequest(request.GameId, request.Question);
        var retrieved = await _retrieval
            .RetrieveWithPoolAsync(retrievalRequest, profile.Content, language.Language, cancellationToken)
            .ConfigureAwait(false);

        var content = profile.Content;
        return new RetrievalProbeDto(
            GameId: request.GameId,
            Question: request.Question,
            AcceptLanguage: acceptLanguage,
            Language: language.Language,
            LanguageSource: language.Source.ToString(),
            ProfileVersionId: profile.VersionId,
            ProfileVersionNumber: profile.VersionNumber,
            Parameters: new RetrievalProbeParametersDto(
                content.CandidatePoolSize,
                content.FinalTopK,
                content.MinScore,
                content.VectorWeight,
                content.KeywordWeight,
                content.RerankerEnabled,
                retrievalRequest.SearchMode,
                retrieved.QueryRoleHint.ToString()),
            Reranking: retrieved.Reranking.ToString(),
            FusedCount: retrieved.FusedCount,
            Pool: retrieved.Pool.Select((r, i) => Chunk(r, i + 1, poolPosition: null, rerankScore: null)).ToList(),
            Final: retrieved.Final
                .Select((pick, i) => Chunk(retrieved.Pool[pick.Index], i + 1, pick.Index + 1, pick.Score))
                .ToList());
    }

    private static RetrievalProbeChunkDto Chunk(SearchResultDto r, int position, int? poolPosition, double? rerankScore) => new(
        Position: position,
        PoolPosition: poolPosition,
        TextChunkId: r.TextChunkId,
        VectorDocumentId: r.VectorDocumentId,
        PdfDocumentId: r.PdfDocumentId,
        ChunkIndex: r.ChunkIndex,
        PageNumber: r.PageNumber,
        Language: r.Language,
        RelevanceScore: r.RelevanceScore,
        HybridScore: r.HybridScore,
        VectorScore: r.VectorScore,
        KeywordScore: r.KeywordScore,
        RerankScore: rerankScore,
        SearchMethod: r.SearchMethod,
        MatchedTerms: r.MatchedTerms,
        Text: r.TextContent);

    /// <summary>
    /// La versione pubblicata. Senza, la sonda si rifiuta con un 503 che dice il motivo, come la ricerca
    /// nella base di conoscenza: niente valori compilati (ADR-095), e niente 500.
    /// </summary>
    private async Task<PublishedAgentProfile> ReadProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _profileReader.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (AgentProfileNotPublishedException ex)
        {
            _logger.LogError(ex, "No published agent profile: the retrieval probe is refused");
            throw new HttpException(
                StatusCodes.Status503ServiceUnavailable, AgentProfileNotPublishedException.Code, ex.Message, ex);
        }
    }
}
