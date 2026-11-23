using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameContributors;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Infrastructure.Seeders.Core;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Host e database una volta per classe (#4050). Gli eventi di dominio partono in linea (<c>Hybrid</c>): nel default
/// <c>OutboxOnly</c> dell'host restano nella tabella dell'outbox, e nessun gestore girerebbe. Il servizio documenti è
/// un doppio che registra le copie: ogni test le conta sul proprio gioco di destinazione.
/// </summary>
public sealed class ShareRequestEventsHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "share_request_events")
{
    public Mock<IShareRequestDocumentService> Documents { get; } = CreateDocuments();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOptions<DomainEventOutboxOptions>>();
            services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
                Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.Hybrid }));

            services.RemoveAll<IShareRequestDocumentService>();
            services.AddSingleton(Documents.Object);
        }));

    private static Mock<IShareRequestDocumentService> CreateDocuments()
    {
        var documents = new Mock<IShareRequestDocumentService>();
        // As the real service when every copy succeeds: one new document id for each source. Since #5109 an approval
        // that gets fewer copies than documents is refused.
        documents
            .Setup(d => d.CopyDocumentsToSharedGame(
                It.IsAny<List<Guid>>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid> ids, Guid _, Guid _, CancellationToken _) => ids.Select(_ => Guid.NewGuid()).ToList());
        return documents;
    }
}

/// <summary>
/// #5078 — <c>ShareRequestRepository</c> non raccoglieva gli eventi di dominio, quindi nessun gestore degli eventi
/// <c>ShareRequest*Event</c> partiva: né le notifiche all'autore, né l'avviso agli admin, né la migrazione di una
/// proposta approvata. I test passano dal repository e dal salvataggio veri, come i comandi.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "5078")]
public sealed class ShareRequestDomainEventsIntegrationTests : IClassFixture<ShareRequestEventsHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IShareRequestDocumentService> _documents;

    public ShareRequestDomainEventsIntegrationTests(ShareRequestEventsHostFixture host)
    {
        _factory = host.Factory;
        _documents = host.Documents;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AShareRequestForASharedGame_NotifiesItsAuthor_AndTheAdmins()
    {
        var (userId, adminId) = await SeedUserAndAdminAsync();
        var title = $"Gioco 5078 {Guid.NewGuid():N}";
        Guid gameId;
        using (var scope = _factory.Services.CreateScope())
        {
            gameId = await TestSessionHelper.SeedSharedGameAsync(
                scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), title);
        }

        var requestId = await SaveNewAsync(ShareRequest.Create(userId, gameId, ContributionType.NewGame));

        var created = await NotificationsAsync(userId, "share_request_created");
        created.Should().ContainSingle().Which.Message.Should().Contain(title);
        var alerts = await NotificationsAsync(adminId, "admin_new_share_request");
        alerts.Should().ContainSingle().Which.Link.Should().Contain(requestId.ToString());
    }

    [Fact]
    public async Task ANewShareRequest_AlertsEveryoneWhoCanReviewIt()
    {
        // #5113: the alert went to Role == "admin" only; the review routes admit SuperAdmin and Admin.
        Guid userId, adminId, superAdminId, editorId, gameId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
            (adminId, _) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct);
            (superAdminId, _) = await TestSessionHelper.CreateSuperAdminSessionAsync(db, cancellationToken: Ct);
            (editorId, _) = await TestSessionHelper.CreateEditorSessionAsync(db, cancellationToken: Ct);
            gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"Gioco 5113 {Guid.NewGuid():N}");
        }

        await SaveNewAsync(ShareRequest.Create(userId, gameId, ContributionType.NewGame));

        (await NotificationsAsync(adminId, "admin_new_share_request")).Should().ContainSingle();
        (await NotificationsAsync(superAdminId, "admin_new_share_request")).Should().ContainSingle();
        (await NotificationsAsync(editorId, "admin_new_share_request")).Should().BeEmpty();
    }

    [Fact]
    public async Task AProposal_NotifiesItsAuthor_WithThePrivateGameTitle_AtEveryStepOfItsReview()
    {
        var (userId, adminId) = await SeedUserAndAdminAsync();
        var (privateGameId, title) = await SeedPrivateGameAsync(userId);

        var requestId = await SaveNewAsync(ShareRequest.CreateGameProposal(userId, privateGameId));
        await UpdateAsync(requestId, r => r.StartReview(adminId));
        await UpdateAsync(requestId, r => r.RequestChanges(adminId, "Serve la scheda del regolamento"));
        await UpdateAsync(requestId, r => r.StartReview(adminId));
        await UpdateAsync(requestId, r => r.Reject(adminId, "È già nel catalogo"));

        (await NotificationsAsync(adminId, "admin_new_share_request")).Should().ContainSingle();
        (await NotificationsAsync(userId, "share_request_created")).Should().ContainSingle()
            .Which.Message.Should().Contain(title);
        (await NotificationsAsync(userId, "game_proposal_in_review")).Should().HaveCount(2)
            .And.OnlyContain(n => n.Message.Contains(title));
        (await NotificationsAsync(userId, "share_request_changes_requested")).Should().ContainSingle()
            .Which.Message.Should().Contain(title);
        (await NotificationsAsync(userId, "share_request_rejected")).Should().ContainSingle()
            .Which.Message.Should().Contain(title);
    }

    [Fact]
    public async Task ApprovingAProposal_RecordsTheMigrationOfItsPrivateGame_NotifiesItsAuthor_AndCopiesItsDocumentsOnce()
    {
        var (userId, adminId) = await SeedUserAndAdminAsync();
        var (privateGameId, title) = await SeedPrivateGameAsync(userId);
        var proposal = ShareRequest.CreateGameProposal(userId, privateGameId);
        proposal.AttachDocument(Guid.NewGuid(), "regolamento-5078.pdf", "application/pdf", 2048);
        var requestId = await SaveNewAsync(proposal);
        await UpdateAsync(requestId, r => r.StartReview(adminId));

        var sharedGameId = await ApproveAsNewAsync(requestId, adminId);

        var migration = await MigrationsAsync(requestId);
        migration.Should().ContainSingle();
        migration[0].PrivateGameId.Should().Be(privateGameId);
        migration[0].SharedGameId.Should().Be(sharedGameId);
        (await NotificationsAsync(userId, "share_request_approved")).Should().ContainSingle()
            .Which.Message.Should().Contain(title);
        // ApproveGameProposal copies the documents itself; an event handler copying them as well doubled them.
        VerifyDocumentCopies(sharedGameId, Times.Once());
    }

    [Fact]
    public async Task AnApprovalDeliveredAgainByTheOutbox_ChangesNothing()
    {
        var (userId, adminId) = await SeedUserAndAdminAsync();
        await SeedBadgesAsync();
        var (privateGameId, _) = await SeedPrivateGameAsync(userId);
        var proposal = ShareRequest.CreateGameProposal(userId, privateGameId);
        proposal.AttachDocument(Guid.NewGuid(), "regolamento-5078.pdf", "application/pdf", 2048);
        var requestId = await SaveNewAsync(proposal);
        await UpdateAsync(requestId, r => r.StartReview(adminId));
        var sharedGameId = await ApproveAsNewAsync(requestId, adminId);
        // #5114: the user's first approved request; BadgeEvaluator could not read a seeded requirement before.
        (await BadgeCodesAsync(userId)).Should().Equal("FIRST_CONTRIBUTION");

        // Hybrid delivers the event inline and leaves its outbox row for the processor: deliver that row the way the
        // processor does (DomainEventOutboxProcessor), from its JSON.
        await DeliverOutboxRowAgainAsync<ShareRequestApprovedEvent>(e => e.ShareRequestId == requestId);

        (await MigrationsAsync(requestId)).Should().ContainSingle();
        (await NotificationsAsync(userId, "share_request_approved")).Should().ContainSingle();
        VerifyDocumentCopies(sharedGameId, Times.Once());
        (await BadgeCodesAsync(userId)).Should().Equal("FIRST_CONTRIBUTION");
    }

    [Fact]
    [Trait("Issue", "5109")]
    public async Task ApprovingANewGameWithADocument_CopiesItIntoItsSourceGame_Once_EvenIfTheOutboxDeliversAgain()
    {
        // #5109: approve-share-request copies the documents itself, into the game the request contributes to. For a
        // NewGame that is its source game, already a shared game. No event handler copies them a second time (#5078).
        var (userId, adminId) = await SeedUserAndAdminAsync();
        var gameId = await SeedSharedGameAsync();
        var request = ShareRequest.Create(userId, gameId, ContributionType.NewGame);
        request.AttachDocument(Guid.NewGuid(), "regolamento-5109.pdf", "application/pdf", 2048);
        var requestId = await SaveNewAsync(request);
        await UpdateAsync(requestId, r => r.StartReview(adminId));

        await ApproveShareRequestAsync(requestId, adminId, targetSharedGameId: null);

        VerifyDocumentCopies(gameId, Times.Once());
        await DeliverOutboxRowAgainAsync<ShareRequestApprovedEvent>(e => e.ShareRequestId == requestId);
        VerifyDocumentCopies(gameId, Times.Once());
    }

    [Fact]
    [Trait("Issue", "5109")]
    public async Task ApprovingWithATargetNamingAnotherGame_IsRefused_AndCopiesNothing()
    {
        var (userId, adminId) = await SeedUserAndAdminAsync();
        var gameId = await SeedSharedGameAsync();
        var otherGameId = await SeedSharedGameAsync();
        var request = ShareRequest.Create(userId, gameId, ContributionType.NewGame);
        request.AttachDocument(Guid.NewGuid(), "regolamento-5109.pdf", "application/pdf", 2048);
        var requestId = await SaveNewAsync(request);
        await UpdateAsync(requestId, r => r.StartReview(adminId));

        var act = () => ApproveShareRequestAsync(requestId, adminId, targetSharedGameId: otherGameId);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*does not match*");
        VerifyDocumentCopies(gameId, Times.Never());
        VerifyDocumentCopies(otherGameId, Times.Never());
        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IShareRequestRepository>().GetByIdAsync(requestId, Ct);
        stored!.Status.Should().Be(ShareRequestStatus.InReview);
    }

    // #5139, decision of 2026-11-23: no production path wrote contributors and contribution_records, so the badge
    // leaderboard and the contributors of a game were always empty. The approval records them now, in its own save.
    [Fact]
    [Trait("Issue", "5139")]
    public async Task ApprovingANewGame_MakesItsAuthorThePrimaryContributor_WithTheInitialSubmissionOfTheCopies()
    {
        var (userId, adminId) = await SeedUserAndAdminAsync();
        var gameId = await SeedSharedGameAsync();
        var original = Guid.NewGuid();
        var copy = Guid.NewGuid();
        _documents
            .Setup(d => d.CopyDocumentsToSharedGame(
                It.IsAny<List<Guid>>(), gameId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { copy });
        var request = ShareRequest.Create(userId, gameId, ContributionType.NewGame);
        request.AttachDocument(original, "regolamento-5139.pdf", "application/pdf", 2048);
        var requestId = await SaveNewAsync(request);
        await UpdateAsync(requestId, r => r.StartReview(adminId));
        // The game page read before the approval: it caches an empty list for two hours, which the approval must evict
        // (review of #5142).
        (await ContributorsOnThePageAsync(gameId)).Should().BeEmpty();

        await ApproveShareRequestAsync(requestId, adminId, targetSharedGameId: null);

        var contributor = (await ContributorsAsync(gameId)).Should().ContainSingle().Subject;
        contributor.UserId.Should().Be(userId);
        contributor.IsPrimaryContributor.Should().BeTrue();
        var record = contributor.Contributions.Should().ContainSingle().Subject;
        record.Type.Should().Be((int)ContributionRecordType.InitialSubmission);
        record.Version.Should().Be(1);
        record.ShareRequestId.Should().Be(requestId);
        JsonSerializer.Deserialize<List<Guid>>(record.DocumentIdsJson!).Should()
            .Equal([copy], "the record names the copy in the shared game, not the request's original {0}", original);

        // What the two readers that were always empty now see.
        (await ContributorsOnThePageAsync(gameId)).Should().ContainSingle()
            .Which.Should().Match<GameContributorDto>(c => c.UserId == userId && c.IsPrimaryContributor && c.ContributionCount == 1);
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new GetBadgeLeaderboardQuery(LeaderboardPeriod.AllTime, 1, 100), Ct))
            .Should().ContainSingle(e => e.UserId == userId).Which.ContributionCount.Should().Be(1);
    }

    [Fact]
    [Trait("Issue", "5139")]
    public async Task AnotherApprovalOfTheAuthorOnTheGame_AddsTheNextVersion_ToTheSameContributor()
    {
        var (userId, adminId) = await SeedUserAndAdminAsync();
        var gameId = await SeedSharedGameAsync();
        var first = await SaveNewAsync(ShareRequest.Create(userId, gameId, ContributionType.NewGame));
        await UpdateAsync(first, r => r.StartReview(adminId));
        await ApproveShareRequestAsync(first, adminId, targetSharedGameId: null);
        var more = ShareRequest.Create(userId, gameId, ContributionType.AdditionalContent, targetSharedGameId: gameId);
        more.AttachDocument(Guid.NewGuid(), "espansione-5139.pdf", "application/pdf", 2048);
        var second = await SaveNewAsync(more);
        await UpdateAsync(second, r => r.StartReview(adminId));

        await ApproveShareRequestAsync(second, adminId, targetSharedGameId: null);

        var contributor = (await ContributorsAsync(gameId)).Should().ContainSingle().Subject;
        contributor.ModifiedAt.Should().NotBeNull();
        contributor.Contributions.OrderBy(c => c.Version)
            .Select(c => (c.Version, c.Type, c.ShareRequestId))
            .Should().Equal(
                (1, (int)ContributionRecordType.InitialSubmission, (Guid?)first),
                (2, (int)ContributionRecordType.DocumentAddition, (Guid?)second));
    }

    [Fact]
    [Trait("Issue", "5139")]
    public async Task ABulkApprovalOfTwoRequestsOfOneAuthorOnOneGame_GivesOneContributorAndTwoVersions()
    {
        // One contributor per user and game (ix_contributors_user_shared_game_unique): two in one save would be a 23505,
        // and the batch is all-or-nothing.
        var (userId, adminId) = await SeedUserAndAdminAsync();
        var gameId = await SeedSharedGameAsync();
        var ids = new List<Guid>();
        foreach (var file in new[] { "a-5139.pdf", "b-5139.pdf" })
        {
            var request = ShareRequest.Create(userId, gameId, ContributionType.AdditionalContent, targetSharedGameId: gameId);
            request.AttachDocument(Guid.NewGuid(), file, "application/pdf", 2048);
            var id = await SaveNewAsync(request);
            await UpdateAsync(id, r => r.StartReview(adminId));
            ids.Add(id);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new BulkApproveShareRequestsCommand(ids, adminId), Ct);
            result.SuccessCount.Should().Be(2, string.Join("; ", result.Errors));
        }

        var contributor = (await ContributorsAsync(gameId)).Should().ContainSingle().Subject;
        contributor.IsPrimaryContributor.Should().BeFalse("additional content does not make anyone the game's author");
        contributor.Contributions.Select(c => c.Version).Should().BeEquivalentTo([1, 2]);
        contributor.Contributions.Select(c => c.ShareRequestId).Should().BeEquivalentTo(ids.Select(id => (Guid?)id));
    }

    [Fact]
    [Trait("Issue", "5139")]
    public async Task ApprovingAProposalAsNew_MakesItsAuthorThePrimaryContributorOfTheNewGame()
    {
        var (userId, adminId) = await SeedUserAndAdminAsync();
        var (privateGameId, _) = await SeedPrivateGameAsync(userId);
        var requestId = await SaveNewAsync(ShareRequest.CreateGameProposal(userId, privateGameId));
        await UpdateAsync(requestId, r => r.StartReview(adminId));

        var sharedGameId = await ApproveAsNewAsync(requestId, adminId);

        var contributor = (await ContributorsAsync(sharedGameId)).Should().ContainSingle().Subject;
        contributor.UserId.Should().Be(userId);
        contributor.IsPrimaryContributor.Should().BeTrue();
        contributor.Contributions.Should().ContainSingle()
            .Which.Type.Should().Be((int)ContributionRecordType.InitialSubmission);
    }

    [Fact]
    [Trait("Issue", "5139")]
    public async Task ANewGameOnAGameThatHasItsAuthorAlready_DoesNotMakeASecondPrimary()
    {
        var (authorId, adminId) = await SeedUserAndAdminAsync();
        var (otherId, _) = await SeedUserAndAdminAsync();
        var gameId = await SeedSharedGameAsync();
        foreach (var userId in new[] { authorId, otherId })
        {
            var id = await SaveNewAsync(ShareRequest.Create(userId, gameId, ContributionType.NewGame));
            await UpdateAsync(id, r => r.StartReview(adminId));
            await ApproveShareRequestAsync(id, adminId, targetSharedGameId: null);
        }

        var contributors = await ContributorsAsync(gameId);
        contributors.Should().HaveCount(2);
        contributors.Single(c => c.IsPrimaryContributor).UserId.Should().Be(authorId);
        contributors.Single(c => c.UserId == otherId).Contributions.Should().ContainSingle()
            .Which.Type.Should().Be((int)ContributionRecordType.ContentEnhancement);
    }

    // #5143: the two races #5142 left open, closed by unique indexes. Each is recreated with two units of work that both
    // read before either saves, as two approvals at the same time do: the first saves, the second is a 409.
    [Fact]
    [Trait("Issue", "5143")]
    public async Task TwoAuthorsNewGames_ApprovedAtTheSameTime_GiveOnePrimary_AndTheSecondIsAConflict()
    {
        var gameId = await SeedSharedGameAsync();
        var (first, _) = await SeedUserAndAdminAsync();
        var (second, _) = await SeedUserAndAdminAsync();
        using var a = _factory.Services.CreateScope();
        using var b = _factory.Services.CreateScope();
        var approvalA = Contributions(a);
        var approvalB = Contributions(b);
        await approvalA.RecordAsync(ShareRequest.Create(first, gameId, ContributionType.NewGame), gameId,
            ShareRequestApprovalContributions.Kind.NewGame, [], Ct);
        await approvalB.RecordAsync(ShareRequest.Create(second, gameId, ContributionType.NewGame), gameId,
            ShareRequestApprovalContributions.Kind.NewGame, [], Ct);

        await SaveAsync(approvalA, a);
        var act = () => SaveAsync(approvalB, b);

        await act.Should().ThrowAsync<ConflictException>();
        (await ContributorsAsync(gameId)).Should().ContainSingle(c => c.IsPrimaryContributor)
            .Which.UserId.Should().Be(first);
    }

    [Fact]
    [Trait("Issue", "5143")]
    public async Task TwoApprovals_AddingToOneContributorAtTheSameTime_WriteOneNextVersion_AndTheSecondIsAConflict()
    {
        var gameId = await SeedSharedGameAsync();
        var (userId, _) = await SeedUserAndAdminAsync();
        using (var seed = _factory.Services.CreateScope())
        {
            var first = Contributions(seed);
            await first.RecordAsync(ShareRequest.Create(userId, gameId, ContributionType.NewGame), gameId,
                ShareRequestApprovalContributions.Kind.Content, [], Ct);
            await SaveAsync(first, seed);
        }

        using var a = _factory.Services.CreateScope();
        using var b = _factory.Services.CreateScope();
        var approvalA = Contributions(a);
        var approvalB = Contributions(b);
        foreach (var approval in new[] { approvalA, approvalB })
        {
            await approval.RecordAsync(ShareRequest.Create(userId, gameId, ContributionType.NewGame), gameId,
                ShareRequestApprovalContributions.Kind.Content, [], Ct);
        }

        await SaveAsync(approvalA, a);
        var act = () => SaveAsync(approvalB, b);

        await act.Should().ThrowAsync<ConflictException>();
        (await ContributorsAsync(gameId)).Should().ContainSingle()
            .Which.Contributions.Select(c => c.Version).Should().BeEquivalentTo([1, 2]);
    }

    private static ShareRequestApprovalContributions Contributions(IServiceScope scope) =>
        new(scope.ServiceProvider.GetRequiredService<IContributorRepository>());

    private Task SaveAsync(ShareRequestApprovalContributions contributions, IServiceScope scope) =>
        contributions.SaveWithTheApprovalAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
            scope.ServiceProvider.GetRequiredService<ISharedGameCacheInvalidator>(),
            Ct);

    // The query behind the game page, cache included.
    private async Task<List<GameContributorDto>> ContributorsOnThePageAsync(Guid sharedGameId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new GetGameContributorsQuery(sharedGameId), Ct);
    }

    private async Task<List<ContributorEntity>> ContributorsAsync(Guid sharedGameId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Set<ContributorEntity>()
            .AsNoTracking()
            .Include(c => c.Contributions)
            .Where(c => c.SharedGameId == sharedGameId)
            .ToListAsync(Ct);
    }

    private async Task<Guid> SeedSharedGameAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await TestSessionHelper.SeedSharedGameAsync(
            scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), $"Gioco 5109 {Guid.NewGuid():N}");
    }

    private async Task ApproveShareRequestAsync(Guid requestId, Guid adminId, Guid? targetSharedGameId)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new ApproveShareRequestCommand(requestId, adminId, targetSharedGameId, "Approvata"), Ct);
    }

    private async Task<(Guid UserId, Guid AdminId)> SeedUserAndAdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        var (adminId, _) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct);
        return (userId, adminId);
    }

    private async Task SeedBadgesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        // Idempotent: the badges BadgeEvaluationOnApprovalHandler awards, as production seeds them.
        await BadgeSeeder.SeedBadgesAsync(
            scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), NullLogger.Instance, Ct);
    }

    private async Task<List<string>> BadgeCodesAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.UserBadges.AsNoTracking()
            .Where(ub => ub.UserId == userId)
            .Join(db.Badges, ub => ub.BadgeId, b => b.Id, (_, b) => b.Code)
            .ToListAsync(Ct);
    }

    private async Task<(Guid Id, string Title)> SeedPrivateGameAsync(Guid ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var game = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = $"Gioco privato 5078 {Guid.NewGuid():N}",
            Description = "Proposto al catalogo",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            Source = PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow,
        };
        db.PrivateGames.Add(game);
        await db.SaveChangesAsync(Ct);
        return (game.Id, game.Title);
    }

    // The command handlers' way: AddAsync, then IUnitOfWork.SaveChangesAsync.
    private async Task<Guid> SaveNewAsync(ShareRequest request)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IShareRequestRepository>().AddAsync(request, Ct);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return request.Id;
    }

    // GetByIdForUpdateAsync, the domain method, Update, then IUnitOfWork.SaveChangesAsync.
    private async Task UpdateAsync(Guid requestId, Action<ShareRequest> change)
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IShareRequestRepository>();
        var request = await repository.GetByIdForUpdateAsync(requestId, Ct);
        change(request!);
        repository.Update(request!);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
    }

    private async Task<Guid> ApproveAsNewAsync(Guid requestId, Guid adminId)
    {
        using var scope = _factory.Services.CreateScope();
        var response = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new ApproveGameProposalCommand(requestId, adminId, ProposalApprovalAction.ApproveAsNew, null, "Approvata"),
            Ct);
        return response.TargetSharedGameId!.Value;
    }

    private async Task DeliverOutboxRowAgainAsync<TEvent>(Func<TEvent, bool> match)
        where TEvent : class, IDomainEvent
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var resolver = new DomainEventTypeResolver();
        var rows = await db.DomainEventOutbox.AsNoTracking().ToListAsync(Ct);
        var redelivered = rows
            .Where(row => resolver.Resolve(row.EventType) == typeof(TEvent))
            .Select(row => (TEvent)JsonSerializer.Deserialize(row.PayloadJson, typeof(TEvent), DomainEventJsonOptions.Default)!)
            .Should().ContainSingle(e => match(e)).Subject;

        await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(redelivered, Ct);
    }

    private async Task<List<NotificationEntity>> NotificationsAsync(Guid userId, string type)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.Type == type)
            .ToListAsync(Ct);
    }

    private async Task<List<ProposalMigrationEntity>> MigrationsAsync(Guid requestId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().ProposalMigrations.AsNoTracking()
            .Where(m => m.ShareRequestId == requestId)
            .ToListAsync(Ct);
    }

    private void VerifyDocumentCopies(Guid sharedGameId, Times times) =>
        _documents.Verify(d => d.CopyDocumentsToSharedGame(
            It.IsAny<List<Guid>>(), sharedGameId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), times);
}
