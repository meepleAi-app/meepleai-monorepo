using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Correzione dell'offset di lingua sulla cosine, dentro <c>FuseGlobally</c> (issue #3740).
/// </summary>
/// <remarks>
/// <para>
/// Il corpus è mixed-language (51.505 chunk <c>en</c> / 4.332 <c>it</c> / 530 <c>de</c>) e nello
/// spazio di <c>multilingual-e5</c> la lingua del testo è una componente dominante: per una query
/// italiana l'intera banda dei chunk italiani sta più in alto di quella inglese, uniformemente e
/// senza rapporto con la pertinenza. Misurato su `catan-setup-it`: i primi dieci vicini sono dieci
/// chunk italiani di altri giochi, e il miglior chunk di Catan — l'unico manuale che la query nomina,
/// e che esiste solo in inglese — sta al rango 132. Restringendo a <c>lang='en'</c> risale al rango 1.
/// </para>
/// <para>
/// Questi test girano attraverso <c>SearchAsync</c>, non su un helper privato: ciò che conta è
/// l'<b>ordine finale</b>, ed è l'unica cosa che il chiamante osserva.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class MultiGameHybridSearchServiceLanguageNormTests
{
    private readonly PerGameSearchStub _perGame = new();

    [Fact]
    public async Task MonolingualCandidates_OrderingIsUnchanged()
    {
        // La proprietà che rende questo cambio sicuro: con una sola lingua fra i candidati, la media
        // del gruppo È la media globale, quindi l'offset è esattamente 0 e la funzione è un no-op.
        // È il caso normale, e non deve muoversi di un capello.
        var games = NewGames(4);
        var perGame = new[]
        {
            Chunk(games[0], 0.70f, "en"),
            Chunk(games[1], 0.90f, "en"),
            Chunk(games[2], 0.80f, "en"),
            Chunk(games[3], 0.60f, "en"),
        };
        var sut = SetUpPerGameResults(games, perGame);

        var result = await sut.SearchAsync("how do I set up the board?", games, 10, PerGameSearchStub.Seed);

        result.Select(r => r.VectorScore).Should().BeEquivalentTo(
            new float?[] { 0.90f, 0.80f, 0.70f, 0.60f },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task MixedLanguage_ForeignBandNoLongerBuriesTheRelevantChunk()
    {
        // Sei chunk `it` tutti a 0.86 (giochi che la query non nomina) contro sei `en` fra 0.80 e
        // 0.82, dove il migliore è quello atteso. Sulla cosine grezza vincono tutti e sei gli `it`:
        // è esattamente la forma del difetto misurato. Togliendo l'offset di lingua, l'`en` migliore
        // passa davanti.
        var italianGames = NewGames(6);
        var englishGames = NewGames(6);
        var target = Chunk(englishGames[0], 0.82f, "en", content: "TARGET");

        var sut = SetUpPerGameResults(
            italianGames.Concat(englishGames).ToArray(),
            italianGames.Select(g => Chunk(g, 0.86f, "it"))
                .Concat(new[] { target })
                .Concat(englishGames.Skip(1).Select(g => Chunk(g, 0.80f, "en")))
                .ToArray());

        var result = await sut.SearchAsync("come si prepara il tabellone?", italianGames.Concat(englishGames).ToArray(), 12, PerGameSearchStub.Seed);

        result[0].Content.Should().Be("TARGET",
            "l'offset uniforme della banda italiana non deve più decidere il primo risultato");
        result[0].Language.Should().Be("en");
    }

    [Fact]
    public async Task MixedLanguage_RawCosineOrderWouldHaveBuriedIt()
    {
        // Contro-prova dello scenario precedente sulla stessa forma di dati: senza la correzione
        // l'ordine sarebbe quello della cosine grezza, e il target starebbe dietro tutti gli `it`.
        // Qui si asserisce che il target NON è dove la cosine grezza lo metterebbe.
        var italianGames = NewGames(6);
        var englishGames = NewGames(6);
        var target = Chunk(englishGames[0], 0.82f, "en", content: "TARGET");
        var all = italianGames.Concat(englishGames).ToArray();

        var sut = SetUpPerGameResults(
            all,
            italianGames.Select(g => Chunk(g, 0.86f, "it"))
                .Concat(new[] { target })
                .Concat(englishGames.Skip(1).Select(g => Chunk(g, 0.80f, "en")))
                .ToArray());

        var result = await sut.SearchAsync("come si prepara il tabellone?", all, 12, PerGameSearchStub.Seed);

        // Il target ha la cosine GREZZA più bassa (0.82) dei sei chunk italiani (0.86), quindi su
        // quel segnale sarebbe stato settimo. Che sia primo è la correzione, e la cosine riportata
        // resta quella grezza — l'aggiustamento vive solo dentro il punteggio.
        result[0].Content.Should().Be("TARGET");
        result[0].VectorScore.Should().Be(0.82f);
        result.Skip(1).Take(6).Should().OnlyContain(
            r => r.Language == "it" && r.VectorScore == 0.86f,
            "i sei italiani seguono, pur avendo una cosine grezza superiore");
    }

    [Fact]
    public async Task LanguageGroupBelowMinimumSize_IsNotShifted()
    {
        // La guardia contro la stima priva di significato. Un solo chunk in una lingua rara, con una
        // cosine bassa, non deve essere promosso: è il difetto che una normalizzazione min-max PER
        // GRUPPO avrebbe introdotto (gruppo singleton -> intervallo degenere -> 1.0, il massimo).
        var englishGames = NewGames(6);
        var germanGame = NewGames(1)[0];
        var all = englishGames.Append(germanGame).ToArray();

        var sut = SetUpPerGameResults(
            all,
            englishGames.Select(g => Chunk(g, 0.80f, "en"))
                .Append(Chunk(germanGame, 0.50f, "de", content: "LONE_DE"))
                .ToArray());

        var result = await sut.SearchAsync("wie baue ich das Spielbrett auf?", all, 10, PerGameSearchStub.Seed);

        result[^1].Content.Should().Be("LONE_DE",
            "un gruppo con un solo membro non ha una media da cui stimare un offset");
    }

    [Fact]
    public async Task KeywordOnlyHits_DoNotDistortTheLanguageOffsets()
    {
        // Un hit del solo braccio lessicale non ha cosine e non ha lingua (legge text_chunks, che
        // non ha la colonna). Non deve entrare nella stima degli offset né spostare l'esito.
        var italianGames = NewGames(6);
        var englishGames = NewGames(6);
        var keywordGames = NewGames(3);
        var target = Chunk(englishGames[0], 0.82f, "en", content: "TARGET");
        var all = italianGames.Concat(englishGames).Concat(keywordGames).ToArray();

        var sut = SetUpPerGameResults(
            all,
            italianGames.Select(g => Chunk(g, 0.86f, "it"))
                .Concat(new[] { target })
                .Concat(englishGames.Skip(1).Select(g => Chunk(g, 0.80f, "en")))
                .Concat(keywordGames.Select(KeywordOnlyChunk))
                .ToArray());

        var result = await sut.SearchAsync("come si prepara il tabellone?", all, 20, PerGameSearchStub.Seed);

        result[0].Content.Should().Be("TARGET");
    }

    // ------------------------------------------------------------------ helpers

    private static Guid[] NewGames(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

    /// <summary>
    /// Fa restituire a ogni gioco i chunk che gli appartengono, così l'aggregato che arriva a
    /// <c>FuseGlobally</c> è esattamente l'insieme passato.
    /// </summary>
    private MultiGameHybridSearchService SetUpPerGameResults(
        IReadOnlyList<Guid> games, IReadOnlyList<(Guid GameId, SearchResultDto Passage)> chunks)
    {
        foreach (var gameId in games)
        {
            _perGame.Returns(gameId, chunks.Where(c => c.GameId == gameId).Select(c => c.Passage).ToArray());
        }

        return new MultiGameHybridSearchService(
            _perGame.ScopeFactory(),
            new MockEmbeddingService(),
            NullLogger<MultiGameHybridSearchService>.Instance);
    }

    // HybridScore per-gioco: irrilevante, FuseGlobally lo sostituisce.
    private static (Guid GameId, SearchResultDto Passage) Chunk(
        Guid gameId, float cosine, string language, string? content = null) =>
        (gameId, PerGameSearchStub.Passage(
            hybridScore: cosine, vectorScore: cosine, language: language,
            content: content ?? $"chunk {language} {cosine}"));

    private static (Guid GameId, SearchResultDto Passage) KeywordOnlyChunk(Guid gameId) =>
        (gameId, PerGameSearchStub.Passage(
            hybridScore: 0.1f, keywordScore: 0.1f, language: null,
            content: "keyword only", matchedTerms: new[] { "catan" }));
}
