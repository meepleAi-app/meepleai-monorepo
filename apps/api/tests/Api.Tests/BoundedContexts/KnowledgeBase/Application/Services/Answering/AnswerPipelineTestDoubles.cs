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
        Enum.GetValues<DocumentCategory>());

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

    public static AnswerRetrieval Retrieval(
        SearchQueryHandler searchQueryHandler,
        ICrossEncoderReranker reranker,
        IIntentClassifierService? intentClassifier = null) =>
        new(searchQueryHandler, reranker, intentClassifier ?? new IntentClassifierService(), NullLogger<AnswerRetrieval>.Instance);
}
