using System.Text.Json;
using Api.BoundedContexts.Authentication.Application.Commands.CreateShareLink;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;
using Api.BoundedContexts.UserNotifications.Infrastructure.Services;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using Api.Infrastructure.Entities;
using Api.Routing;
using Api.Services;
using Api.Services.Email;
using Api.SharedKernel.Application;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;
using IUserRepository = Api.BoundedContexts.Authentication.Infrastructure.Persistence.IUserRepository;

namespace Api.Tests.SharedKernel.Services;

/// <summary>
/// #4774: i generatori di link del backend elencati in <c>Producers</c> compongono la base dall'URL pubblico
/// del front-end, e lo risolvono attraverso un solo punto. Prima, inviti, condivisioni e disiscrizione leggevano
/// <c>App:BaseUrl</c> (che in staging e produzione nessuno imposta, e che <c>appsettings.json</c> riempie con
/// <c>localhost:3000</c>) mentre l'email di verifica leggeva <c>Frontend:BaseUrl</c>: gli stessi link, due
/// chiavi, due ripieghi.
/// </summary>
/// <remarks>
/// Un caso solo per scenario, eseguito su <b>ogni generatore in elenco</b>: un generatore nuovo che compone un
/// link senza passare dal punto unico si aggiunge qui e cade nello scenario che lo riguarda. Ogni produttore
/// restituisce il testo che porta il link (corpo dell'email, DTO, JSON del messaggio Slack). L'elenco è tenuto a
/// mano: un generatore che non vi compare non è coperto da nessuno scenario. Il digest admin delle richieste di
/// condivisione ne è rimasto fuori, con un dominio fisso scritto nel job, finché un <c>git grep</c> del dominio
/// non l'ha trovato: cercare i domini scritti a mano resta il controllo che l'elenco non fa.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
public sealed class PublicBaseUrlLinkGeneratorsTests
{
    private const string FrontendUrl = "https://front.example.test";
    private const string LegacyUrl = "https://legacy.example.test";
    private const string EnvironmentVariableUrl = "https://env.example.test";

    private delegate Task<string> LinkProducer(IConfiguration configuration);

    private static readonly Dictionary<string, LinkProducer> Producers = new(StringComparer.Ordinal)
    {
        ["game-night-invitation-email"] = ProduceGameNightInvitationEmailLinkAsync,
        ["session-invite-token"] = ProduceSessionInviteLinkAsync,
        ["session-share-link"] = ProduceSessionShareLinkAsync,
        ["library-share-create"] = ProduceLibraryShareCreateLinkAsync,
        ["library-share-update"] = ProduceLibraryShareUpdateLinkAsync,
        ["library-share-get"] = ProduceLibraryShareGetLinkAsync,
        ["chat-share-link"] = ProduceChatShareLinkAsync,
        ["unsubscribe-url"] = ProduceUnsubscribeLinkAsync,
        ["email-service-invitation"] = ProduceEmailServiceInvitationLinkAsync,
        ["email-template-support-and-preferences"] = ProduceEmailTemplateLinksAsync,
        ["slack-generic"] = ProduceGenericSlackLinkAsync,
        ["slack-pdf-processing"] = ProducePdfSlackLinkAsync,
        ["slack-share-request"] = ProduceShareRequestSlackLinkAsync,
        ["password-reset-url"] = ProducePasswordResetLinkAsync,
        ["oauth-frontend-redirect"] = ProduceOAuthRedirectAsync,
        ["admin-share-request-digest-job"] = ProduceAdminDigestJobLinkAsync,
    };

    public static TheoryData<string> Generators() => new(Producers.Keys);

    private static async Task<string> ProduceAsync(string generator, Dictionary<string, string?> urlKeys)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(urlKeys).Build();
        return await Producers[generator](configuration);
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public async Task WithOnlyTheFrontendKey_TheLinkUsesIt(string generator)
    {
        var link = await ProduceAsync(generator, new() { ["Frontend:BaseUrl"] = FrontendUrl });

        link.Should().Contain($"{FrontendUrl}/");
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public async Task WithOnlyTheLegacyAppKey_TheLinkStillUsesIt(string generator)
    {
        var link = await ProduceAsync(generator, new() { ["App:BaseUrl"] = LegacyUrl });

        link.Should().Contain($"{LegacyUrl}/");
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public async Task WithBothKeys_TheFrontendKeyWins(string generator)
    {
        var link = await ProduceAsync(generator, new()
        {
            ["Frontend:BaseUrl"] = FrontendUrl,
            ["App:BaseUrl"] = LegacyUrl,
        });

        link.Should().Contain($"{FrontendUrl}/");
        link.Should().NotContain(LegacyUrl);
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public async Task WithOnlyTheEnvironmentVariableAlias_TheLinkUsesIt(string generator)
    {
        var link = await ProduceAsync(generator, new() { ["FRONTEND_BASE_URL"] = EnvironmentVariableUrl });

        link.Should().Contain($"{EnvironmentVariableUrl}/");
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public async Task ABlankFrontendKeyIsNotAValue_TheNextSourceDecides(string generator)
    {
        // `${VAR:-}` in a compose file hands the container a PRESENT, EMPTY variable.
        var link = await ProduceAsync(generator, new()
        {
            ["Frontend:BaseUrl"] = "  ",
            ["App:BaseUrl"] = LegacyUrl,
        });

        link.Should().Contain($"{LegacyUrl}/");
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public async Task TrailingSlashesOnTheConfiguredValueNeverDoubleUpInTheLink(string generator)
    {
        var link = await ProduceAsync(generator, new() { ["Frontend:BaseUrl"] = FrontendUrl + "//" });

        link.Should().Contain($"{FrontendUrl}/");
        link.Should().NotContain("front.example.test//");
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public async Task WithNoKeyAtAll_InADeployedEnvironment_NoLinkIsMadeUp(string generator)
    {
        // The producers build the provider for a deployed environment: nothing is configured, so there is
        // no honest base to write. A made-up localhost link in a production e-mail fails silently; this fails
        // where somebody can see it.
        var act = async () => await ProduceAsync(generator, new Dictionary<string, string?>());

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        failure.Which.ToString().Should().Contain("Frontend:BaseUrl");
    }

    [Fact]
    public async Task TheUnsubscribeLink_KeepsThePublishedApiAddressAheadOfTheFrontendOne()
    {
        // The unsubscribe page is an API route: the address the API is published at (the OAuth callback
        // base) was always first, and the front-end URL only stands in when there is none.
        var link = await ProduceAsync("unsubscribe-url", new()
        {
            ["Authentication:OAuth:CallbackBaseUrl"] = "https://api.example.test/",
            ["Frontend:BaseUrl"] = FrontendUrl,
        });

        link.Should().StartWith("https://api.example.test/api/v1/notifications/unsubscribe?token=");
    }

    [Fact]
    public async Task ThePasswordResetLink_AnExplicitResetAddressStillWins()
    {
        var link = await ProduceAsync("password-reset-url", new()
        {
            ["Email:ResetUrlBase"] = "https://accounts.example.test/choose-new-password",
            ["Frontend:BaseUrl"] = FrontendUrl,
        });

        link.Should().Be("https://accounts.example.test/choose-new-password?token=reset-token");
    }

    [Fact]
    public async Task ThePasswordResetLink_HangsOffTheFrontendUrlOnTheResetPage()
    {
        var link = await ProduceAsync("password-reset-url", new() { ["Frontend:BaseUrl"] = FrontendUrl });

        link.Should().Be($"{FrontendUrl}/reset-password?token=reset-token");
    }

    [Fact]
    public async Task TheAdminDigestJob_LinksToTheReviewQueueOfTheFrontend()
    {
        var run = await RunAdminDigestJobAsync(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = FrontendUrl + "/" })
            .Build());

        run.ReviewQueueUrls.Should().Equal($"{FrontendUrl}/admin/share-requests");
    }

    [Fact]
    public async Task TheAdminDigestJob_WithNoKeyInADeployedEnvironment_SendsNoEmailAndSaysWhyItStopped()
    {
        // The job does not rethrow (Quartz would only log it): the outcome is no e-mail and a result that names
        // the key to set. A digest with a made-up link would be a mail nobody could act on.
        var run = await RunAdminDigestJobAsync(new ConfigurationBuilder().Build());

        run.ReviewQueueUrls.Should().BeEmpty();
        run.Email.Verify(
            e => e.SendAdminShareRequestDigestEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Dictionary<string, int>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        run.Failure.Should().NotBeNull().And.Contain("Frontend:BaseUrl");
    }

    // ------------------------------------------------------------------------------------------------------
    // Producers: one per link generator. Each builds the real component over the given configuration.
    // ------------------------------------------------------------------------------------------------------

    private static async Task<string> ProduceGameNightInvitationEmailLinkAsync(IConfiguration configuration)
    {
        var organizerId = Guid.NewGuid();
        var gameNight = GameNightEvent.Create(
            organizerId: organizerId,
            title: "Friday Night Catan",
            scheduledAt: DateTimeOffset.UtcNow.AddDays(7),
            description: null,
            location: "My place",
            maxPlayers: 5,
            gameIds: null);

        var invitations = new Mock<IGameNightInvitationRepository>();
        var gameNights = new Mock<IGameNightEventRepository>();
        gameNights
            .Setup(r => r.GetByIdAsync(gameNight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameNight);
        var email = new Mock<IGameNightEmailService>();

        var handler = new CreateGameNightInvitationByEmailCommandHandler(
            invitations.Object,
            gameNights.Object,
            new Mock<IUserRepository>().Object,
            new Mock<IGameCoreDataProvider>().Object,
            email.Object,
            new Mock<IUnitOfWork>().Object,
            configuration,
            PublicBaseUrlTestSupport.ProviderFor(configuration),
            TimeProvider.System);

        await handler.Handle(
            new CreateGameNightInvitationByEmailCommand(gameNight.Id, "guest@example.com", organizerId),
            CancellationToken.None);

        var call = email.Invocations.Single(i => i.Method.Name == nameof(IGameNightEmailService.SendGameNightInvitationEmailAsync));
        return $"{call.Arguments[6]} {call.Arguments[7]}";
    }

    private static (Mock<ISessionRepository> Repo, Session Session, Guid OwnerId) AnOwnedSession()
    {
        var ownerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.GameSpecific);
        typeof(Session).GetProperty("Id")!.SetValue(session, sessionId);
        var repo = new Mock<ISessionRepository>();
        repo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        return (repo, session, ownerId);
    }

    private static async Task<string> ProduceSessionInviteLinkAsync(IConfiguration configuration)
    {
        var (repo, session, ownerId) = AnOwnedSession();
        var handler = new GenerateInviteTokenCommandHandler(
            repo.Object,
            new Mock<IUnitOfWork>().Object,
            PublicBaseUrlTestSupport.ProviderFor(configuration),
            NullLogger<GenerateInviteTokenCommandHandler>.Instance);

        var response = await handler.Handle(
            new GenerateInviteTokenCommand(session.Id, ownerId, 24), CancellationToken.None);

        return response.InviteUrl;
    }

    private static async Task<string> ProduceSessionShareLinkAsync(IConfiguration configuration)
    {
        var (repo, session, ownerId) = AnOwnedSession();
        var scores = new Mock<IScoreEntryRepository>();
        scores
            .Setup(r => r.GetBySessionIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<ScoreEntry>());
        var handler = new GenerateSessionShareLinkQueryHandler(
            repo.Object,
            scores.Object,
            PublicBaseUrlTestSupport.ProviderFor(configuration),
            NullLogger<GenerateSessionShareLinkQueryHandler>.Instance);

        var response = await handler.Handle(
            new GenerateSessionShareLinkQuery(session.Id, ownerId), CancellationToken.None);

        return $"{response.ShareUrl} {response.OgMetadata.ImageUrl} {response.OgMetadata.Url}";
    }

    private static async Task<string> ProduceLibraryShareCreateLinkAsync(IConfiguration configuration)
    {
        var userId = Guid.NewGuid();
        var repo = new Mock<ILibraryShareLinkRepository>();
        repo.Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        repo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((LibraryShareLink?)null);
        var handler = new CreateLibraryShareLinkCommandHandler(
            repo.Object,
            new Mock<IUnitOfWork>().Object,
            PublicBaseUrlTestSupport.ProviderFor(configuration),
            NullLogger<CreateLibraryShareLinkCommandHandler>.Instance);

        var dto = await handler.Handle(
            new CreateLibraryShareLinkCommand(userId, "public", true, null), CancellationToken.None);

        return dto.ShareUrl;
    }

    private static async Task<string> ProduceLibraryShareUpdateLinkAsync(IConfiguration configuration)
    {
        var userId = Guid.NewGuid();
        var link = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, true, null);
        var repo = new Mock<ILibraryShareLinkRepository>();
        repo.Setup(r => r.GetByShareTokenAsync(link.ShareToken, It.IsAny<CancellationToken>())).ReturnsAsync(link);
        var handler = new UpdateLibraryShareLinkCommandHandler(
            repo.Object,
            new Mock<IUnitOfWork>().Object,
            PublicBaseUrlTestSupport.ProviderFor(configuration),
            NullLogger<UpdateLibraryShareLinkCommandHandler>.Instance);

        var dto = await handler.Handle(
            new UpdateLibraryShareLinkCommand(userId, link.ShareToken, "unlisted", null, null),
            CancellationToken.None);

        return dto.ShareUrl;
    }

    private static async Task<string> ProduceLibraryShareGetLinkAsync(IConfiguration configuration)
    {
        var userId = Guid.NewGuid();
        var link = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, true, null);
        var repo = new Mock<ILibraryShareLinkRepository>();
        repo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(link);
        var handler = new GetLibraryShareLinkQueryHandler(repo.Object, PublicBaseUrlTestSupport.ProviderFor(configuration));

        var dto = await handler.Handle(new GetLibraryShareLinkQuery(userId), CancellationToken.None);

        return dto!.ShareUrl;
    }

    private static async Task<string> ProduceChatShareLinkAsync(IConfiguration configuration)
    {
        // The handler signs the token with its own key: add it next to the URL keys under test.
        var withSecret = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ShareLinks:SecretKey"] = "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256",
            })
            .Build();

        var userId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var thread = new ChatThreadEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Share me",
            LastMessageAt = DateTime.UtcNow,
        };
        db.ChatThreads.Add(thread);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CreateShareLinkCommandHandler(
            db, new Mock<IShareLinkRepository>().Object, withSecret, PublicBaseUrlTestSupport.ProviderFor(withSecret));

        var result = await handler.Handle(
            new CreateShareLinkCommand(thread.Id, ShareLinkRole.View, DateTime.UtcNow.AddDays(1), null, userId),
            CancellationToken.None);

        return result.ShareableUrl;
    }

    private static Task<string> ProduceUnsubscribeLinkAsync(IConfiguration configuration)
    {
        var withSecret = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-for-unsubscribe-tokens-minimum-32-chars-long",
            })
            .Build();

        var service = new UnsubscribeTokenService(withSecret, PublicBaseUrlTestSupport.ProviderFor(withSecret));

        return Task.FromResult(service.GenerateUnsubscribeUrl(Guid.NewGuid(), "document_ready"));
    }

    private static async Task<string> ProduceEmailServiceInvitationLinkAsync(IConfiguration configuration)
    {
        var withTransport = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FromAddress"] = "noreply@meepleai.app",
                ["Email:EnableSsl"] = "false",
            })
            .Build();

        EmailRequest? captured = null;
        var sender = new Mock<IEmailSender>();
        sender
            .Setup(s => s.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<EmailRequest, CancellationToken>((request, _) => captured = request)
            .Returns(Task.CompletedTask);

        var service = new EmailService(
            withTransport,
            NullLogger<EmailService>.Instance,
            sender.Object,
            publicBaseUrl: PublicBaseUrlTestSupport.ProviderFor(withTransport));

        await service.SendInvitationEmailAsync("recipient@example.com", "User", "abc123", "Admin", CancellationToken.None);

        return captured!.HtmlBody;
    }

    private static Task<string> ProduceEmailTemplateLinksAsync(IConfiguration configuration)
    {
        var service = new EmailTemplateService(PublicBaseUrlTestSupport.ProviderFor(configuration));

        // The failure template links the support page; every template links the notification preferences.
        return Task.FromResult(service.RenderDocumentFailed("Mario", "rules.pdf", "boom"));
    }

    private static Task<string> ProducePasswordResetLinkAsync(IConfiguration configuration)
    {
        var service = new EmailService(
            configuration,
            NullLogger<EmailService>.Instance,
            sender: null,
            publicBaseUrl: PublicBaseUrlTestSupport.ProviderFor(configuration));

        return Task.FromResult(service.BuildPasswordResetUrl("reset-token"));
    }

    private static Task<string> ProduceOAuthRedirectAsync(IConfiguration configuration) =>
        Task.FromResult(OAuthEndpoints.FrontendUrl(
            PublicBaseUrlTestSupport.ProviderFor(configuration), "/oauth-callback?success=true&new=False"));

    private static Task<string> ProduceGenericSlackLinkAsync(IConfiguration configuration)
    {
        var builder = new GenericSlackBuilder(PublicBaseUrlTestSupport.ProviderFor(configuration));

        return Task.FromResult(JsonSerializer.Serialize(
            builder.BuildMessage(new GenericPayload("Title", "Body"), "/notifications/123")));
    }

    private static Task<string> ProducePdfSlackLinkAsync(IConfiguration configuration)
    {
        var builder = new PdfProcessingSlackBuilder(PublicBaseUrlTestSupport.ProviderFor(configuration));

        return Task.FromResult(JsonSerializer.Serialize(
            builder.BuildMessage(new PdfProcessingPayload(Guid.NewGuid(), "rules.pdf", "Ready"), "/documents/123")));
    }

    private static Task<string> ProduceShareRequestSlackLinkAsync(IConfiguration configuration)
    {
        var builder = new ShareRequestSlackBuilder(PublicBaseUrlTestSupport.ProviderFor(configuration), TimeProvider.System);

        return Task.FromResult(JsonSerializer.Serialize(
            builder.BuildMessage(new ShareRequestPayload(Guid.NewGuid(), "Mario", "Catan", null), "/share-requests/1")));
    }

    private sealed record AdminDigestRun(
        Mock<IEmailService> Email, IReadOnlyList<string> ReviewQueueUrls, string? Failure);

    /// <summary>
    /// Runs the real job over one pending request and one admin. <c>Failure</c> is the <c>Error</c> of the job's
    /// result when it ended unsuccessfully: the job catches everything and reports through
    /// <c>IJobExecutionContext.Result</c>.
    /// </summary>
    private static async Task<AdminDigestRun> RunAdminDigestJobAsync(IConfiguration configuration)
    {
        var shareRequests = new Mock<IShareRequestRepository>();
        shareRequests
            .Setup(r => r.GetPendingStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingShareRequestStats
            {
                TotalPending = 3,
                CreatedToday = 1,
                OldestPendingAge = TimeSpan.FromDays(2),
            });

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.Users.Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.test",
            PasswordHash = "hash",
            Role = "admin",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var email = new Mock<IEmailService>();
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        context.SetupProperty(c => c.Result);

        var job = new AdminShareRequestDigestJob(
            shareRequests.Object,
            db,
            email.Object,
            PublicBaseUrlTestSupport.ProviderFor(configuration),
            NullLogger<AdminShareRequestDigestJob>.Instance);

        await job.Execute(context.Object);

        var urls = email.Invocations
            .Where(i => i.Method.Name == nameof(IEmailService.SendAdminShareRequestDigestEmailAsync))
            .Select(i => (string)i.Arguments[6])
            .ToList();

        // The result is an anonymous object: it carries Error (the message) when the job failed.
        var result = context.Object.Result;
        var failure = result?.GetType().GetProperty("Error")?.GetValue(result) as string;

        return new AdminDigestRun(email, urls, failure);
    }

    private static async Task<string> ProduceAdminDigestJobLinkAsync(IConfiguration configuration)
    {
        var run = await RunAdminDigestJobAsync(configuration);

        // The shared scenarios expect a generator with no address to fail: this one reports it in its result.
        if (run.Failure is not null)
        {
            throw new InvalidOperationException(run.Failure);
        }

        return run.ReviewQueueUrls.Single();
    }
}
