using System.Net;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Integration.KnowledgeBase;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #4325: <c>DeletePdfCommandHandler</c> (#2949) counted the links to a PDF with <c>GetCountForEntityAsync</c> and, with
/// more than one, did not delete: it answered "unlinked from game (still referenced by other games)" and left the
/// row, the file and the vectors. The count was every link of anyone from any id, and
/// <c>POST /library/entity-links</c> writes one between any two ids with nothing but a session. Whoever knows the id of
/// somebody else's PDF can keep its owner from deleting it, and the owner gets a success answer.
/// </summary>
/// <remarks>
/// <para>
/// Decision (2026-10-25): the direct delete counts only the links that make sense, with the same rule as the purge of a
/// deleted private game (#4313): <see cref="Api.BoundedContexts.DocumentProcessing.Application.Commands.KbCardLinkRule"/>.
/// The question became "does a game other than the one letting go of the PDF use it?".
/// </para>
/// <para>
/// On Postgres, over HTTP (one case goes through the mediator, because only a saga names a caller game). The links are
/// seeded straight in the database: the point is what the delete does with a link that is already there, not how it
/// got written. The cases above the "Pinned" line are red on the old code. The ones below pin what the new decision
/// keeps: some pass on the old code too (a control), others are the behaviour change itself and are named as such.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4325")]
public sealed class DeletePdfLinksIntegrationTests
    : IClassFixture<DeletePdfKbCacheHostFixture>, IAsyncLifetime
{
    private readonly DeletePdfKbCacheHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _gameId;
    private Guid _pdfId;

    public DeletePdfLinksIntegrationTests(DeletePdfKbCacheHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _hostFixture.HybridCache.Clear();
        _hostFixture.AiCacheCancelsInvalidation = false;
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        _gameId = await SeedPrivateGameAsync(_userId, deleted: false);
        _pdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, _gameId, "Ready", Ct,
            ("At setup each player draws five cards.", QueryVectorEmbeddings.Axis(1f, 0f)));
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// The case of the issue: a stranger writes links to the PDF of somebody else, from games of his own. They are not
    /// the owner's, they do not use the PDF, and two of them were enough to keep the owner from deleting it.
    /// </summary>
    [Fact]
    public async Task TwoLinksOfAStranger_DoNotStopTheOwnerFromDeletingThePdf()
    {
        var (strangerId, firstGame) = await SeedStrangerWithAGameAsync();
        var secondGame = await SeedPrivateGameAsync(strangerId, deleted: false);
        await LinkAsync(firstGame, _pdfId, strangerId);
        await LinkAsync(secondGame, _pdfId, strangerId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("a stranger's links do not use the owner's PDF");
    }

    /// <summary>
    /// A link from an id that is no game at all: <c>Game(any id)</c> is exactly what the endpoint lets anyone write.
    /// </summary>
    [Fact]
    public async Task LinksFromIdsThatAreNoGame_DoNotKeepThePdf()
    {
        await LinkAsync(Guid.NewGuid(), _pdfId, _userId);
        await LinkAsync(Guid.NewGuid(), _pdfId, _userId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("nothing that exists uses it");
    }

    /// <summary>A game that is deleted uses nothing any more, a private one or a catalog one.</summary>
    [Fact]
    public async Task LinksFromGamesThatAreGone_DoNotKeepThePdf()
    {
        var goneGame = await SeedPrivateGameAsync(_userId, deleted: true);
        var goneCatalogGame = await SeedCatalogGameAsync(deleted: true);
        await LinkAsync(goneGame, _pdfId, _userId);
        await LinkAsync(goneCatalogGame, _pdfId, _userId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("both games are gone");
    }

    /// <summary>
    /// The owner's own link (the one the upload creates) next to a stranger's: only the first one counts, so the PDF
    /// has no use beyond the game it was uploaded for, and the threshold of "more than one" is not reached.
    /// </summary>
    [Fact]
    public async Task AStrangersLinkNextToTheOwnLink_DoesNotKeepThePdf()
    {
        var (strangerId, strangersGame) = await SeedStrangerWithAGameAsync();
        await LinkAsync(_gameId, _pdfId, _userId);
        await LinkAsync(strangersGame, _pdfId, strangerId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("the stranger's link is the only thing that was keeping it");
    }

    /// <summary>
    /// A stranger's link from a catalog game: the second branch of the rule is for PDFs that belong to the catalog,
    /// and this one is a private PDF. The same case as in the purge of a deleted private game.
    /// </summary>
    [Fact]
    public async Task AStrangersLinkFromACatalogGame_DoesNotKeepAPrivatePdf()
    {
        var (strangerId, _) = await SeedStrangerWithAGameAsync();
        var catalogGame = await SeedCatalogGameAsync(deleted: false);
        await LinkAsync(_gameId, _pdfId, _userId);
        await LinkAsync(catalogGame, _pdfId, strangerId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("a stranger's catalog link does not use a private PDF");
    }

    /// <summary>
    /// The attack on a PDF a user uploaded against a catalog game (it has <c>SharedGameId</c> set, like every
    /// <c>AddRulebook</c> upload): a stranger reads the PDF id from <c>GET /library/entity-links</c> and writes links
    /// from a catalog game with two link types, which the unique index allows because it includes the type. Neither
    /// is the uploader's, so neither keeps the PDF alive.
    /// </summary>
    [Fact]
    public async Task ForgedLinksOfSeveralTypes_FromACatalogGame_DoNotKeepACatalogPdf()
    {
        var (strangerId, _) = await SeedStrangerWithAGameAsync();
        var uploadedFor = await SeedCatalogGameAsync(deleted: false);
        var anotherCatalogGame = await SeedCatalogGameAsync(deleted: false);
        await MakeItACatalogPdfAsync(_pdfId, uploadedFor);
        await LinkAsync(uploadedFor, _pdfId, _userId);
        await LinkAsync(anotherCatalogGame, _pdfId, strangerId, EntityLinkType.CompanionTo);
        await LinkAsync(anotherCatalogGame, _pdfId, strangerId, EntityLinkType.CollaboratesWith);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("a stranger's links do not make another game use it");
    }

    /// <summary>
    /// A game that reaches the PDF with several link types is one game, and when it is the game letting go of the PDF
    /// it keeps nothing alive. The owner adding a second link type from the same game must not block the delete.
    /// </summary>
    [Fact]
    public async Task SeveralLinksFromTheSameGame_AreNotAnotherGame()
    {
        await LinkAsync(_gameId, _pdfId, _userId, EntityLinkType.RelatedTo);
        await LinkAsync(_gameId, _pdfId, _userId, EntityLinkType.CompanionTo);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("no game other than the one letting go of it uses it");
    }

    /// <summary>
    /// The owner wrote the links, but from games of somebody else: they are not games of the owner, so they do not
    /// use the owner's PDF.
    /// </summary>
    [Fact]
    public async Task TwoLinksOfTheOwner_FromGamesOfSomeoneElse_DoNotKeepThePdf()
    {
        var (_, firstGame) = await SeedStrangerWithAGameAsync();
        var (_, secondGame) = await SeedStrangerWithAGameAsync();
        await LinkAsync(firstGame, _pdfId, _userId);
        await LinkAsync(secondGame, _pdfId, _userId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("those games are not the owner's");
    }

    /// <summary>
    /// THE CHOICE, pinned so that changing it is deliberate. The catalog deduplication links a user who uploads a file
    /// that is already there to the PDF of the first uploader, with a link of their own; before #4326 it did so across
    /// catalog games too, and those links are still in the database. A game reached that way is left out when the first
    /// uploader deletes the PDF: the link cannot be told from a forged one
    /// (<c>SharedGameId</c> is set on every PDF a user uploads against a catalog game, and a link carries nothing that
    /// says a trusted flow wrote it). Protecting that reuse needs a sign that only the reusing flows write.
    /// </summary>
    [Fact]
    public async Task ACatalogGameThatReusedThePdf_WithALinkOfItsOwn_IsNotKeptAliveByIt()
    {
        var (reuserId, _) = await SeedStrangerWithAGameAsync();
        var uploadedFor = await SeedCatalogGameAsync(deleted: false);
        var reusingGame = await SeedCatalogGameAsync(deleted: false);
        await MakeItACatalogPdfAsync(_pdfId, uploadedFor);
        await LinkAsync(uploadedFor, _pdfId, _userId);
        await LinkAsync(reusingGame, _pdfId, reuserId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("the link of somebody else is never counted, whatever the game");
    }

    /// <summary>
    /// The rule is about games. The endpoint lets a link start from any entity type with any id, so one that starts
    /// from an agent, a session, ... and happens to carry the id of a game that exists is not a game using the PDF.
    /// </summary>
    [Fact]
    public async Task ALinkWhoseSourceIsNotAGame_DoesNotKeepThePdf()
    {
        var secondGame = await SeedPrivateGameAsync(_userId, deleted: false);
        await LinkAsync(_gameId, _pdfId, _userId);
        await LinkAsync(secondGame, _pdfId, _userId, sourceType: MeepleEntityType.Session);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("a session is not a game, whatever id it carries");
    }

    // ─── Pinned: what the new decision keeps ─────────────────

    /// <summary>
    /// The game letting go of the PDF is the one the caller names (<c>RemoveRagFromSharedGame</c> does), and the
    /// others are everything else. Here the caller's own game is gone, so its own link does not stand up; the game the
    /// PDF was uploaded for still uses it. A count with a threshold would read one link and call the PDF unused.
    /// </summary>
    [Fact]
    public async Task TheCallerGameBeingGone_DoesNotMakeThePdfLookUnused()
    {
        var uploadedFor = await SeedCatalogGameAsync(deleted: false);
        var goneCallerGame = await SeedCatalogGameAsync(deleted: true);
        await MakeItACatalogPdfAsync(_pdfId, uploadedFor);
        await LinkAsync(uploadedFor, _pdfId, _userId);
        await LinkAsync(goneCallerGame, _pdfId, _userId);

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new DeletePdfCommand(_pdfId.ToString(), CallerGameId: goneCallerGame), Ct);

        result.Success.Should().BeTrue();
        (await PdfExistsAsync(_pdfId)).Should().BeTrue("the game it was uploaded for still uses it");
    }

    /// <summary>
    /// Two links of the owner from two games that exist: another game uses it, the row stays. For a private PDF the
    /// owner can never delete it from here while that holds, and the answer is still a success: the open question of
    /// #4627, which this test does not settle.
    /// </summary>
    [Fact]
    public async Task TwoLinksOfTheOwner_FromGamesThatExist_KeepThePdf()
    {
        var secondGame = await SeedPrivateGameAsync(_userId, deleted: false);
        await LinkAsync(_gameId, _pdfId, _userId);
        await LinkAsync(secondGame, _pdfId, _userId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeTrue("the owner's second game still uses it");
    }

    /// <summary>The PDF with only its own link is the last use: it goes.</summary>
    [Fact]
    public async Task APdfWithOnlyItsOwnLink_IsDeleted()
    {
        await LinkAsync(_gameId, _pdfId, _userId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse();
    }

    /// <summary>
    /// Asking twice changes nothing the second time while another game uses the PDF. The first call removes the link of
    /// the game the PDF was uploaded for; that game is still the one letting go on the second call, the other game
    /// still uses it, and there is no link of the caller left to remove. The old threshold deleted it on the second
    /// call, taking the PDF from the game that used it. Pinned so that the answer to "what does the owner see" (#4627)
    /// is a decision and not a surprise: the message says that nothing was left to unlink.
    /// </summary>
    [Fact]
    public async Task ADeleteAskedTwice_OfAPdfAnotherGameUses_KeepsItBothTimes()
    {
        var uploadedFor = await SeedCatalogGameAsync(deleted: false);
        var otherGame = await SeedCatalogGameAsync(deleted: false);
        await MakeItACatalogPdfAsync(_pdfId, uploadedFor);
        await LinkAsync(uploadedFor, _pdfId, _userId);
        await LinkAsync(otherGame, _pdfId, _userId);

        var first = await SendDeleteThroughMediatorAsync(_pdfId);
        var second = await SendDeleteThroughMediatorAsync(_pdfId);

        first.Success.Should().BeTrue();
        first.Message.Should().Contain("unlinked");
        second.Success.Should().BeTrue();
        second.Message.Should().Contain("kept", "there was no link of the caller left to remove");
        (await PdfExistsAsync(_pdfId)).Should().BeTrue("the other game still uses it");
    }

    /// <summary>
    /// A legacy row with no game of its own (neither <c>SharedGameId</c> nor <c>PrivateGameId</c>) and no named caller
    /// has nobody letting go of it: its single link is its only use, and it goes, as it did.
    /// </summary>
    [Fact]
    public async Task APdfWithNoGameOfItsOwn_AndOneLink_IsDeleted()
    {
        await ForgetTheGamesOfAsync(_pdfId);
        await LinkAsync(_gameId, _pdfId, _userId);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("its single link is its only use");
    }

    /// <summary>
    /// <c>IgnoreQueryFilters</c> removes the default filter on links, so the rule has to leave out the soft-deleted ones
    /// by itself. An owner who deleted a reuse link (the `DELETE /library/entity-links` flow) from a game that still
    /// exists has let go of the PDF from that game.
    /// </summary>
    [Fact]
    public async Task ASoftDeletedLink_FromAGameThatExists_DoesNotKeepThePdf()
    {
        var secondGame = await SeedPrivateGameAsync(_userId, deleted: false);
        await LinkAsync(_gameId, _pdfId, _userId);
        await LinkAsync(secondGame, _pdfId, _userId);
        await SoftDeleteLinksFromAsync(secondGame);

        var response = await DeletePdfAsync(_pdfId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await PdfExistsAsync(_pdfId)).Should().BeFalse("the only other link was deleted");
    }

    /// <summary>
    /// #4641: a catalog game can hold two links to the same PDF, one for each user who declared to have the file. When
    /// the uploader takes the PDF off the game (the user flow names no caller game), the uploader's links go; the other
    /// user's stays, because that user declared it too and the PDF is still there for the game that uses it.
    /// </summary>
    [Fact]
    public async Task TheUploaderLettingGo_KeepsTheLinkOfTheOtherUser()
    {
        var (secondUploaderId, _) = await SeedStrangerWithAGameAsync();
        var uploadedFor = await SeedCatalogGameAsync(deleted: false);
        var otherGame = await SeedCatalogGameAsync(deleted: false);
        await MakeItACatalogPdfAsync(_pdfId, uploadedFor);
        await LinkAsync(uploadedFor, _pdfId, _userId);
        await LinkAsync(uploadedFor, _pdfId, secondUploaderId);
        await LinkAsync(otherGame, _pdfId, _userId);

        var result = await SendDeleteThroughMediatorAsync(_pdfId);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("unlinked");
        (await LinkOwnersAsync(uploadedFor, _pdfId)).Should().BeEquivalentTo(new[] { secondUploaderId },
            "the uploader's link went, the other user's did not");
        (await LinkOwnersAsync(otherGame, _pdfId)).Should().BeEquivalentTo(new[] { _userId });
        (await PdfExistsAsync(_pdfId)).Should().BeTrue("the other game still uses it");
    }

    /// <summary>
    /// The flow that names a caller game (an admin taking the RAG off a catalog game) decides for the game, not for a
    /// user: every link of that game to the PDF goes, whoever wrote it.
    /// </summary>
    [Fact]
    public async Task ANamedCallerGame_LosesTheLinksOfEveryUser()
    {
        var (secondUploaderId, _) = await SeedStrangerWithAGameAsync();
        var uploadedFor = await SeedCatalogGameAsync(deleted: false);
        var otherGame = await SeedCatalogGameAsync(deleted: false);
        await MakeItACatalogPdfAsync(_pdfId, uploadedFor);
        await LinkAsync(uploadedFor, _pdfId, _userId);
        await LinkAsync(uploadedFor, _pdfId, secondUploaderId);
        await LinkAsync(otherGame, _pdfId, _userId);

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new DeletePdfCommand(_pdfId.ToString(), CallerGameId: uploadedFor), Ct);

        result.Success.Should().BeTrue();
        (await LinkOwnersAsync(uploadedFor, _pdfId)).Should().BeEmpty("the game let go of the PDF, for every user");
        (await LinkOwnersAsync(otherGame, _pdfId)).Should().BeEquivalentTo(new[] { _userId });
        (await PdfExistsAsync(_pdfId)).Should().BeTrue("the other game still uses it");
    }

    // ─── Support ────────────────────────────────────────────────

    private Task<HttpResponseMessage> DeletePdfAsync(Guid pdfId) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/api/v1/pdf/{pdfId}", _token), Ct);

    private async Task<Guid> SeedPrivateGameAsync(Guid ownerId, bool deleted)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
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

    private async Task<(Guid UserId, Guid GameId)> SeedStrangerWithAGameAsync()
    {
        Guid strangerId;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (strangerId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        return (strangerId, await SeedPrivateGameAsync(strangerId, deleted: false));
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

    /// <summary>The PDF as a catalog upload leaves it: the catalog game in <c>SharedGameId</c>, no private game.</summary>
    private async Task MakeItACatalogPdfAsync(Guid pdfId, Guid sharedGameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.PdfDocuments
            .Where(p => p.Id == pdfId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.SharedGameId, sharedGameId)
                .SetProperty(p => p.PrivateGameId, (Guid?)null), Ct);
    }

    private async Task LinkAsync(
        Guid gameId,
        Guid pdfId,
        Guid ownerId,
        EntityLinkType linkType = EntityLinkType.RelatedTo,
        MeepleEntityType sourceType = MeepleEntityType.Game)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.EntityLinks.Add(EntityLink.Create(
            sourceType, gameId,
            MeepleEntityType.KbCard, pdfId,
            linkType, EntityLinkScope.User, ownerId));
        await db.SaveChangesAsync(Ct);
    }

    /// <summary>Soft-deletes the links that start from the game, as <c>DELETE /library/entity-links</c> does.</summary>
    private async Task SoftDeleteLinksFromAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.EntityLinks
            .Where(l => l.SourceEntityId == gameId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsDeleted, true), Ct);
    }

    /// <summary>The PDF as a legacy upload leaves it: no catalog game, no private game.</summary>
    private async Task ForgetTheGamesOfAsync(Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.PdfDocuments
            .Where(p => p.Id == pdfId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.SharedGameId, (Guid?)null)
                .SetProperty(p => p.PrivateGameId, (Guid?)null), Ct);
    }

    private async Task<PdfDeleteResult> SendDeleteThroughMediatorAsync(Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new DeletePdfCommand(pdfId.ToString()), Ct);
    }

    private async Task<List<Guid>> LinkOwnersAsync(Guid gameId, Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.EntityLinks
            .Where(l => l.SourceEntityId == gameId && l.TargetEntityId == pdfId && l.TargetEntityType == MeepleEntityType.KbCard)
            .Select(l => l.OwnerUserId)
            .ToListAsync(Ct);
    }

    private async Task<bool> PdfExistsAsync(Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.PdfDocuments.IgnoreQueryFilters().AnyAsync(p => p.Id == pdfId, Ct);
    }
}
