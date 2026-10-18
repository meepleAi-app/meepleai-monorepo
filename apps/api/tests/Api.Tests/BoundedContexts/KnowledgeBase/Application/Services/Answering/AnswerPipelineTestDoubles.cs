using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.SharedKernel.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168: i doppi dei passi della pipeline per i test degli handler che li usano. Il profilo è
/// quello del seed, cioè i valori che gli handler avevano compilati prima della pipeline, così che
/// i test esistenti continuino a descrivere lo stesso comportamento.
/// </summary>
internal static class AnswerPipelineTestDoubles
{
    public static readonly Guid SeedVersionId = Guid.Parse("a9e7f5c1-4167-4095-9a1e-000000000102");

    public static AgentProfileContent SeedContent(string? primaryModelId = null) => AgentProfileContent.Create(
        "MeepleAI",
        "Specialized agent for board game rules interpretation and clarification.",
        "a precise board game rules assistant",
        primaryModelId ?? "deepseek-flash",
        "meta-llama/llama-3.3-70b-instruct",
        temperature: 0.3m,
        maxResponseTokens: 1500,
        CitationStyle.PageReferencesInText,
        new Dictionary<string, string> { ["en"] = "You are MeepleAI, a precise board game rules assistant." },
        fallbackLanguage: "en",
        candidatePoolSize: 20,
        finalTopK: 5,
        minScore: 0.55m,
        vectorWeight: 0.7m,
        keywordWeight: 0.3m,
        rerankerEnabled: true,
        Enum.GetValues<DocumentCategory>(),
            Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.SessionAssistantSettings.Disabled);

    public static IPublishedAgentProfileReader ProfileReader(AgentProfileContent? content = null)
    {
        var reader = new Mock<IPublishedAgentProfileReader>();
        reader.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishedAgentProfile(SeedVersionId, 2, content ?? SeedContent()));
        return reader.Object;
    }

    /// <summary>
    /// Un rilevatore che non è mai sicuro: la lingua della risposta è quella che la query chiede, o
    /// l'inglese. È il comportamento che i test esistenti si aspettano da <c>Language</c>.
    /// </summary>
    public static ILanguageDetectionService UncertainLanguageDetection()
    {
        var detector = new Mock<ILanguageDetectionService>();
        detector.Setup(d => d.Detect(It.IsAny<string>())).Returns(new LanguageDetection("en", IsConfident: false));
        detector.Setup(d => d.IsSupportedLanguage(It.IsAny<string?>()))
            .Returns<string?>(l => l is "en" or "it" or "de" or "fr" or "es");
        return detector.Object;
    }

    /// <summary>
    /// Il passo delle precondizioni. Senza un piano, quello senza limite: nessuna domanda fermata e
    /// nessuna contata, che è ciò che si aspettano i test che non parlano di quota.
    /// </summary>
    public static AnswerPreconditions Preconditions(
        IRagAccessService ragAccess, Api.SharedKernel.Services.ITierEnforcementService? tiers = null) =>
        new(ragAccess, tiers ?? PlanWith(used: 0, max: int.MaxValue).Object, NullLogger<AnswerPreconditions>.Instance);

    /// <summary>
    /// Ciò che il servizio dei piani direbbe a un certo punto della giornata: quante domande hanno
    /// già avuto risposta, quante ne prevede il piano, e se il contatore si è potuto leggere.
    /// </summary>
    public static Mock<Api.SharedKernel.Services.ITierEnforcementService> PlanWith(
        int used, int max, bool counterReadable = true)
    {
        var tiers = new Mock<Api.SharedKernel.Services.ITierEnforcementService>();
        tiers.Setup(t => t.CheckUsageAsync(
                It.IsAny<Guid>(), Api.SharedKernel.Services.TierAction.AgentQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Api.SharedKernel.Services.TierUsageCheck(used, max, counterReadable));
        return tiers;
    }

    public static AnswerRetrieval Retrieval(
        SearchQueryHandler searchQueryHandler,
        ICrossEncoderReranker reranker,
        IIntentClassifierService? intentClassifier = null,
        IMultiGameHybridSearchService? multiGameSearch = null) =>
        new(searchQueryHandler, multiGameSearch ?? Mock.Of<IMultiGameHybridSearchService>(), reranker,
            intentClassifier ?? new IntentClassifierService(), NullLogger<AnswerRetrieval>.Instance);

    /// <summary>#4410: il passo di recupero e citazioni della pipeline, con il recupero e il resolver dati.</summary>
    public static TieredPassageSearch PassageSearch(
        AnswerRetrieval retrieval,
        Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IVectorDocumentRepository vectorDocuments,
        ICopyrightTierResolver tiers) =>
        new(retrieval, vectorDocuments, tiers);

    /// <summary>Il recupero per la ricerca fra giochi: la ricerca per gioco non serve, il reranker non riordina.</summary>
    public static AnswerRetrieval RetrievalAcrossGames(IMultiGameHybridSearchService multiGameSearch, ICrossEncoderReranker? reranker = null)
    {
        var searchQueryHandler = new SearchQueryHandler(
            Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IEmbeddingRepository>(),
            new Mock<VectorSearchDomainService>().Object,
            new Mock<RrfFusionDomainService>().Object,
            Mock.Of<IEmbeddingService>(),
            Mock.Of<IKeywordSearchService>(),
            Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.IRagAccessService>(),
            NullLogger<SearchQueryHandler>.Instance);
        return Retrieval(searchQueryHandler, reranker ?? PassThroughReranker(), multiGameSearch: multiGameSearch);
    }

    /// <summary>Un reranker che lascia l'ordine della fusione e taglia ai primi K.</summary>
    public static ICrossEncoderReranker PassThroughReranker()
    {
        var reranker = new Mock<ICrossEncoderReranker>();
        reranker
            .Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<RerankChunk> chunks, int? topK, CancellationToken _) =>
                new RerankResult(
                    chunks.Take(topK ?? chunks.Count)
                        .Select(c => new RerankedChunk(c.Id, c.Content, c.OriginalScore, c.OriginalScore))
                        .ToList(),
                    "pass-through", 0));
        return reranker.Object;
    }
}
