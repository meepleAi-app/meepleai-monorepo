using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AddCommentToSharedThread;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Il commento a una chat condivisa guarda la riga del link, non solo il token (#4592).
/// </summary>
/// <remarks>
/// <para>
/// <c>AddCommentToSharedThreadCommandHandler</c> decideva con <c>ValidateShareLinkQuery</c>: firma del JWT,
/// scadenza del token, blacklist Redis. Non leggeva mai <c>share_links</c>. Dopo #4573 la visita tratta la
/// riga come verità, e la scrittura no: un link revocato o scaduto nel database, con la chiave Redis persa,
/// rispondeva 404 alla visita e accettava il commento nella chat del proprietario.
/// </para>
/// <para>
/// Qui il database è la verità. Ogni test porta il link in uno stato che solo la riga conosce (il token
/// firmato resta valido, la chiave nella blacklist non c'è) e controlla due cose: la risposta, e che nella
/// chat non sia stato scritto nulla.
/// </para>
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4592")]
public sealed class SharedThreadCommentLinkValidityIntegrationTests : IClassFixture<SharedThreadVisitHostFixture>
{
    private const string CommentText = "Bel gioco, lo riprovo sabato";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SharedThreadCommentLinkValidityIntegrationTests(SharedThreadVisitHostFixture host)
    {
        _factory = host.Factory;
        _client = host.Client;
    }

    [Fact]
    public async Task AddComment_ActiveCommentLink_AcceptsAndWritesTheComment()
    {
        // La guardia: senza questo test, un controllo che rifiuta tutto farebbe passare gli altri.
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner, "comment");

        using var response = await CommentAsync(link.Token);
        var body = await response.Content.ReadAsStringAsync();

        var messages = await ReadMessagesJsonAsync(link.ThreadId);
        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"a valid comment link accepts comments. Body: {body}");
            messages.Should().Contain(CommentText, "the comment is written in the owner's chat");
        }
    }

    [Fact]
    public async Task AddComment_RevokedInTheDatabaseWithoutTheBlacklistKey_IsRefusedAndWritesNothing()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner, "comment");
        (await RevokeAsync(link.Id, owner)).Should().Be(HttpStatusCode.OK, "setup: the owner revokes the link");

        // La chiave Redis si può perdere (riavvio, eviction, cache in memoria, scrittura fallita). La riga resta revocata.
        await RemoveBlacklistKeyAsync(link.Id);

        // Stessa condizione, vista dalla visita: è il confronto che dice cosa deve rispondere il commento.
        using var visit = await _client.GetAsync($"/api/v1/shared/thread?token={Uri.EscapeDataString(link.Token)}");
        using var response = await CommentAsync(link.Token);
        var body = await response.Content.ReadAsStringAsync();

        var messages = await ReadMessagesJsonAsync(link.ThreadId);
        using (new AssertionScope())
        {
            visit.StatusCode.Should().Be(HttpStatusCode.NotFound, "setup: the visit already treats the row as the truth (#4573)");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                $"the row says revoked: losing the blacklist key must not let the comment through. Body: {body}");
            messages.Should().NotContain(CommentText, "a refused comment is not written in the owner's chat");
            MessageCount(messages).Should().Be(0);
        }
    }

    [Fact]
    public async Task AddComment_ExpiredInTheDatabase_IsRefusedAndWritesNothing()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner, "comment");

        // Il token firmato non è scaduto e la blacklist non lo conosce: a fermare il commento può essere
        // solo la scadenza scritta sulla riga.
        await ExpireAsync(link.Id);

        using var response = await CommentAsync(link.Token);
        var body = await response.Content.ReadAsStringAsync();

        var messages = await ReadMessagesJsonAsync(link.ThreadId);
        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                $"the row says expired: the signed token alone must not let the comment through. Body: {body}");
            messages.Should().NotContain(CommentText, "a refused comment is not written in the owner's chat");
            MessageCount(messages).Should().Be(0);
        }
    }

    [Fact]
    public async Task AddComment_ViewLink_Answers403AndWritesNothing()
    {
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner, "view");

        using var response = await CommentAsync(link.Token);
        var body = await response.Content.ReadAsStringAsync();

        var messages = await ReadMessagesJsonAsync(link.ThreadId);
        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                $"a view link is a valid link without the right to comment: forbidden, not a server error. Body: {body}");
            body.Should().Contain("\"error\":\"forbidden\"", "the exception middleware answered, not a hand-written catch");
            messages.Should().NotContain(CommentText);
            MessageCount(messages).Should().Be(0);
        }
    }

    [Fact]
    public async Task AddComment_ViewLinkRevokedInTheDatabase_AnswersLikeAnyDeadLink()
    {
        // Un link morto risponde allo stesso modo qualunque sia il ruolo: il ruolo di un link che non vale
        // più non è una cosa che la risposta debba dire.
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner, "view");
        (await RevokeAsync(link.Id, owner)).Should().Be(HttpStatusCode.OK, "setup: the owner revokes the link");
        await RemoveBlacklistKeyAsync(link.Id);

        using var response = await CommentAsync(link.Token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddComment_HourlyLimitReached_Answers429AndWritesNothing()
    {
        // Guardia sul percorso del rate limit: l'endpoint lo traduceva da solo, ora lo fa il middleware.
        var owner = await CreateOwnerAsync();
        var link = await CreateShareLinkAsync(owner, "comment");
        await SetCommentCountAsync(link.Id, AddCommentToSharedThreadCommandHandler.MaxCommentsPerHour);

        using var response = await CommentAsync(link.Token);
        var body = await response.Content.ReadAsStringAsync();

        var messages = await ReadMessagesJsonAsync(link.ThreadId);
        using (new AssertionScope())
        {
            response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, $"the link reached the hourly limit. Body: {body}");
            body.Should().Contain("\"error\":\"too_many_requests\"", "the exception middleware answered, not a hand-written catch");
            messages.Should().NotContain(CommentText);
            MessageCount(messages).Should().Be(0);
        }
    }

    // ========================================
    // REPOSITORY: IsActiveAsync
    // ========================================

    [Fact]
    public async Task IsActiveAsync_ReflectsTheRow_ActiveRevokedExpiredUnknown()
    {
        var owner = await CreateOwnerAsync();
        var active = await CreateShareLinkAsync(owner, "comment");
        var revoked = await CreateShareLinkAsync(owner, "comment");
        var expired = await CreateShareLinkAsync(owner, "comment");
        (await RevokeAsync(revoked.Id, owner)).Should().Be(HttpStatusCode.OK, "setup: the owner revokes one link");
        await ExpireAsync(expired.Id);

        using var request = _factory.Services.CreateScope();
        var repository = request.ServiceProvider.GetRequiredService<IShareLinkRepository>();

        var isActive = await repository.IsActiveAsync(active.Id);
        var isRevokedActive = await repository.IsActiveAsync(revoked.Id);
        var isExpiredActive = await repository.IsActiveAsync(expired.Id);
        var isUnknownActive = await repository.IsActiveAsync(Guid.NewGuid());

        // Il controllo non è una visita: non conta e non scrive.
        var row = await ReadRowAsync(active.Id);

        using (new AssertionScope())
        {
            isActive.Should().BeTrue();
            isRevokedActive.Should().BeFalse("a revoked link is not active");
            isExpiredActive.Should().BeFalse("an expired link is not active");
            isUnknownActive.Should().BeFalse("there is no link to be active");
            row.AccessCount.Should().Be(0, "looking at the row is not an access");
            row.LastAccessedAt.Should().BeNull();
        }
    }

    // ========================================
    // AIUTI
    // ========================================

    private sealed record OwnerSession(Guid UserId, string Token);

    private sealed record CreatedLink(Guid Id, Guid ThreadId, string Token);

    private async Task<OwnerSession> CreateOwnerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, token) = await TestSessionHelper.CreateUserSessionAsync(db);
        return new OwnerSession(userId, token);
    }

    /// <summary>
    /// Crea la chat nel database e il link dall'endpoint: la riga del link è quella che il sistema scrive,
    /// non una costruita a mano. Ogni link ha la sua chat, quindi i messaggi di un test non sono di un altro.
    /// </summary>
    private async Task<CreatedLink> CreateShareLinkAsync(OwnerSession owner, string role)
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
            $$"""{"threadId":"{{threadId}}","role":"{{role}}","expiryDays":7}""", Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"setup: the share link must be created. Body: {body}");

        var root = JsonDocument.Parse(body).RootElement;
        return new CreatedLink(root.GetProperty("shareLinkId").GetGuid(), threadId, root.GetProperty("token").GetString()!);
    }

    private async Task<HttpResponseMessage> CommentAsync(string token)
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { token, content = CommentText }), Encoding.UTF8, "application/json");
        return await _client.PostAsync("/api/v1/shared/thread/comment", content);
    }

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

    private async Task SetCommentCountAsync(Guid shareLinkId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        await cache.SetStringAsync(
            $"share_link_comments:{shareLinkId}",
            count.ToString(CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
    }

    private async Task<string> ReadMessagesJsonAsync(Guid threadId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var thread = await db.ChatThreads.AsNoTracking().SingleAsync(t => t.Id == threadId);
        return thread.MessagesJson;
    }

    private static int MessageCount(string messagesJson) =>
        JsonDocument.Parse(messagesJson).RootElement.GetArrayLength();

    private async Task<ShareLinkEntity> ReadRowAsync(Guid shareLinkId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.ShareLinks.AsNoTracking().SingleAsync(l => l.Id == shareLinkId);
    }
}
