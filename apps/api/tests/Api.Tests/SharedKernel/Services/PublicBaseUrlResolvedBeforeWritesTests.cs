using Api.BoundedContexts.Authentication.Application.Commands.CreateShareLink;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using IUserRepository = Api.BoundedContexts.Authentication.Infrastructure.Persistence.IUserRepository;

namespace Api.Tests.SharedKernel.Services;

/// <summary>
/// #4774: a handler that writes a link resolves the public front-end URL <b>before</b> it writes anything.
/// In a deployed environment with no key the resolution throws; if it ran after the write, the exception
/// would reach a caller whose data had already changed: a link revoked, a new one stored, an invitation saved
/// that no e-mail announces. Each test is a deployed host with nothing configured, and asserts both the
/// exception (it names the key) and that no write reached the repository or the unit of work.
/// </summary>
/// <remarks>
/// The refusals that never needed the address (not the organizer, not the owner, a pending invitation already
/// there, a quota reached) keep their own answer: they are asserted too, so moving the resolution earlier
/// cannot turn a 403/404/409 into the configuration error.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
public sealed class PublicBaseUrlResolvedBeforeWritesTests
{
    private const string NamesTheKey = "*Frontend:BaseUrl*";

    /// <summary>A deployed host (the default of the support class) with no front-end key at all.</summary>
    private static PublicBaseUrlProvider NoAddressConfigured() =>
        PublicBaseUrlTestSupport.ProviderFor(new ConfigurationBuilder().Build());

    // ------------------------------------------------------------------------------------------------------
    // CreateGameNightInvitationByEmailCommandHandler
    // ------------------------------------------------------------------------------------------------------

    private sealed class InvitationFixture
    {
        public Guid OrganizerId { get; } = Guid.NewGuid();

        public GameNightEvent GameNight { get; }

        public Mock<IGameNightInvitationRepository> Invitations { get; } = new();

        public Mock<IGameNightEmailService> Email { get; } = new();

        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public CreateGameNightInvitationByEmailCommandHandler Handler { get; }

        public InvitationFixture()
        {
            GameNight = GameNightEvent.Create(
                organizerId: OrganizerId,
                title: "Friday Night Catan",
                scheduledAt: DateTimeOffset.UtcNow.AddDays(7),
                description: null,
                location: "My place",
                maxPlayers: 5,
                gameIds: null);

            var gameNights = new Mock<IGameNightEventRepository>();
            gameNights
                .Setup(r => r.GetByIdAsync(GameNight.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(GameNight);

            Handler = new CreateGameNightInvitationByEmailCommandHandler(
                Invitations.Object,
                gameNights.Object,
                new Mock<IUserRepository>().Object,
                new Mock<IGameCoreDataProvider>().Object,
                Email.Object,
                UnitOfWork.Object,
                new ConfigurationBuilder().Build(),
                NoAddressConfigured(),
                TimeProvider.System);
        }

        public void GivenAPendingInvitationFor(string email)
        {
            var pending = GameNightInvitation.Create(
                gameNightId: GameNight.Id,
                email: email,
                expiresAt: DateTimeOffset.UtcNow.AddDays(14),
                createdBy: OrganizerId,
                utcNow: DateTimeOffset.UtcNow);

            Invitations
                .Setup(r => r.ExistsPendingByEmailAsync(GameNight.Id, email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            Invitations
                .Setup(r => r.GetByGameNightIdAsync(GameNight.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GameNightInvitation> { pending });
        }

        public void VerifyNothingWasWrittenAndNoEmailWasSent()
        {
            Invitations.Verify(
                r => r.AddAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
            Invitations.Verify(
                r => r.UpdateAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
            UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            Email.Invocations.Should().BeEmpty("no e-mail may go out for an invitation whose link cannot be written");
        }
    }

    [Fact]
    public async Task GameNightInvitation_NewInvitation_WithNoAddress_SavesNothingAndSendsNothing()
    {
        var fixture = new InvitationFixture();

        var act = async () => await fixture.Handler.Handle(
            new CreateGameNightInvitationByEmailCommand(fixture.GameNight.Id, "guest@example.com", fixture.OrganizerId),
            CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage(NamesTheKey);
        fixture.VerifyNothingWasWrittenAndNoEmailWasSent();
    }

    [Fact]
    public async Task GameNightInvitation_ResendOfAPendingOne_WithNoAddress_SendsNothing()
    {
        var fixture = new InvitationFixture();
        fixture.GivenAPendingInvitationFor("guest@example.com");

        var act = async () => await fixture.Handler.Handle(
            new CreateGameNightInvitationByEmailCommand(
                fixture.GameNight.Id, "guest@example.com", fixture.OrganizerId, ResendIfPending: true),
            CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage(NamesTheKey);
        fixture.VerifyNothingWasWrittenAndNoEmailWasSent();
    }

    [Fact]
    public async Task GameNightInvitation_APendingOneWithoutResend_StillAnswersConflict_WithNoAddress()
    {
        var fixture = new InvitationFixture();
        fixture.GivenAPendingInvitationFor("guest@example.com");

        var act = async () => await fixture.Handler.Handle(
            new CreateGameNightInvitationByEmailCommand(fixture.GameNight.Id, "guest@example.com", fixture.OrganizerId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task GameNightInvitation_ACallerWhoIsNotTheOrganizer_StillAnswersUnauthorized_WithNoAddress()
    {
        var fixture = new InvitationFixture();

        var act = async () => await fixture.Handler.Handle(
            new CreateGameNightInvitationByEmailCommand(fixture.GameNight.Id, "guest@example.com", Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ------------------------------------------------------------------------------------------------------
    // CreateLibraryShareLinkCommandHandler
    // ------------------------------------------------------------------------------------------------------

    private static (CreateLibraryShareLinkCommandHandler Handler, Mock<ILibraryShareLinkRepository> Repo,
        Mock<IUnitOfWork> UnitOfWork, LibraryShareLink Active, Guid UserId) CreateLibraryShareFixture(int recentCount = 0)
    {
        var userId = Guid.NewGuid();
        var active = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, true, null);
        var repo = new Mock<ILibraryShareLinkRepository>();
        repo.Setup(r => r.CountRecentByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(recentCount);
        repo.Setup(r => r.GetActiveByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(active);
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CreateLibraryShareLinkCommandHandler(
            repo.Object,
            unitOfWork.Object,
            NoAddressConfigured(),
            NullLogger<CreateLibraryShareLinkCommandHandler>.Instance);

        return (handler, repo, unitOfWork, active, userId);
    }

    [Fact]
    public async Task LibraryShareCreate_WithNoAddress_KeepsTheWorkingLinkAndStoresNoNewOne()
    {
        var (handler, repo, unitOfWork, active, userId) = CreateLibraryShareFixture();

        var act = async () => await handler.Handle(
            new CreateLibraryShareLinkCommand(userId, "public", true, null), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage(NamesTheKey);
        active.RevokedAt.Should().BeNull("the link the person already shares must survive a failed creation");
        repo.Verify(r => r.UpdateAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.AddAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LibraryShareCreate_AQuotaReached_StillAnswersDomainException_WithNoAddress()
    {
        var (handler, _, _, _, userId) = CreateLibraryShareFixture(recentCount: 10);

        var act = async () => await handler.Handle(
            new CreateLibraryShareLinkCommand(userId, "public", true, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ------------------------------------------------------------------------------------------------------
    // UpdateLibraryShareLinkCommandHandler
    // ------------------------------------------------------------------------------------------------------

    private static (UpdateLibraryShareLinkCommandHandler Handler, Mock<ILibraryShareLinkRepository> Repo,
        Mock<IUnitOfWork> UnitOfWork, LibraryShareLink Link, Guid UserId) UpdateLibraryShareFixture()
    {
        var userId = Guid.NewGuid();
        var link = LibraryShareLink.Create(userId, LibrarySharePrivacyLevel.Public, true, null);
        var repo = new Mock<ILibraryShareLinkRepository>();
        repo.Setup(r => r.GetByShareTokenAsync(link.ShareToken, It.IsAny<CancellationToken>())).ReturnsAsync(link);
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new UpdateLibraryShareLinkCommandHandler(
            repo.Object,
            unitOfWork.Object,
            NoAddressConfigured(),
            NullLogger<UpdateLibraryShareLinkCommandHandler>.Instance);

        return (handler, repo, unitOfWork, link, userId);
    }

    [Fact]
    public async Task LibraryShareUpdate_WithNoAddress_ChangesAndSavesNothing()
    {
        var (handler, repo, unitOfWork, link, userId) = UpdateLibraryShareFixture();

        var act = async () => await handler.Handle(
            new UpdateLibraryShareLinkCommand(userId, link.ShareToken, "unlisted", null, null),
            CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage(NamesTheKey);
        link.PrivacyLevel.Should().Be(LibrarySharePrivacyLevel.Public, "the new settings must not be applied");
        repo.Verify(r => r.UpdateAsync(It.IsAny<LibraryShareLink>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LibraryShareUpdate_ACallerWhoDoesNotOwnTheLink_StillAnswersNotFound_WithNoAddress()
    {
        var (handler, _, _, link, _) = UpdateLibraryShareFixture();

        var act = async () => await handler.Handle(
            new UpdateLibraryShareLinkCommand(Guid.NewGuid(), link.ShareToken, "unlisted", null, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ------------------------------------------------------------------------------------------------------
    // CreateShareLinkCommandHandler (chat thread)
    // ------------------------------------------------------------------------------------------------------

    private static async Task<(CreateShareLinkCommandHandler Handler, Mock<IShareLinkRepository> Repo, Guid ThreadId, Guid UserId)>
        CreateChatShareFixtureAsync(Api.Infrastructure.MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        var thread = new ChatThreadEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Share me",
            LastMessageAt = DateTime.UtcNow,
        };
        db.ChatThreads.Add(thread);
        await db.SaveChangesAsync(CancellationToken.None);

        // The signing key is there: the only thing missing is the address, so that is what must be reported.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ShareLinks:SecretKey"] = "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256",
            })
            .Build();
        var repo = new Mock<IShareLinkRepository>();

        var handler = new CreateShareLinkCommandHandler(db, repo.Object, configuration, NoAddressConfigured());

        return (handler, repo, thread.Id, userId);
    }

    [Fact]
    public async Task ChatShare_WithNoAddress_StoresNoShareLink()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (handler, repo, threadId, userId) = await CreateChatShareFixtureAsync(db);

        var act = async () => await handler.Handle(
            new CreateShareLinkCommand(threadId, ShareLinkRole.View, DateTime.UtcNow.AddDays(1), null, userId),
            CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage(NamesTheKey);
        repo.Verify(r => r.AddAsync(It.IsAny<ShareLink>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChatShare_AThreadOfSomeoneElse_StillAnswersNotFound_WithNoAddress()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var (handler, _, threadId, _) = await CreateChatShareFixtureAsync(db);

        var act = async () => await handler.Handle(
            new CreateShareLinkCommand(threadId, ShareLinkRole.View, DateTime.UtcNow.AddDays(1), null, Guid.NewGuid()),
            CancellationToken.None);

        // Both refusals are an InvalidOperationException: the message tells the two apart.
        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*not found or does not belong*");
    }
}
