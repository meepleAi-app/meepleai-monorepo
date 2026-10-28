using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// La fixture di <see cref="GameNightCreateRetryEndpointTests"/>.
/// </summary>
/// <remarks>
/// Host condiviso per la classe, database fresco per test (#4050). Il doppio del servizio email vive
/// quanto la classe, quindi il suo comportamento non puo' dipendere dal test: fallisce per i
/// destinatari <c>@down.test</c> e riesce per tutti gli altri, e ogni test usa indirizzi propri
/// (con un <see cref="Guid"/>) per poter contare gli invii senza vedere quelli degli altri.
/// </remarks>
public sealed class GameNightCreateRetryHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "gn_create_retry")
{
    public const string DownDomain = "@down.test";

    private readonly Mock<IGameNightEmailService> _emailService = new();

    protected override Dictionary<string, string?>? ExtraConfiguration => new()
    {
        ["App:BaseUrl"] = "https://meepleai.test",
        ["GameNight:InvitationExpiryDays"] = "14",
    };

    protected override WebApplicationFactory<Program> ConfigureHost(
        WebApplicationFactory<Program> factory)
    {
        _emailService
            .Setup(s => s.SendGameNightInvitationEmailAsync(
                It.Is<string>(to => to.EndsWith(DownDomain, StringComparison.Ordinal)),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp is down"));

        _emailService
            .Setup(s => s.SendGameNightInvitationEmailAsync(
                It.Is<string>(to => !to.EndsWith(DownDomain, StringComparison.Ordinal)),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll(typeof(IGameNightEmailService));
            s.AddSingleton(_emailService.Object);
        }));
    }

    /// <summary>Quante volte il servizio email e' stato chiamato per questo destinatario.</summary>
    public int EmailAttemptsTo(string recipient) =>
        _emailService.Invocations.Count(i =>
            i.Method.Name == nameof(IGameNightEmailService.SendGameNightInvitationEmailAsync)
            && string.Equals((string)i.Arguments[0], recipient, StringComparison.Ordinal));
}

/// <summary>
/// Ripetere la creazione di una serata non deve crearne una seconda.
/// </summary>
/// <remarks>
/// <para>
/// La pagina <c>/game-nights/new</c> ritenta la creazione fino a quattro volte (la specifica del wizard
/// lo chiede: ritardi 1 s, 2 s, 4 s) e React Query ne aggiunge un'altra su ogni mutazione. Ma
/// <c>POST /api/v1/game-nights</c> non era sicuro da ripetere: ogni chiamata creava un aggregato nuovo.
/// Due inneschi reali: la risposta che si perde dopo che il server ha salvato, e il gestore che salva
/// la serata e <b>poi</b> invia gli inviti (transazioni separate, per scelta), per cui un invio email
/// fallito dopo il salvataggio rende errore una richiesta che ha gia' creato la serata.
/// </para>
/// <para>
/// Il contratto: il chiamante puo' mandare un <c>id</c> scelto da lui. Lo stesso <c>id</c> dello stesso
/// organizzatore e' la stessa creazione: non si ricrea, e gli inviti gia' in sospeso si reinviano
/// (consegna "almeno una volta", stesso token). Lo stesso <c>id</c> di un altro organizzatore e' un
/// conflitto e non tocca la serata altrui.
/// </para>
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightCreateRetryEndpointTests
    : IClassFixture<GameNightCreateRetryHostFixture>, IAsyncLifetime
{
    private readonly GameNightCreateRetryHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public GameNightCreateRetryEndpointTests(GameNightCreateRetryHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        _client = _hostFixture.Client;
    }

    // Host, client e database appartengono alla fixture.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Retry_WithTheSameId_ReturnsTheSameGameNight_AndCreatesOnlyOne()
    {
        var (organizerId, token) = await CreateUserAsync();
        var id = Guid.NewGuid();

        var first = await PostCreateAsync(token, NewBody(id));
        var second = await PostCreateAsync(token, NewBody(id));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await second.Content.ReadFromJsonAsync<Guid>()).Should().Be(id);
        (await CountGameNightsAsync(organizerId)).Should().Be(1);
    }

    [Fact]
    public async Task Retry_WithInvitedEmail_ReusesTheGameNight_AndTheInvitation_AndSendsTheEmailAgain()
    {
        var (organizerId, token) = await CreateUserAsync();
        var id = Guid.NewGuid();
        var email = $"guest-{Guid.NewGuid():N}@example.com";

        var first = await PostCreateAsync(token, NewBody(id, email));
        var second = await PostCreateAsync(token, NewBody(id, email));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created, "una ripetizione riuscita non e' un conflitto");
        (await CountGameNightsAsync(organizerId)).Should().Be(1);
        (await CountInvitationsAsync(email)).Should().Be(1, "l'invito in sospeso e' lo stesso, non un secondo");
        _hostFixture.EmailAttemptsTo(email).Should().Be(2, "l'email si reinvia: consegna almeno una volta");
    }

    [Fact]
    public async Task Retry_AfterAnEmailFailure_DoesNotCreateASecondGameNight()
    {
        var (organizerId, token) = await CreateUserAsync();
        var id = Guid.NewGuid();
        var email = $"guest-{Guid.NewGuid():N}{GameNightCreateRetryHostFixture.DownDomain}";

        var first = await PostCreateAsync(token, NewBody(id, email));
        var second = await PostCreateAsync(token, NewBody(id, email));

        // La posta e' giu': entrambe le richieste restano un errore, ed e' giusto (l'utente deve saperlo).
        ((int)first.StatusCode).Should().BeGreaterThanOrEqualTo(500);
        ((int)second.StatusCode).Should().BeGreaterThanOrEqualTo(500);
        // Ma la serata c'e' una volta sola, con un solo invito in sospeso: niente copie per ogni tentativo.
        (await CountGameNightsAsync(organizerId)).Should().Be(1);
        (await CountInvitationsAsync(email)).Should().Be(1);
        _hostFixture.EmailAttemptsTo(email).Should().Be(2, "ogni tentativo riprova a recapitare l'invito");
    }

    [Fact]
    public async Task SameId_FromAnotherOrganizer_IsAConflict_AndLeavesTheFirstGameNightAlone()
    {
        var (ownerId, ownerToken) = await CreateUserAsync();
        var (otherId, otherToken) = await CreateUserAsync();
        var id = Guid.NewGuid();

        var created = await PostCreateAsync(ownerToken, NewBody(id, title: "La serata di chi l'ha creata"));
        var hijack = await PostCreateAsync(otherToken, NewBody(id, title: "Un'altra serata, stesso id"));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        hijack.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CountGameNightsAsync(ownerId)).Should().Be(1);
        (await CountGameNightsAsync(otherId)).Should().Be(0);
        (await TitleOfAsync(id)).Should().Be("La serata di chi l'ha creata");
    }

    // ────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────

    private async Task<(Guid UserId, string Token)> CreateUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await TestSessionHelper.CreateUserSessionAsync(dbContext);
    }

    private static object NewBody(Guid id, string? email = null, string title = "Serata di prova") => new
    {
        id,
        title,
        scheduledAt = DateTimeOffset.UtcNow.AddDays(3),
        invitedEmails = email is null ? null : new[] { email },
    };

    private Task<HttpResponseMessage> PostCreateAsync(string sessionToken, object body) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/game-nights", sessionToken, body));

    private async Task<int> CountGameNightsAsync(Guid organizerId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await dbContext.GameNightEvents.CountAsync(e => e.OrganizerId == organizerId);
    }

    private async Task<int> CountInvitationsAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await dbContext.GameNightInvitations.CountAsync(i => i.Email == email.ToLowerInvariant());
    }

    private async Task<string?> TitleOfAsync(Guid gameNightId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await dbContext.GameNightEvents
            .Where(e => e.Id == gameNightId)
            .Select(e => e.Title)
            .SingleOrDefaultAsync();
    }
}
