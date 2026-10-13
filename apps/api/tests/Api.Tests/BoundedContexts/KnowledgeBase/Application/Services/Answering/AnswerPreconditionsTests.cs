using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168 (5g-a). Il passo che i tre percorsi di risposta eseguono prima di cercare: chi chiede può
/// interrogare questo gioco? È lo stesso controllo di prima, in un punto solo. E, con la 5g-b, il
/// passo 2: la quota del piano.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4168")]
public sealed class AnswerPreconditionsTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Game = Guid.NewGuid();

    private readonly Mock<IRagAccessService> _access = new();
    private readonly Mock<ITierEnforcementService> _tiers = new();
    private readonly Mock<ILogger<AnswerPreconditions>> _logger = new();
    private readonly AnswerPreconditions _sut;

    public AnswerPreconditionsTests()
    {
        _sut = new AnswerPreconditions(_access.Object, _tiers.Object, _logger.Object);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AUserWhoMayQueryTheGame_Passes()
    {
        _access.Setup(a => a.CanAccessRagAsync(User, Game, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _sut.EnsureCanAskAboutGameAsync(User, "User", Game, Ct);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AUserWhoMayNot_IsRefused_NamingTheGame()
    {
        _access.Setup(a => a.CanAccessRagAsync(User, Game, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => _sut.EnsureCanAskAboutGameAsync(User, "User", Game, Ct);

        (await act.Should().ThrowAsync<RagOwnershipRequiredException>()).Which.GameId.Should().Be(Game);
    }

    /// <summary>
    /// Il ruolo arriva dalla sessione come testo. Decide il passaggio degli amministratori, quindi
    /// va letto senza badare alle maiuscole, e un ruolo sconosciuto vale come utente: nel dubbio
    /// si controlla, non si lascia passare.
    /// </summary>
    [Theory]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("admin", UserRole.Admin)]
    [InlineData("SuperAdmin", UserRole.SuperAdmin)]
    [InlineData("Editor", UserRole.Editor)]
    [InlineData("User", UserRole.User)]
    [InlineData("qualcosa-che-non-esiste", UserRole.User)]
    [InlineData("", UserRole.User)]
    [InlineData(null, UserRole.User)]
    public async Task TheRoleOfTheSession_IsTheRoleTheGateSees(string? roleFromTheSession, UserRole expected)
    {
        _access.Setup(a => a.CanAccessRagAsync(User, Game, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.EnsureCanAskAboutGameAsync(User, roleFromTheSession, Game, Ct);

        _access.Verify(a => a.CanAccessRagAsync(User, Game, expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheGamesOpenToAUser_AreTheOnesTheAccessServiceLists()
    {
        var games = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _access.Setup(a => a.GetAccessibleGameIdsAsync(User, UserRole.User, It.IsAny<CancellationToken>()))
            .ReturnsAsync(games);

        var open = await _sut.GamesOpenToAsync(User, UserRole.User, Ct);

        open.Should().Equal(games);
    }

    // ─── Passo 2: la quota del piano (5g-b) ──────────────────────

    private void ThePlanSays(int used, int max, bool counterReadable = true) =>
        _tiers.Setup(t => t.CheckUsageAsync(User, TierAction.AgentQuery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierUsageCheck(used, max, counterReadable));

    [Fact]
    public async Task AQuestionUnderTheQuota_Passes()
    {
        ThePlanSays(used: 2, max: 3);

        var quota = await _sut.CheckQuotaAsync(User, Ct);

        quota.Should().Be(new AnswerQuota(User, Current: 2, Max: 3, Exceeded: false, Counts: true));
    }

    /// <summary>
    /// Tre risposte consegnate oggi su un piano da tre: la quarta domanda si ferma, e chi la ferma
    /// sa dire i numeri.
    /// </summary>
    [Fact]
    public async Task AQuestionAtTheQuota_IsStopped_WithTheNumbersOfThePlan()
    {
        ThePlanSays(used: 3, max: 3);

        var quota = await _sut.CheckQuotaAsync(User, Ct);

        quota.Should().Be(new AnswerQuota(User, Current: 3, Max: 3, Exceeded: true, Counts: true));
    }

    [Fact]
    public async Task ADeliveredAnswer_IsCountedOnce()
    {
        ThePlanSays(used: 2, max: 3);
        var quota = await _sut.CheckQuotaAsync(User, Ct);

        await _sut.RecordAnswerDeliveredAsync(quota, Ct);

        _tiers.Verify(t => t.RecordUsageAsync(User, TierAction.AgentQuery, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Admin ed editor non hanno un limite: non vengono fermati, e il loro contatore non cresce.
    /// </summary>
    [Fact]
    public async Task APlanWithoutALimit_IsNeverStopped_AndItsAnswersAreNotCounted()
    {
        ThePlanSays(used: 0, max: int.MaxValue);

        var quota = await _sut.CheckQuotaAsync(User, Ct);
        await _sut.RecordAnswerDeliveredAsync(quota, Ct);

        quota.Exceeded.Should().BeFalse();
        quota.Counts.Should().BeFalse();
        _tiers.Verify(
            t => t.RecordUsageAsync(It.IsAny<Guid>(), It.IsAny<TierAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Redis non risponde: la quota non blocca, ed è voluto — la spesa la protegge il tetto del
    /// gateway, che invece blocca. Ma non deve passare in silenzio: serve un warning, e la risposta
    /// va comunque contata, se a quel punto il contatore è tornato.
    /// </summary>
    [Fact]
    public async Task WhenTheCounterCannotBeRead_TheQuestionPasses_WithAWarning()
    {
        ThePlanSays(used: 0, max: 3, counterReadable: false);

        var quota = await _sut.CheckQuotaAsync(User, Ct);

        quota.Exceeded.Should().BeFalse();
        quota.Counts.Should().BeTrue();
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// E una metrica: finché Redis è giù la quota non vale per nessuno, e un log non si conta.
    /// Il listener è di processo, quindi vede anche le domande di altri test che girano insieme:
    /// si asserisce che questa c'è, non quante sono.
    /// </summary>
    [Fact]
    public async Task WhenTheCounterCannotBeRead_TheMetricCountsIt()
    {
        ThePlanSays(used: 0, max: 3, counterReadable: false);
        long counted = 0;
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (string.Equals(instrument.Name, "meepleai.answer.quota.unreadable", StringComparison.Ordinal))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => Interlocked.Add(ref counted, measurement));
        listener.Start();

        await _sut.CheckQuotaAsync(User, Ct);

        Interlocked.Read(ref counted).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task WhenTheCounterIsRead_NothingIsLoggedAsAWarning()
    {
        ThePlanSays(used: 2, max: 3);

        await _sut.CheckQuotaAsync(User, Ct);

        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Nello stream la domanda fermata è un evento <c>Error</c> con gli stessi campi del 403
    /// <c>tier_limit_exceeded</c> che gli altri limiti di piano usano già: il frontend li legge così.
    /// </summary>
    [Fact]
    public void AStoppedQuestion_SaysInTheStreamWhatThe403Says()
    {
        var quota = new AnswerQuota(User, Current: 3, Max: 3, Exceeded: true, Counts: true);

        var refusal = quota.ToException();
        var error = quota.ToStreamingError();

        refusal.LimitType.Should().Be("AgentQuery");
        (refusal.Current, refusal.Max, refusal.UpgradeUrl).Should().Be((3, 3, "/pricing"));
        error.Should().Be(new StreamingError(
            "Tier limit exceeded: AgentQuery (3/3)",
            "TIER_LIMIT_EXCEEDED",
            limitType: "AgentQuery",
            current: 3,
            max: 3,
            upgradeUrl: "/pricing"));
    }

    /// <summary>
    /// Gli altri eventi d'errore restano com'erano sul filo: i campi della quota compaiono solo
    /// quando ci sono.
    /// </summary>
    [Fact]
    public void OnTheWire_TheQuotaFieldsAppearOnlyOnAQuotaError()
    {
        var quota = new AnswerQuota(User, Current: 3, Max: 3, Exceeded: true, Counts: true);

        var stopped = System.Text.Json.JsonSerializer.Serialize(quota.ToStreamingError());
        var other = System.Text.Json.JsonSerializer.Serialize(new StreamingError("No relevant information found.", "NO_RESULTS"));

        stopped.Should().Be(
            """{"errorMessage":"Tier limit exceeded: AgentQuery (3/3)","errorCode":"TIER_LIMIT_EXCEEDED","limitType":"AgentQuery","current":3,"max":3,"upgradeUrl":"/pricing"}""");
        other.Should().Be("""{"errorMessage":"No relevant information found.","errorCode":"NO_RESULTS"}""");
    }
}
