using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perché e le condizioni per
/// condividere il database stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perché condividere il database è sicuro QUI.</b> Ogni test crea il proprio utente
/// (<c>TestSessionHelper.CreateUserSessionAsync</c>, email univoca), la propria chat e il proprio link, e
/// legge e scrive solo la riga di quel link, cercata per id. Nessuna asserzione riguarda conteggi o liste
/// complete.
/// </para>
/// </summary>
public sealed class SharedThreadVisitHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "shared_thread_race")
{
    /// <summary>
    /// <c>POST /share-links</c> firma il token con <c>Jwt:ShareLinks:SecretKey</c> e compone l'URL da
    /// <c>App:BaseUrl</c>: la configurazione d'integrazione non porta né l'una né l'altra, e senza la
    /// creazione del link risponde 500.
    /// </summary>
    protected override Dictionary<string, string?>? ExtraConfiguration => new()
    {
        ["Jwt:ShareLinks:SecretKey"] = "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256",
        ["App:BaseUrl"] = "http://localhost:3000",
    };

    /// <summary>
    /// Le gare fra visita pubblica e proprietario (#4573): il repository dei link è decorato perché un test
    /// possa fermare una richiesta dopo la lettura del link. Senza un'azione registrata il decoratore è
    /// trasparente, quindi gli altri test della classe non lo vedono.
    /// </summary>
    internal SharedThreadLinkReadInterleaving LinkReads { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(LinkReads.Decorate));
}

/// <summary>
/// Le gare fra la visita anonima a una chat condivisa e il proprietario del link (#4573). Stessa forma di
/// #4558, sui link di condivisione della libreria.
/// </summary>
/// <remarks>
/// <para>
/// Ogni test ferma la prima richiesta in un punto preciso (<see cref="SharedThreadLinkReadInterleaving"/>) e,
/// in quel punto, esegue per intero la seconda. In produzione l'ordine lo decide il caso; qui è fisso,
/// quindi il test è deterministico.
/// </para>
/// <para>
/// Prima di #4573 la visita leggeva l'aggregato, lo incrementava in memoria e lo riscriveva (<c>RecordAccess</c>
/// + <c>UpdateAsync</c>), e <c>UpdateAsync</c> copiava sulla riga rilette i valori dell'aggregato. Con la
/// lettura fermata prima della scrittura, la revoca salvata nel mezzo tornava <c>revoked_at = NULL</c> e due
/// visite contavano una volta sola. Ora la visita conta con un solo UPDATE condizionato alla validità
/// (<c>TryRecordAccessAsync</c>) e non legge più il link: il punto di fermata è l'istante prima di quel conteggio.
/// </para>
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4573")]
public sealed class SharedThreadVisitRaceIntegrationTests : IClassFixture<SharedThreadVisitHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly SharedThreadLinkReadInterleaving _interleaving;

    public SharedThreadVisitRaceIntegrationTests(SharedThreadVisitHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
        _interleaving = host.LinkReads;
    }

    [Fact]
    public async Task GetSharedThread_RevokedWhileTheVisitIsInFlight_TheRevocationStays()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);
        HttpStatusCode? revokeStatus = null;
        _interleaving.BeforeNextVisitCount(link.Id, async () => revokeStatus = await RevokeAsync(link.Id, owner));

        using var visit = await VisitAsync(link.Token);

        _interleaving.IsPending(link.Id).Should().BeFalse("setup: the revocation must run inside the visit");
        revokeStatus.Should().Be(HttpStatusCode.OK, "setup: the owner revokes the link while the visit is in flight");

        var row = await ReadRowAsync(link.Id);
        row.RevokedAt.Should().NotBeNull(
            "a public visit must never undo a revocation. If the visit writes back the revoked_at it read, " +
            "the revoked link is active again in the database");
    }

    [Fact]
    public async Task GetSharedThread_RevokedWhileTheVisitIsInFlight_Answers404AndDoesNotCount()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);
        HttpStatusCode? revokeStatus = null;
        _interleaving.BeforeNextVisitCount(link.Id, async () => revokeStatus = await RevokeAsync(link.Id, owner));

        using var visit = await VisitAsync(link.Token);
        var body = await visit.Content.ReadAsStringAsync();

        _interleaving.IsPending(link.Id).Should().BeFalse("setup: the revocation must run inside the visit");
        revokeStatus.Should().Be(HttpStatusCode.OK, "setup: the owner revokes the link while the visit is in flight");

        var row = await ReadRowAsync(link.Id);
        using var after = await VisitAsync(link.Token);

        // Tutte le asserzioni insieme: il rosso deve mostrare ogni effetto della visita, non solo il primo.
        using (new AssertionScope())
        {
            visit.StatusCode.Should().Be(HttpStatusCode.NotFound,
                $"the link was revoked before the visit could count itself, so the visit must not return the chat. Body: {body}");
            row.AccessCount.Should().Be(0, "a visit that lost the race to the revocation is not an access");
            row.LastAccessedAt.Should().BeNull();
            row.RevokedAt.Should().NotBeNull();
            after.StatusCode.Should().Be(HttpStatusCode.NotFound, "the revoked link stays revoked");
        }
    }

    [Fact]
    public async Task GetSharedThread_TwoVisitsInterleaved_CountsBoth()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);
        HttpStatusCode? secondVisit = null;
        _interleaving.BeforeNextVisitCount(link.Id, async () =>
        {
            using var response = await VisitAsync(link.Token);
            secondVisit = response.StatusCode;
        });

        using var firstVisit = await VisitAsync(link.Token);

        _interleaving.IsPending(link.Id).Should().BeFalse("setup: the second visit must run inside the first");
        firstVisit.StatusCode.Should().Be(HttpStatusCode.OK);
        secondVisit.Should().Be(HttpStatusCode.OK);

        var row = await ReadRowAsync(link.Id);
        row.AccessCount.Should().Be(2,
            "two visits are two accesses. If the first visit writes back the count it read plus one, " +
            "the second access is lost");
        row.LastAccessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSharedThread_ManyVisitsAtTheSameTime_CountEveryOne()
    {
        // Non è una gara pilotata: lancia le visite insieme e basta. Il conteggio è un UPDATE atomico, quindi
        // il totale è esatto a ogni esecuzione; una lettura-modifica-scrittura ne perderebbe qualcuna.
        const int visits = 10;
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);

        var statuses = await Task.WhenAll(Enumerable.Range(0, visits).Select(async _ =>
        {
            using var response = await VisitAsync(link.Token);
            return response.StatusCode;
        }));

        var row = await ReadRowAsync(link.Id);
        using (new AssertionScope())
        {
            statuses.Should().OnlyContain(status => status == HttpStatusCode.OK);
            row.AccessCount.Should().Be(visits, "every visit is an access");
        }
    }

    [Fact]
    public async Task GetSharedThread_ExpiredWhileTheVisitIsInFlight_Answers404AndDoesNotCount()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);

        // La scadenza passa fra la lettura e il conteggio: il token firmato e la blacklist hanno visto il link
        // valido, quindi a fermare la visita può essere solo la condizione del conteggio sulla riga.
        _interleaving.BeforeNextVisitCount(link.Id, () => ExpireAsync(link.Id));

        using var visit = await VisitAsync(link.Token);

        _interleaving.IsPending(link.Id).Should().BeFalse("setup: the link must expire inside the visit");
        var row = await ReadRowAsync(link.Id);
        using (new AssertionScope())
        {
            visit.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "the link expired before the visit could count itself: the count is conditioned on expires_at too");
            row.AccessCount.Should().Be(0, "a visit to an expired link is not an access");
            row.LastAccessedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task GetSharedThread_RevokedInTheDatabaseWithoutTheBlacklistKey_Answers404()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);
        (await RevokeAsync(link.Id, owner)).Should().Be(HttpStatusCode.OK, "setup: the owner revokes the link");

        // La chiave Redis si può perdere (riavvio, eviction, cache in memoria). La riga resta revocata.
        await RemoveBlacklistKeyAsync(link.Id);

        using var visit = await VisitAsync(link.Token);

        var row = await ReadRowAsync(link.Id);
        using (new AssertionScope())
        {
            visit.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "the row says revoked: losing the blacklist key must not make the link valid again");
            row.AccessCount.Should().Be(0, "a visit to a revoked link is not an access");
            row.RevokedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task RevokeShareLink_VisitedWhileTheRevocationIsInFlight_KeepsTheVisit()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);
        HttpStatusCode? visitStatus = null;
        _interleaving.AfterNextRead(link.Id, async () =>
        {
            using var response = await VisitAsync(link.Token);
            visitStatus = response.StatusCode;
        });

        var revokeStatus = await RevokeAsync(link.Id, owner);

        _interleaving.IsPending(link.Id).Should().BeFalse("setup: the visit must run inside the revocation");
        visitStatus.Should().Be(HttpStatusCode.OK, "the link was still active when the visit arrived");
        revokeStatus.Should().Be(HttpStatusCode.OK);

        var row = await ReadRowAsync(link.Id);
        using (new AssertionScope())
        {
            row.RevokedAt.Should().NotBeNull();
            row.AccessCount.Should().Be(1,
                "the owner's write does not touch the visit counters. A revocation that writes back the count it read would leave 0");
            row.LastAccessedAt.Should().NotBeNull("the last access is the visit's, and the revocation must not erase it");
        }
    }

    [Fact]
    public async Task RevokeShareLink_RevokedTwiceAtTheSameTime_BothAnswer200AndTheFirstRevocationStays()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);
        HttpStatusCode? otherRevoke = null;
        DateTime? revokedByTheFirst = null;
        _interleaving.AfterNextRead(link.Id, async () =>
        {
            otherRevoke = await RevokeAsync(link.Id, owner);
            revokedByTheFirst = (await ReadRowAsync(link.Id)).RevokedAt;
        });

        var revoke = await RevokeAsync(link.Id, owner);

        _interleaving.IsPending(link.Id).Should().BeFalse("setup: the second revocation must run inside the first");
        otherRevoke.Should().Be(HttpStatusCode.OK, "setup: the other revocation completes first");
        revokedByTheFirst.Should().NotBeNull("setup: the other revocation has written revoked_at");

        // La prima revoca ha letto un link attivo e lo trova revocato quando scrive. Il risultato che voleva
        // c'è già, e una revoca ripetuta non è un errore: ma non deve nemmeno riscrivere l'istante della revoca.
        var row = await ReadRowAsync(link.Id);
        using var visit = await VisitAsync(link.Token);
        using (new AssertionScope())
        {
            revoke.Should().Be(HttpStatusCode.OK, "the link is revoked, which is what both requests asked for");
            row.RevokedAt.Should().Be(revokedByTheFirst, "a revocation is never rewritten, not even by a second revocation");
            visit.StatusCode.Should().Be(HttpStatusCode.NotFound, "the revoked link is blocked");
        }
    }

    [Fact]
    public async Task RevokeShareLink_RevokedAgainAfterTheFirstRevocation_AnswersLikeTheFirstAndWritesTheBlacklistKey()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);
        var first = await RevokeAsync(link.Id, owner);
        var revokedAt = (await ReadRowAsync(link.Id)).RevokedAt;

        // La prima revoca è nel database ma Redis ha fallito: la chiave non c'è. Il proprietario riprova.
        await RemoveBlacklistKeyAsync(link.Id);
        var second = await RevokeAsync(link.Id, owner);

        var row = await ReadRowAsync(link.Id);
        using var visit = await VisitAsync(link.Token);
        using (new AssertionScope())
        {
            first.Should().Be(HttpStatusCode.OK);
            second.Should().Be(HttpStatusCode.OK, "a repeated revocation is not an error. Without handling it the answer is 500");
            row.RevokedAt.Should().Be(revokedAt, "a revocation is never rewritten, not even by a repeated one");
            (await ReadBlacklistKeyAsync(link.Id)).Should().NotBeNull("the retry must write the key the first attempt lost");
            visit.StatusCode.Should().Be(HttpStatusCode.NotFound, "the revoked link is blocked");
        }
    }

    // ========================================
    // REPOSITORY: SCRITTURE DEL PROPRIETARIO E CONTEGGIO DELLA VISITA
    // ========================================
    //
    // Gli stessi difetti, un livello sotto l'handler: il proprietario legge l'aggregato, un'altra richiesta
    // cambia la riga, il proprietario scrive. Ogni scope è una richiesta, con il suo DbContext.

    [Fact]
    public async Task UpdateAsync_OnALinkRevokedAfterTheRead_IsRefusedAndTheRevocationStays()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);
        var before = await ReadRowAsync(link.Id);

        using var ownerRequest = _factory.Services.CreateScope();
        var repository = ownerRequest.ServiceProvider.GetRequiredService<IShareLinkRepository>();
        var aggregate = await repository.GetByIdAsync(link.Id);
        aggregate.Should().NotBeNull();

        (await RevokeAsync(link.Id, owner)).Should().Be(HttpStatusCode.OK, "setup: another request revokes the link after the read");

        aggregate!.UpdateExpiration(DateTime.UtcNow.AddDays(30));
        var write = () => repository.UpdateAsync(aggregate);

        await write.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the owner read a link that was not revoked, and the link was revoked before the write: writing it " +
            "back would set revoked_at to NULL again");

        var after = await ReadRowAsync(link.Id);
        using (new AssertionScope())
        {
            after.RevokedAt.Should().NotBeNull("a revocation is never undone");
            after.ExpiresAt.Should().Be(before.ExpiresAt, "the refused write changes nothing");
        }
    }

    [Fact]
    public async Task UpdateAsync_DoesNotWriteBackTheVisitCounters()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);

        using var ownerRequest = _factory.Services.CreateScope();
        var repository = ownerRequest.ServiceProvider.GetRequiredService<IShareLinkRepository>();
        var aggregate = await repository.GetByIdAsync(link.Id);
        aggregate!.AccessCount.Should().Be(0, "setup: the owner reads the link before any visit");

        using (var visit = await VisitAsync(link.Token))
        {
            visit.StatusCode.Should().Be(HttpStatusCode.OK, "setup: a visit is counted after the owner's read");
        }

        var newExpiry = DateTime.UtcNow.AddDays(30);
        aggregate.UpdateExpiration(newExpiry);
        await repository.UpdateAsync(aggregate);

        var after = await ReadRowAsync(link.Id);
        using (new AssertionScope())
        {
            after.ExpiresAt.Should().BeCloseTo(newExpiry, TimeSpan.FromSeconds(1), "the owner's change is saved");
            after.AccessCount.Should().Be(1,
                "the counters belong to the visit (TryRecordAccessAsync): the owner's write must not bring them back to what it read");
            after.LastAccessedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task TryRecordAccessAsync_ValidLink_CountsAndRevokedLinkDoesNot()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);

        using var request = _factory.Services.CreateScope();
        var repository = request.ServiceProvider.GetRequiredService<IShareLinkRepository>();

        var valid = await repository.TryRecordAccessAsync(link.Id);
        var afterValid = await ReadRowAsync(link.Id);

        (await RevokeAsync(link.Id, owner)).Should().Be(HttpStatusCode.OK, "setup: the owner revokes the link");
        var revoked = await repository.TryRecordAccessAsync(link.Id);
        var afterRevoked = await ReadRowAsync(link.Id);

        using (new AssertionScope())
        {
            valid.Should().BeTrue();
            afterValid.AccessCount.Should().Be(1);
            afterValid.LastAccessedAt.Should().NotBeNull();
            revoked.Should().BeFalse("a revoked link is not visited");
            afterRevoked.AccessCount.Should().Be(1, "a visit to a revoked link is not counted");
            afterRevoked.LastAccessedAt.Should().Be(afterValid.LastAccessedAt);
        }
    }

    [Fact]
    public async Task TryRecordAccessAsync_ExpiredOrUnknownLink_DoesNotCount()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner);
        await ExpireAsync(link.Id);

        using var request = _factory.Services.CreateScope();
        var repository = request.ServiceProvider.GetRequiredService<IShareLinkRepository>();

        var expired = await repository.TryRecordAccessAsync(link.Id);
        var unknown = await repository.TryRecordAccessAsync(Guid.NewGuid());
        var row = await ReadRowAsync(link.Id);

        using (new AssertionScope())
        {
            expired.Should().BeFalse("an expired link is not visited");
            unknown.Should().BeFalse("there is no link to count");
            row.AccessCount.Should().Be(0);
            row.LastAccessedAt.Should().BeNull();
        }
    }

    // ========================================
    // AIUTI
    // ========================================

    private sealed record OwnerSession(Guid UserId, string Token);

    private sealed record CreatedLink(Guid Id, string Token);

    private async Task<OwnerSession> CreateOwnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, token) = await TestSessionHelper.CreateUserSessionAsync(db);
        return new OwnerSession(userId, token);
    }

    /// <summary>
    /// Crea la chat nel database e il link dall'endpoint: la riga del link è quella che il sistema scrive,
    /// non una costruita a mano.
    /// </summary>
    private async Task<CreatedLink> CreateShareLinkAsync(OwnerSession owner)
    {
        var threadId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            db.ChatThreads.Add(new ChatThreadEntity { Id = threadId, UserId = owner.UserId, Title = "Chat condivisa" });
            await db.SaveChangesAsync();
        }

        using var request = TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Post, "/api/v1/share-links", owner.Token);
        request.Content = new StringContent(
            $$"""{"threadId":"{{threadId}}","role":"view","expiryDays":7}""", Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"setup: the share link must be created. Body: {body}");

        var root = JsonDocument.Parse(body).RootElement;
        return new CreatedLink(root.GetProperty("shareLinkId").GetGuid(), root.GetProperty("token").GetString()!);
    }

    private async Task<HttpResponseMessage> VisitAsync(string token) =>
        await _client.GetAsync($"/api/v1/shared/thread?token={Uri.EscapeDataString(token)}");

    private async Task<HttpStatusCode> RevokeAsync(Guid shareLinkId, OwnerSession owner)
    {
        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/api/v1/share-links/{shareLinkId}", owner.Token);
        using var response = await _client.SendAsync(request);
        return response.StatusCode;
    }

    private async Task ExpireAsync(Guid shareLinkId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var updated = await db.ShareLinks
            .Where(l => l.Id == shareLinkId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresAt, yesterday));
        updated.Should().Be(1, "setup: exactly the link of this test must expire");
    }

    private async Task RemoveBlacklistKeyAsync(Guid shareLinkId)
    {
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        var key = $"revoked_share_link:{shareLinkId}";
        (await cache.GetStringAsync(key)).Should().NotBeNull("setup: the revocation must have put the link in the blacklist");
        await cache.RemoveAsync(key);
    }

    private async Task<string?> ReadBlacklistKeyAsync(Guid shareLinkId)
    {
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        return await cache.GetStringAsync($"revoked_share_link:{shareLinkId}");
    }

    private async Task<ShareLinkEntity> ReadRowAsync(Guid shareLinkId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.ShareLinks.AsNoTracking().SingleAsync(l => l.Id == shareLinkId);
    }
}
