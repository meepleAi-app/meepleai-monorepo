using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.UserLibrary;

/// <summary>
/// #4155: il dettaglio di un gioco in libreria dice se l'utente può interrogarne il manuale, così la
/// pagina offre la dichiarazione di possesso solo a chi ne ha bisogno. Prima il campo non esisteva
/// nel DTO e il frontend lo leggeva sempre come <c>false</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4155")]
public sealed class GetGameDetailQueryHandlerRagAccessTests
{
    private static GetGameDetailQueryHandler CreateHandler(
        MeepleAiDbContext db,
        SharedGame game,
        UserLibraryEntry entry,
        Guid userId,
        IRagAccessService ragAccess)
    {
        var libraryRepo = new Mock<IUserLibraryRepository>();
        libraryRepo.Setup(r => r.GetUserGameWithStatsAsync(userId, game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        var sharedGameRepo = new Mock<ISharedGameRepository>();
        sharedGameRepo.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var labelRepo = new Mock<IGameLabelRepository>();
        labelRepo.Setup(r => r.GetLabelsForEntryAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GameLabel>());

        var chatThreadRepo = new Mock<IChatThreadRepository>();
        chatThreadRepo.Setup(r => r.FindByUserIdAndGameIdAsync(userId, game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread>());

        HybridCache cache = TestDbContextFactory.CreateInMemoryHybridCache();
        return new GetGameDetailQueryHandler(
            libraryRepo.Object,
            sharedGameRepo.Object,
            labelRepo.Object,
            chatThreadRepo.Object,
            ragAccess,
            db,
            Mock.Of<IBlobStorageService>(),
            cache,
            NullLogger<GetGameDetailQueryHandler>.Instance);
    }

    private static (SharedGame game, UserLibraryEntry entry) BuildCatan(Guid userId)
    {
        var game = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Trade, build, and settle the island of Catan.",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.3m,
            averageRating: 7.1m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            rules: null,
            createdBy: userId,
            bggId: 13);
        return (game, new UserLibraryEntry(Guid.NewGuid(), userId, game.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_HasRagAccess_FollowsTheRagGate(bool canAccess)
    {
        var userId = Guid.NewGuid();
        var (game, entry) = BuildCatan(userId);
        var gate = new Mock<IRagAccessService>();
        gate.Setup(g => g.CanAccessRagAsync(userId, game.Id, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canAccess);
        var handler = CreateHandler(TestDbContextFactory.CreateInMemoryDbContext(), game, entry, userId, gate.Object);

        var detail = await handler.Handle(new GetGameDetailQuery(userId, game.Id), CancellationToken.None);

        detail.HasRagAccess.Should().Be(canAccess);
    }

    [Fact]
    public async Task Handle_HasRagAccess_IsReadFresh_EvenWhenTheDetailIsCached()
    {
        // Il dettaglio resta in cache 5 minuti. Dopo una dichiarazione di possesso (o un admin che
        // rende pubblico il RAG del gioco) la pagina deve smettere subito di offrire la dichiarazione.
        var userId = Guid.NewGuid();
        var (game, entry) = BuildCatan(userId);
        var gate = new Mock<IRagAccessService>();
        gate.SetupSequence(g => g.CanAccessRagAsync(userId, game.Id, It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        var handler = CreateHandler(TestDbContextFactory.CreateInMemoryDbContext(), game, entry, userId, gate.Object);

        var before = await handler.Handle(new GetGameDetailQuery(userId, game.Id), CancellationToken.None);
        var after = await handler.Handle(new GetGameDetailQuery(userId, game.Id), CancellationToken.None);

        before.HasRagAccess.Should().BeFalse();
        after.HasRagAccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AsksTheGateWithTheStoredRole()
    {
        // La query non porta il ruolo: lo si legge dall'utente, come fa la lista della libreria.
        // Un admin ha accesso per la regola 1 e non deve vedersi offrire la dichiarazione.
        var userId = Guid.NewGuid();
        var (game, entry) = BuildCatan(userId);
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "admin-4155@test.invalid",
            DisplayName = "Admin",
            PasswordHash = "placeholder",
            Role = "admin",
            Tier = "free",
            Status = "Active",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var gate = new Mock<IRagAccessService>();
        var handler = CreateHandler(db, game, entry, userId, gate.Object);

        await handler.Handle(new GetGameDetailQuery(userId, game.Id), CancellationToken.None);

        gate.Verify(g => g.CanAccessRagAsync(userId, game.Id, UserRole.Admin, It.IsAny<CancellationToken>()), Times.Once);
    }
}
