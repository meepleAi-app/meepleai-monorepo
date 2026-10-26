using System.Net;
using System.Text.Json;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
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
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.EntityRelationships;

/// <summary>
/// #4637: <c>GET /api/v1/library/entity-links</c> returned the <c>User</c>-scope links of EVERY user, with the id of
/// the PDF each one points at and the owner of the link, and <c>/count</c> counted them all. Measured over HTTP: user A
/// links her own PDF to a catalog game, user B reads that link. The id of somebody else's PDF was the one thing missing
/// for the attack of #4325 (a forged link that keeps the owner from deleting a PDF).
/// </summary>
/// <remarks>
/// <para>
/// Decision (2026-10-26): a <c>User</c>-scope link is personal, so the user's reading leaves out those of other users.
/// <c>Shared</c>-scope links (imported or curated) are not personal and keep counting for everybody. The admin
/// listing, which sees every scope, is not touched.
/// </para>
/// <para>
/// On Postgres, over HTTP. The links are seeded straight in the database.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "EntityRelationships")]
[Trait("Issue", "4637")]
public sealed class EntityLinkReadVisibilityIntegrationTests
    : IClassFixture<DeletePdfKbCacheHostFixture>, IAsyncLifetime
{
    private readonly DeletePdfKbCacheHostFixture _hostFixture;
    private HttpClient _client = null!;
    private Guid _aId;
    private string _aToken = null!;
    private Guid _bId;
    private string _bToken = null!;
    private string _adminToken = null!;
    private Guid _catalogGameId;
    private Guid _otherCatalogGameId;
    private Guid _pdfOfA;
    private Guid _pdfOfB;

    public EntityLinkReadVisibilityIntegrationTests(DeletePdfKbCacheHostFixture hostFixture)
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
        (_aId, _aToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        (_bId, _bToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        (_, _adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct);

        _catalogGameId = await SeedCatalogGameAsync(db);
        _otherCatalogGameId = await SeedCatalogGameAsync(db);
        var gameOfA = await SeedPrivateGameAsync(db, _aId);
        var gameOfB = await SeedPrivateGameAsync(db, _bId);
        _pdfOfA = await PrivateGameKbSeed.IndexPdfAsync(db, _aId, gameOfA, "Ready", Ct,
            ("A rule of A.", QueryVectorEmbeddings.Axis(1f, 0f)));
        _pdfOfB = await PrivateGameKbSeed.IndexPdfAsync(db, _bId, gameOfB, "Ready", Ct,
            ("A rule of B.", QueryVectorEmbeddings.Axis(0f, 1f)));

        // A and B each link their own PDF to the same catalog game, as the upload does.
        db.EntityLinks.Add(EntityLink.Create(
            MeepleEntityType.Game, _catalogGameId, MeepleEntityType.KbCard, _pdfOfA,
            EntityLinkType.RelatedTo, EntityLinkScope.User, _aId));
        db.EntityLinks.Add(EntityLink.Create(
            MeepleEntityType.Game, _catalogGameId, MeepleEntityType.KbCard, _pdfOfB,
            EntityLinkType.RelatedTo, EntityLinkScope.User, _bId));
        // A link that is nobody's in particular: curated, shared.
        db.EntityLinks.Add(EntityLink.Create(
            MeepleEntityType.Game, _catalogGameId, MeepleEntityType.Game, _otherCatalogGameId,
            EntityLinkType.ExpansionOf, EntityLinkScope.Shared, _aId));
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── The listing ─────────────────────────────────────────────

    /// <summary>The case of the issue: B reads the links of the catalog game and gets A's, with the id of A's PDF.</summary>
    [Fact]
    public async Task TheLinksOfAnotherUser_AreNotInTheListing()
    {
        var links = await ListAsync(_bToken, _catalogGameId);

        links.Select(l => l.TargetEntityId).Should().NotContain(_pdfOfA, "the link is A's, and so is the id of her PDF");
        links.Select(l => l.OwnerUserId).Should().OnlyContain(owner => owner == _bId);
    }

    /// <summary>Each user reads her own, with <c>IsOwner</c> set: the listing still works for what is hers.</summary>
    [Fact]
    public async Task TheOwnLinks_AreInTheListing_AsTheirOwner()
    {
        var links = await ListAsync(_aToken, _catalogGameId);

        var own = links.Should().ContainSingle().Which;
        own.TargetEntityId.Should().Be(_pdfOfA);
        own.OwnerUserId.Should().Be(_aId);
        own.IsOwner.Should().BeTrue();
    }

    // ─── The count ───────────────────────────────────────────────

    /// <summary>
    /// The badge counts what the viewer can see: her own <c>User</c> links and the <c>Shared</c> ones. Game X has A's
    /// link, B's link and a shared one.
    /// </summary>
    [Fact]
    public async Task TheCount_LeavesOutTheUserLinksOfOthers_AndKeepsTheSharedOnes()
    {
        var forA = await CountAsync(_aToken, MeepleEntityType.Game, _catalogGameId);
        var forB = await CountAsync(_bToken, MeepleEntityType.Game, _catalogGameId);

        forA.Should().Be(2, "her own link and the shared one");
        forB.Should().Be(2, "her own link and the shared one");
    }

    /// <summary>The count of somebody else's PDF reveals nothing: A's PDF has one link, A's, and B does not see it.</summary>
    [Fact]
    public async Task TheCountOfSomebodyElsesPdf_IsZero()
    {
        (await CountAsync(_bToken, MeepleEntityType.KbCard, _pdfOfA)).Should().Be(0);
        (await CountAsync(_aToken, MeepleEntityType.KbCard, _pdfOfA)).Should().Be(1);
    }

    // ─── Not touched ─────────────────────────────────────────────

    /// <summary>The admin listing sees every scope and every owner: it is the tool to look at what is there.</summary>
    [Fact]
    public async Task TheAdminListing_StillSeesEveryLink()
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/admin/entity-links?sourceType=Game&sourceId={_catalogGameId}", _adminToken);
        var response = await _client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetArrayLength().Should().Be(3, "A's link, B's link and the shared one");
    }

    // ─── Support ────────────────────────────────────────────────

    private sealed record LinkRow(Guid TargetEntityId, Guid OwnerUserId, bool IsOwner);

    private async Task<List<LinkRow>> ListAsync(string token, Guid gameId)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/library/entity-links?entityType=Game&entityId={gameId}&targetEntityType=KbCard", token);
        var response = await _client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.EnumerateArray()
            .Select(e => new LinkRow(
                e.GetProperty("targetEntityId").GetGuid(),
                e.GetProperty("ownerUserId").GetGuid(),
                e.GetProperty("isOwner").GetBoolean()))
            .ToList();
    }

    private async Task<int> CountAsync(string token, MeepleEntityType type, Guid id)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/library/entity-links/count?entityType={type}&entityId={id}", token);
        var response = await _client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("count").GetInt32();
    }

    private static async Task<Guid> SeedPrivateGameAsync(MeepleAiDbContext db, Guid ownerId)
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
        });
        await db.SaveChangesAsync(Ct);
        return gameId;
    }

    private async Task<Guid> SeedCatalogGameAsync(MeepleAiDbContext db)
    {
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
            CreatedBy = _aId,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
        return gameId;
    }
}
