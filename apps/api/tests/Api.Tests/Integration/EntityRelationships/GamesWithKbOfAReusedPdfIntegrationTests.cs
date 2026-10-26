using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.EntityRelationships;

/// <summary>
/// #4641: a PDF that two users upload is one file and two links, both from the same catalog game. The list of the
/// games with a knowledge base (<c>GET /users/{userId}/games/with-kb</c>) joins the library of whoever asks to the links
/// of the game, <b>of anybody</b>, and then to the PDFs: with two links to one PDF it listed the PDF twice, to both users.
/// </summary>
/// <remarks>
/// Decision (2026-10-26): the PDFs a user can use are those of the games they declared to own, which is the rule the chat
/// already applies (<c>CanAccessRagAsync</c>: admin, public RAG, or declared ownership). The list is the chat selection
/// screen, so it follows the same rule: all the PDFs of the game, or none.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4641")]
public sealed class GamesWithKbOfAReusedPdfIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _db = null!;
    private ServiceProvider _serviceProvider = null!;

    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _pdfId = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GamesWithKbOfAReusedPdfIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_withkb_{Guid.NewGuid():N}";
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _serviceProvider = IntegrationServiceCollectionBuilder.CreateBase(conn).BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _db.Database.MigrateAsync(Ct);

        foreach (var (id, mail) in new[] { (_userA, "a-withkb@meepleai.dev"), (_userB, "b-withkb@meepleai.dev") })
        {
            _db.Users.Add(new UserEntity
            {
                Id = id,
                Email = mail,
                DisplayName = mail,
                Role = "User",
                Tier = "Free",
                CreatedAt = DateTime.UtcNow,
            });
            _db.UserLibraryEntries.Add(new UserLibraryEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = id,
                SharedGameId = _gameId,
                OwnershipDeclaredAt = DateTime.UtcNow,
            });
        }

        _db.SharedGames.Add(new SharedGameEntity
        {
            Id = _gameId,
            Title = "A reused rulebook",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow,
        });
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = _pdfId,
            SharedGameId = _gameId,
            UploadedByUserId = _userA,
            FileName = "rules.pdf",
            FilePath = "/test/rules.pdf",
            FileSizeBytes = 1024,
            UploadedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow,
            ContentHash = "hash-4641",
            ProcessingState = nameof(PdfProcessingState.Ready),
        });
        await _db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _serviceProvider.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); } catch { /* ignore */ }
        }
    }

    private async Task LinkBothUsersAsync()
    {
        foreach (var owner in new[] { _userA, _userB })
        {
            _db.EntityLinks.Add(EntityLink.Create(
                MeepleEntityType.Game, _gameId, MeepleEntityType.KbCard, _pdfId,
                EntityLinkType.RelatedTo, EntityLinkScope.User, owner));
        }

        await _db.SaveChangesAsync(Ct);
        _db.ChangeTracker.Clear();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ThePdf_IsListedOnce_ForEachOfTheTwoUsers(bool askAsTheUploader)
    {
        await LinkBothUsersAsync();
        var handler = new GetGamesWithKbQueryHandler(_db);

        var games = await handler.Handle(new GetGamesWithKbQuery(askAsTheUploader ? _userA : _userB), Ct);

        var game = games.Should().ContainSingle().Subject;
        game.GameId.Should().Be(_gameId);
        game.Rulebooks.Should().ContainSingle("one file is one rulebook, however many links point at it")
            .Which.PdfDocumentId.Should().Be(_pdfId);
    }

    /// <summary>The control: with the link of the uploader only, the list was already one rulebook.</summary>
    [Fact]
    public async Task WithOneLink_ThePdfIsListedOnce()
    {
        _db.EntityLinks.Add(EntityLink.Create(
            MeepleEntityType.Game, _gameId, MeepleEntityType.KbCard, _pdfId,
            EntityLinkType.RelatedTo, EntityLinkScope.User, _userA));
        await _db.SaveChangesAsync(Ct);
        _db.ChangeTracker.Clear();

        var games = await new GetGamesWithKbQueryHandler(_db).Handle(new GetGamesWithKbQuery(_userB), Ct);

        games.Should().ContainSingle().Which.Rulebooks.Should().ContainSingle();
    }

    /// <summary>The user has the game in the library and never declared to own it: the chat refuses it, so the list does not offer it.</summary>
    [Fact]
    public async Task AGameWhoseOwnershipIsNotDeclared_IsNotOffered()
    {
        await LinkBothUsersAsync();
        await _db.UserLibraryEntries
            .Where(e => e.UserId == _userB)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.OwnershipDeclaredAt, (DateTime?)null), Ct);
        var handler = new GetGamesWithKbQueryHandler(_db);

        var asB = await handler.Handle(new GetGamesWithKbQuery(_userB), Ct);
        var asA = await handler.Handle(new GetGamesWithKbQuery(_userA), Ct);

        asB.Should().BeEmpty("B never declared to own the game");
        asA.Should().ContainSingle("A did, and the rule is the same for both");
    }

    /// <summary>A game whose RAG is public is usable by everybody, declared ownership or not.</summary>
    [Fact]
    public async Task AGameWithPublicRag_IsOffered_WithoutADeclaration()
    {
        await LinkBothUsersAsync();
        await _db.UserLibraryEntries
            .Where(e => e.UserId == _userB)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.OwnershipDeclaredAt, (DateTime?)null), Ct);
        await _db.SharedGames
            .Where(g => g.Id == _gameId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsRagPublic, true), Ct);

        var asB = await new GetGamesWithKbQueryHandler(_db).Handle(new GetGamesWithKbQuery(_userB), Ct);

        asB.Should().ContainSingle();
    }

    /// <summary>An admin can always use the chat of a game, so the list offers it.</summary>
    [Fact]
    public async Task AnAdmin_IsOffered_TheGame_WithoutADeclaration()
    {
        await LinkBothUsersAsync();
        await _db.UserLibraryEntries
            .Where(e => e.UserId == _userB)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.OwnershipDeclaredAt, (DateTime?)null), Ct);
        await _db.Users
            .Where(u => u.Id == _userB)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, "Admin"), Ct);

        var asB = await new GetGamesWithKbQueryHandler(_db).Handle(new GetGamesWithKbQuery(_userB), Ct);

        asB.Should().ContainSingle();
    }
}
