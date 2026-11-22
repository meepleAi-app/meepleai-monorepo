using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;
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
        documents
            .Setup(d => d.CopyDocumentsToSharedGame(
                It.IsAny<List<Guid>>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
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
        var (privateGameId, _) = await SeedPrivateGameAsync(userId);
        var proposal = ShareRequest.CreateGameProposal(userId, privateGameId);
        proposal.AttachDocument(Guid.NewGuid(), "regolamento-5078.pdf", "application/pdf", 2048);
        var requestId = await SaveNewAsync(proposal);
        await UpdateAsync(requestId, r => r.StartReview(adminId));
        var sharedGameId = await ApproveAsNewAsync(requestId, adminId);

        // Hybrid delivers the event inline and leaves its outbox row for the processor: deliver that row the way the
        // processor does (DomainEventOutboxProcessor), from its JSON.
        await DeliverOutboxRowAgainAsync<ShareRequestApprovedEvent>(e => e.ShareRequestId == requestId);

        (await MigrationsAsync(requestId)).Should().ContainSingle();
        (await NotificationsAsync(userId, "share_request_approved")).Should().ContainSingle();
        VerifyDocumentCopies(sharedGameId, Times.Once());
        // No badge count: BadgeEvaluator cannot read a seeded badge's requirement (#5114), so no badge is awarded and
        // a count would stay equal for nothing.
    }

    private async Task<(Guid UserId, Guid AdminId)> SeedUserAndAdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        var (adminId, _) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct);
        return (userId, adminId);
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
