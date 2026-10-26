using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.Integration.DocumentProcessing;
using Api.Tests.Integration.KnowledgeBase;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.EntityRelationships;

/// <summary>
/// #4325: <c>POST /api/v1/library/entity-links</c> wrote a link between any two ids with nothing but a session
/// (measured: 201 for <c>Game(any id) → KbCard(somebody else's PDF)</c>). Code reads a link <c>Game → KbCard</c> as
/// "this game uses this PDF", so the endpoint has to check that the game and the PDF exist and that the caller may
/// link them.
/// </summary>
/// <remarks>
/// <para>
/// Scope, decided 2026-10-26: only the pair the code treats as fact, <c>Game</c> and <c>KbCard</c>, and only on the
/// endpoint. Player, Session, Agent, Document, ChatSession, Event and Toolkit are not checked yet. The flows that link on their own (upload, reuse by
/// deduplication) send <c>CreateEntityLinkCommand</c> to the mediator directly and are untouched; one case below
/// pins it.
/// </para>
/// <para>
/// A game is linkable when it is a private game of the caller that exists, or a catalog game that exists; to attach a
/// PDF to a catalog game, one that the caller has in the library (as <c>AddRulebook</c> asks). A PDF is linkable when
/// the caller uploaded it. "Not found" and "not yours" answer the same 404, so the endpoint does not
/// say which ids exist.
/// </para>
/// <para>On Postgres, over HTTP.</para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "EntityRelationships")]
[Trait("Issue", "4325")]
public sealed class CreateEntityLinkGuardIntegrationTests
    : IClassFixture<DeletePdfKbCacheHostFixture>, IAsyncLifetime
{
    private readonly DeletePdfKbCacheHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _gameId;
    private Guid _pdfId;
    private Guid _strangerId;
    private Guid _strangersGameId;
    private Guid _strangersPdfId;

    public CreateEntityLinkGuardIntegrationTests(DeletePdfKbCacheHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        (_strangerId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        _gameId = await SeedPrivateGameAsync(db, _userId);
        _strangersGameId = await SeedPrivateGameAsync(db, _strangerId);
        _pdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, _gameId, "Ready", Ct,
            ("At setup each player draws five cards.", QueryVectorEmbeddings.Axis(1f, 0f)));
        _strangersPdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _strangerId, _strangersGameId, "Ready", Ct,
            ("A rule that is not yours.", QueryVectorEmbeddings.Axis(0f, 1f)));
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── Refused ─────────────────────────────────────────────────

    /// <summary>The case of the issue: an id that is no game, a PDF that is somebody else's.</summary>
    [Fact]
    public async Task AGameThatDoesNotExist_LinkedToSomebodyElsesPdf_IsRefused()
    {
        var response = await CreateLinkAsync(Guid.NewGuid(), MeepleEntityType.Game, _strangersPdfId, MeepleEntityType.KbCard);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync(Ct));
        (await LinksOfAsync(_strangersPdfId)).Should().Be(0);
    }

    [Fact]
    public async Task AnExistingCatalogGame_LinkedToSomebodyElsesPdf_IsRefused()
    {
        var catalogGame = await SeedCatalogGameAsync(deleted: false);

        var response = await CreateLinkAsync(catalogGame, MeepleEntityType.Game, _strangersPdfId, MeepleEntityType.KbCard);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync(Ct));
        (await LinksOfAsync(_strangersPdfId)).Should().Be(0);
    }

    [Fact]
    public async Task APdfThatDoesNotExist_IsRefused()
    {
        var response = await CreateLinkAsync(_gameId, MeepleEntityType.Game, Guid.NewGuid(), MeepleEntityType.KbCard);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task SomebodyElsesPrivateGame_LinkedToYourOwnPdf_IsRefused()
    {
        var response = await CreateLinkAsync(_strangersGameId, MeepleEntityType.Game, _pdfId, MeepleEntityType.KbCard);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync(Ct));
        (await LinksOfAsync(_pdfId)).Should().Be(0);
    }

    /// <summary>A game that is deleted is not a game to link, a private one or a catalog one.</summary>
    [Fact]
    public async Task AGameThatIsDeleted_IsRefused()
    {
        var gonePrivate = await SeedPrivateGameAsync(_userId, deleted: true);
        var goneCatalog = await SeedCatalogGameAsync(deleted: true);

        var first = await CreateLinkAsync(gonePrivate, MeepleEntityType.Game, _pdfId, MeepleEntityType.KbCard);
        var second = await CreateLinkAsync(goneCatalog, MeepleEntityType.Game, _pdfId, MeepleEntityType.KbCard);

        first.StatusCode.Should().Be(HttpStatusCode.NotFound);
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LinksOfAsync(_pdfId)).Should().Be(0);
    }

    /// <summary>The guard looks at the game on both ends: a Game → Game link to somebody else's private game.</summary>
    [Fact]
    public async Task AGameLinkedToSomebodyElsesPrivateGame_IsRefused()
    {
        var catalogGame = await SeedCatalogGameAsync(deleted: false);

        var response = await CreateLinkAsync(
            catalogGame, MeepleEntityType.Game, _strangersGameId, MeepleEntityType.Game, EntityLinkType.ExpansionOf);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>
    /// A catalog game that exists but that the caller does not have in the library: <c>AddRulebook</c> would answer 403
    /// for it. Without this, anybody could attach their PDF to any catalog game, and it would show in the rulebook list
    /// of everyone who owns that game.
    /// </summary>
    [Fact]
    public async Task ACatalogGameNotInYourLibrary_LinkedToYourOwnPdf_IsRefused()
    {
        var catalogGame = await SeedCatalogGameAsync(deleted: false);

        var response = await CreateLinkAsync(catalogGame, MeepleEntityType.Game, _pdfId, MeepleEntityType.KbCard);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync(Ct));
        (await LinksOfAsync(_pdfId)).Should().Be(0);
    }

    // ─── Allowed ─────────────────────────────────────────────────

    /// <summary>What the upload creates for a private game, and what the user is allowed to do by hand.</summary>
    [Fact]
    public async Task YourOwnPrivateGame_LinkedToYourOwnPdf_IsCreated()
    {
        var response = await CreateLinkAsync(_gameId, MeepleEntityType.Game, _pdfId, MeepleEntityType.KbCard);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        (await LinksOfAsync(_pdfId)).Should().Be(1);
    }

    /// <summary>What <c>AddRulebook</c> creates for a catalog game: the uploader links a catalog game of her library.</summary>
    [Fact]
    public async Task ACatalogGameInYourLibrary_LinkedToYourOwnPdf_IsCreated()
    {
        var catalogGame = await SeedCatalogGameAsync(deleted: false);
        await AddToLibraryAsync(catalogGame);

        var response = await CreateLinkAsync(catalogGame, MeepleEntityType.Game, _pdfId, MeepleEntityType.KbCard);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>
    /// <c>AddExpansionSheet</c>: a private game the user just created, as the expansion of a catalog game. Not a PDF at
    /// all, and the case that shows the guard did not take over the rest of the endpoint.
    /// </summary>
    [Fact]
    public async Task YourPrivateGame_AsTheExpansionOfACatalogGame_IsCreated()
    {
        var catalogGame = await SeedCatalogGameAsync(deleted: false);

        var response = await CreateLinkAsync(
            _gameId, MeepleEntityType.Game, catalogGame, MeepleEntityType.Game, EntityLinkType.ExpansionOf);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>
    /// The entity types the guard does not cover keep the old behaviour, so that this change does not turn into a rule
    /// nobody decided: a link from a session id the database has never seen is still created.
    /// </summary>
    [Fact]
    public async Task AnEntityTypeTheGuardDoesNotCover_IsStillCreated()
    {
        var response = await CreateLinkAsync(
            Guid.NewGuid(), MeepleEntityType.Session, Guid.NewGuid(), MeepleEntityType.Agent, EntityLinkType.RelatedTo);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>
    /// The flows that link on their own (upload, reuse by deduplication) send the command to the mediator and never
    /// pass through the endpoint. The reuse links a user to the PDF of somebody else: it has to keep working.
    /// </summary>
    [Fact]
    public async Task TheFlowsThatLinkOnTheirOwn_AreNotSubjectToTheGuard()
    {
        var catalogGame = await SeedCatalogGameAsync(deleted: false);

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var dto = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new CreateEntityLinkCommand(
                MeepleEntityType.Game, catalogGame,
                MeepleEntityType.KbCard, _strangersPdfId,
                EntityLinkType.RelatedTo, EntityLinkScope.User, _userId), Ct);

        dto.Should().NotBeNull();
        (await LinksOfAsync(_strangersPdfId)).Should().Be(1);
    }

    /// <summary>The duplicate is still a 409, once the guard has agreed.</summary>
    [Fact]
    public async Task AnAllowedLinkCreatedTwice_IsStillAConflict()
    {
        (await CreateLinkAsync(_gameId, MeepleEntityType.Game, _pdfId, MeepleEntityType.KbCard))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await CreateLinkAsync(_gameId, MeepleEntityType.Game, _pdfId, MeepleEntityType.KbCard);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict, await second.Content.ReadAsStringAsync(Ct));
    }

    // ─── Support ────────────────────────────────────────────────

    private async Task<HttpResponseMessage> CreateLinkAsync(
        Guid sourceId,
        MeepleEntityType sourceType,
        Guid targetId,
        MeepleEntityType targetType,
        EntityLinkType linkType = EntityLinkType.RelatedTo)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/library/entity-links", _token);
        request.Content = JsonContent.Create(new
        {
            sourceEntityType = sourceType.ToString(),
            sourceEntityId = sourceId,
            targetEntityType = targetType.ToString(),
            targetEntityId = targetId,
            linkType = linkType.ToString(),
        });
        return await _client.SendAsync(request, Ct);
    }

    private async Task AddToLibraryAsync(Guid catalogGameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SharedGameId = catalogGameId,
            AddedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<int> LinksOfAsync(Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.EntityLinks.IgnoreQueryFilters()
            .CountAsync(l => l.TargetEntityType == MeepleEntityType.KbCard && l.TargetEntityId == pdfId, Ct);
    }

    private async Task<Guid> SeedPrivateGameAsync(Guid ownerId, bool deleted)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await SeedPrivateGameAsync(db, ownerId, deleted);
    }

    private static async Task<Guid> SeedPrivateGameAsync(MeepleAiDbContext db, Guid ownerId, bool deleted = false)
    {
        var gameId = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = gameId,
            OwnerId = ownerId,
            Title = "A prototype",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
            IsDeleted = deleted,
        });
        await db.SaveChangesAsync(Ct);
        return gameId;
    }

    private async Task<Guid> SeedCatalogGameAsync(bool deleted)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "A catalog game",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = deleted,
        });
        await db.SaveChangesAsync(Ct);
        return gameId;
    }
}
