using System.Collections.Concurrent;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Il passo per gioco della ricerca fra giochi, simulato (#4168, 5e-c-2). Da quando quel passo è una
/// <see cref="SearchQuery"/> mandata al mediator (lo stesso recupero della risposta per gioco), i test
/// del servizio simulano il mediator: rispondono per gioco e registrano ogni query ricevuta, così da
/// poter dire che cosa il servizio ha chiesto (soglia, pesi, modo, vettore, filtro per documento).
/// </summary>
internal sealed class PerGameSearchStub
{
    private readonly ConcurrentDictionary<Guid, Func<SearchQuery, IReadOnlyList<SearchResultDto>>> _answers = new();
    private IReadOnlyList<SearchResultDto> _anyGame = Array.Empty<SearchResultDto>();

    /// <summary>I parametri del recupero per gioco del seed del profilo.</summary>
    public static readonly PerGameRetrievalParameters Seed = new(VectorMinScore: 0.55, VectorWeight: 0.7f, KeywordWeight: 0.3f);

    public PerGameSearchStub()
    {
        Mediator
            .Setup(m => m.Send(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .Returns((SearchQuery query, CancellationToken _) =>
            {
                Received.Enqueue(query);
                var answer = _answers.TryGetValue(query.GameId, out var a) ? a(query) : _anyGame;
                return Task.FromResult(answer.ToList());
            });
    }

    public Mock<IMediator> Mediator { get; } = new();

    /// <summary>Le query ricevute, nell'ordine di arrivo.</summary>
    public ConcurrentQueue<SearchQuery> Received { get; } = new();

    public void Returns(Guid gameId, params SearchResultDto[] results) => _answers[gameId] = _ => results;

    /// <summary>La risposta di ogni gioco che non ne ha una sua.</summary>
    public void ReturnsForAnyGame(params SearchResultDto[] results) => _anyGame = results;

    public void Throws(Guid gameId, Exception exception) => _answers[gameId] = _ => throw exception;

    public IReadOnlyList<SearchQuery> For(Guid gameId) => Received.Where(q => q.GameId == gameId).ToList();

    /// <summary>Ogni scope dà il mediator simulato, come il servizio vero risolve dal suo scope per gioco.</summary>
    public IServiceScopeFactory ScopeFactory()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IMediator))).Returns(Mediator.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    /// <summary>Un passaggio come lo restituisce il recupero per gioco, con i segnali della fusione.</summary>
    public static SearchResultDto Passage(
        Guid? pdfDocumentId = null,
        int chunkIndex = 0,
        float hybridScore = 0.5f,
        float? vectorScore = null,
        float? keywordScore = null,
        string? language = "en",
        string content = "chunk text",
        int page = 1,
        IReadOnlyList<string>? matchedTerms = null)
    {
        var pdf = (pdfDocumentId ?? Guid.NewGuid()).ToString();
        return new SearchResultDto(
            VectorDocumentId: pdf,
            TextContent: content,
            PageNumber: page,
            RelevanceScore: vectorScore ?? hybridScore,
            Rank: chunkIndex + 1,
            SearchMethod: "hybrid",
            PdfDocumentId: pdf,
            ChunkIndex: chunkIndex,
            HybridScore: hybridScore,
            VectorScore: vectorScore,
            KeywordScore: keywordScore,
            Language: language,
            MatchedTerms: matchedTerms);
    }
}
