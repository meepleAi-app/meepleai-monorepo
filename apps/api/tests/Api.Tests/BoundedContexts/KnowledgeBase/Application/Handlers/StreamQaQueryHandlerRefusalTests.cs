using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// #3850 e #4481 sulla chat in streaming: il modello dice la rinuncia in inglese, a pezzi; l'utente
/// la legge nella lingua della risposta, la cache conserva quel testo, e il completamento non la
/// dice «Grounded».
/// </summary>
public partial class StreamQaQueryHandlerTests
{
    private const string EnglishRefusal = "This information is not available in the provided rulebook.";
    private const string ItalianRefusal = "Questa informazione non è disponibile nel regolamento fornito.";

    private static readonly string[] EnglishRefusalInTokens =
        ["This", " information", " is", " not", " available", " in", " the", " provided", " rule", "book", "."];

    private void TheQuestionIsIn(string language) =>
        _languageDetectionMock
            .Setup(x => x.Detect(It.IsAny<string>()))
            .Returns(new LanguageDetection(language, IsConfident: true));

    private static List<string> TokensOf(IEnumerable<RagStreamingEvent> events) =>
        events.Where(e => e.Type == StreamingEventType.Token)
            .Select(e => e.Data.Should().BeOfType<StreamingToken>().Which.token)
            .ToList();

    private static StreamingComplete CompleteOf(IEnumerable<RagStreamingEvent> events) =>
        events.Single(e => e.Type == StreamingEventType.Complete)
            .Data.Should().BeOfType<StreamingComplete>().Which;

    [Fact]
    public async Task ItalianQuestion_TheEnglishRefusalInPieces_ReachesTheUserInItalian_UngroundedAndCachedInItalian()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Posso scambiare con la banca due a uno?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        TheQuestionIsIn("it");
        SetupLlmStreamingMock(EnglishRefusalInTokens);
        QaResponse? cached = null;
        _cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<QaResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, QaResponse, int, CancellationToken>((_, response, _, _) => cached = response)
            .Returns(Task.CompletedTask);

        var events = await CollectEventsAsync(query);

        var tokens = TokensOf(events);
        string.Concat(tokens).Should().Be(ItalianRefusal);
        tokens.Should().NotContain(t => t.Contains("information", StringComparison.OrdinalIgnoreCase)
            || t.Contains("rulebook", StringComparison.OrdinalIgnoreCase));
        events.Should().Contain(e => e.Type == StreamingEventType.Citations, "retrieval found sources");
        var complete = CompleteOf(events);
        complete.GroundingStatus.Should().Be("Ungrounded", "a refusal is not an answer grounded in the sources");
        complete.Language.Should().Be("it");
        complete.completionTokens.Should().Be(EnglishRefusalInTokens.Length, "the tokens are those the model produced");
        cached.Should().NotBeNull();
        cached!.answer.Should().Be(ItalianRefusal, "the cache serves what the user read");
    }

    [Fact]
    public async Task ItalianQuestion_ANormalAnswer_FlowsUnchanged_AndIsGrounded()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Posso scambiare con la banca?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        TheQuestionIsIn("it");
        string[] answer = ["Sì", ",", " puoi", " scambiare", " 4:1", " [Page 1]", "."];
        SetupLlmStreamingMock(answer);

        var events = await CollectEventsAsync(query);

        TokensOf(events).Should().Equal(answer);
        CompleteOf(events).GroundingStatus.Should().Be("Grounded");
    }

    [Fact]
    public async Task ItalianQuestion_AnAnswerThatStartsLikeTheRefusal_IsDeliveredWhole_AndGrounded()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Dove trovo questa informazione?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        TheQuestionIsIn("it");
        SetupLlmStreamingMock(["This", " information", " is", " on", " page", " 1", "."]);

        var events = await CollectEventsAsync(query);

        string.Concat(TokensOf(events)).Should().Be("This information is on page 1.");
        CompleteOf(events).GroundingStatus.Should().Be("Grounded");
    }

    [Fact]
    public async Task ItalianQuestion_ARefusalInTail_ReplacesOnlyTheTail_AndStaysGrounded()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Come si calcola la strada più lunga?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        TheQuestionIsIn("it");
        SetupLlmStreamingMock(["- La tessera vale", " **2 PV** [Page 8].", "\n\n", .. EnglishRefusalInTokens]);

        var events = await CollectEventsAsync(query);

        string.Concat(TokensOf(events)).Should().Be("- La tessera vale **2 PV** [Page 8].\n\n" + ItalianRefusal);
        CompleteOf(events).GroundingStatus.Should().Be("Grounded", "the answer is partial, not a refusal");
    }

    [Fact]
    public async Task ItalianQuestion_AnEnglishRefusalAlreadyInTheCache_IsReplayedInItalian_AndUngrounded()
    {
        // Le voci scritte prima di questa correzione portano la frase inglese.
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Posso scambiare con la banca?", TestUserId, "User");
        TheQuestionIsIn("it");
        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QaResponse(
                EnglishRefusal,
                new List<Snippet> { new("Sample rule text", "PDF:doc-1", 1, 0, 0.9f) },
                0, 11, 11, 0.8));

        var events = await CollectEventsAsync(query);

        string.Concat(TokensOf(events)).Should().Be(ItalianRefusal);
        CompleteOf(events).GroundingStatus.Should().Be("Ungrounded");
    }

    [Fact]
    public async Task ItalianQuestion_WhenRetrievalFindsNothing_TheMessageIsItalian()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Posso scambiare con la banca?", TestUserId, "User");
        TheQuestionIsIn("it");
        _cacheMock
            .Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new[] { 0.1f, 0.2f } } });
        _keywordSearchServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<KeywordSearchResult>());

        var events = await CollectEventsAsync(query);

        var error = events.Single(e => e.Type == StreamingEventType.Error).Data.Should().BeOfType<StreamingError>().Which;
        error.errorCode.Should().Be("NO_RESULTS");
        error.errorMessage.Should().Be("Nel regolamento non ho trovato informazioni pertinenti.");
    }

    /// <summary>I pezzi dati, poi la fine con il motivo «length»: la risposta è stata troncata.</summary>
    private static async IAsyncEnumerable<StreamChunk> TruncatedStreamAsync(string[] tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Yield();
            yield return new StreamChunk(token);
        }

        yield return new StreamChunk(null, IsFinal: true, FinishReason: "length");
    }

    /// <summary>
    /// Review di #4494: le posizioni delle citazioni in linea e la risposta parziale del token di
    /// continuazione sono sul testo che il client ha ricevuto, cioè la concatenazione dei token.
    /// </summary>
    private async Task OffsetsAndPartialAnswer_AreOnTheTextTheClientReceived(string[] tokens)
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Come funziona il brigante?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        TheQuestionIsIn("it");
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(TruncatedStreamAsync(tokens));

        var events = await CollectEventsAsync(query);

        var received = string.Concat(TokensOf(events));
        received.Should().Contain(ItalianRefusal).And.NotContain("rulebook");

        var inline = events.Single(e => e.Type == StreamingEventType.InlineCitation)
            .Data.Should().BeOfType<StreamingInlineCitations>().Which.Citations;
        inline.Should().NotBeEmpty();
        foreach (var citation in inline)
        {
            received[citation.StartOffset..citation.EndOffset].Should().BeEquivalentTo("Sample rule text");
        }

        var continuation = events.Single(e => e.Type == StreamingEventType.ContinuationAvailable)
            .Data.Should().BeOfType<StreamingContinuation>().Which;
        var payload = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(continuation.ContinuationToken));
        payload.RootElement.GetProperty("partialAnswer").GetString().Should().Be(received);
    }

    [Fact]
    public async Task ARefusalInTail_InlineCitationsAndPartialAnswer_AreOnTheLocalizedText()
    {
        await OffsetsAndPartialAnswer_AreOnTheTextTheClientReceived(
            ["- Sample rule text", " [Page 1].", "\n\n", .. EnglishRefusalInTokens]);
    }

    [Fact]
    public async Task ARefusalBeforeACitedSentence_InlineCitationsPointAfterTheLocalizedText()
    {
        // La frase italiana è più lunga di quella inglese: sul testo grezzo la citazione dopo la
        // rinuncia cadrebbe tre caratteri prima.
        await OffsetsAndPartialAnswer_AreOnTheTextTheClientReceived(
            ["Per il resto:", " This", " information is not available", " in the provided rulebook", ".", " Sample rule text", " [Page 1]."]);
    }

    private static async IAsyncEnumerable<StreamChunk> InterruptedInTheMiddleOfTheRefusalAsync()
    {
        await Task.Yield();
        yield return new StreamChunk("Sì [Page 1]. ");
        yield return new StreamChunk("This");
        yield return new StreamChunk(" information");
        yield return new StreamChunk(null, IsFinal: true,
            ErrorCode: LlmAnswerErrorCodes.StreamInterrupted, ErrorMessage: "The answer was interrupted before it was complete.");
    }

    [Fact]
    public async Task AnInterruptedStream_DropsWhatWasHeld_WhichIsOnlyTheStartOfTheEnglishSentence()
    {
        // Review di #4494: su AI_STREAM_INTERRUPTED lo stream finisce con l'errore e il trattenuto non
        // esce. Il trattenuto è sempre l'inizio della frase inglese, e la chat non salva una risposta
        // interrotta: non c'è niente da recuperare.
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Posso scambiare?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        TheQuestionIsIn("it");
        _llmServiceMock
            .Setup(x => x.GenerateAnswerStreamAsync(It.IsAny<LlmAnswerRequest>(), It.IsAny<CancellationToken>()))
            .Returns(InterruptedInTheMiddleOfTheRefusalAsync());

        var events = await CollectEventsAsync(query);

        TokensOf(events).Should().Equal("Sì [Page 1]. ");
        events.Single(e => e.Type == StreamingEventType.Error)
            .Data.Should().BeOfType<StreamingError>().Which.errorCode.Should().Be(LlmAnswerErrorCodes.StreamInterrupted);
        events.Should().NotContain(e => e.Type == StreamingEventType.Complete);
    }

    [Fact]
    public async Task EnglishQuestion_TheRefusal_StaysEnglish_AndIsUngrounded()
    {
        var gameId = Guid.NewGuid().ToString();
        var query = new StreamQaQuery(gameId, "Can I trade 2:1 with the bank?", TestUserId, "User");
        SetupHappyPathMocks(gameId, query.Query);
        TheQuestionIsIn("en");
        SetupLlmStreamingMock(EnglishRefusalInTokens);

        var events = await CollectEventsAsync(query);

        TokensOf(events).Should().Equal(EnglishRefusalInTokens, "in English every token goes out as it arrives");
        CompleteOf(events).GroundingStatus.Should().Be("Ungrounded");
    }
}
