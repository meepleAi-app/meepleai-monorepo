using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// #4566 — gli eventi di dominio di <c>SharedGame</c> arrivano all'outbox.
/// </summary>
/// <remarks>
/// <para>
/// Il difetto. <c>SharedGameRepository</c> non raccoglieva mai gli eventi che l'aggregato registra
/// (<c>CollectDomainEvents</c> in <c>AddAsync</c> e <c>Update</c>), e <c>MeepleAiDbContext.SaveChangesAsync</c>
/// scrive in <c>domain_event_outbox</c> solo quelli del collector: un evento registrato restava nella lista
/// dell'aggregato e si perdeva con lui. Nessun handler li vedeva: né l'audit del catalogo
/// (<c>SharedGameCatalogAuditEventHandler</c>) e quello generico (<c>DomainEventAuditHandler</c>), né la
/// notifica agli admin all'invio in approvazione (<c>SharedGameSubmittedForApprovalNotificationHandler</c>).
/// </para>
/// <para>
/// Come si legge ogni test. Un comando HTTP cambia il catalogo; poi si cerca in <c>domain_event_outbox</c> la
/// riga che quell'evento deve aver lasciato, per quel gioco. Il punto d'osservazione è l'outbox e non
/// <c>audit_outbox</c> perché l'host di test è in modalità <c>OutboxOnly</c> e senza processore: nessun handler
/// gira, quindi nessuna riga di audit e nessuna notifica. Che la consegna dall'outbox agli handler funzioni,
/// qui non si prova (si prova su staging).
/// </para>
/// </remarks>
public sealed partial class SharedGameCatalogEndpointsIntegrationTests
{
    [Fact]
    public async Task CreateGame_EnqueuesTheCreatedEvent()
    {
        var admin = await CreateAdminTokenAsync();

        using var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/admin/shared-games",
            admin,
            new
            {
                Title = $"Gioco degli eventi {Guid.NewGuid():N}",
                YearPublished = 2024,
                Description = "Per provare l'outbox",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                MinAge = 10,
                ComplexityRating = 2.5m,
                AverageRating = 7.5m,
                ImageUrl = "https://example.com/a.jpg",
                ThumbnailUrl = "https://example.com/a-thumb.jpg",
                Rules = (object?)null,
                BggId = (int?)null,
            });
        using var response = await NewCookielessClient().SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var gameId = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);

        (await EnqueuedEventsOfAsync(gameId)).Should().Contain(nameof(SharedGameCreatedEvent));
    }

    [Fact]
    public async Task UpdateGame_EnqueuesTheUpdatedEvent()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        await SendAdminAsync(
            admin,
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}",
            HttpStatusCode.NoContent,
            new
            {
                Title = $"{game.Title} (rivisto)",
                YearPublished = 2024,
                Description = "Descrizione rivista",
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                MinAge = 10,
                ComplexityRating = (decimal?)null,
                AverageRating = (decimal?)null,
                ImageUrl = "https://example.com/image.jpg",
                ThumbnailUrl = "https://example.com/thumb.jpg",
                Rules = (object?)null,
            });

        (await EnqueuedEventsOfAsync(game.Id)).Should().Contain(nameof(SharedGameUpdatedEvent));
    }

    [Fact]
    public async Task ArchiveGame_EnqueuesTheArchivedEventExactlyOnce()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/archive", HttpStatusCode.NoContent);

        (await EnqueuedEventsOfAsync(game.Id)).Should().ContainSingle(e => e == nameof(SharedGameArchivedEvent));
    }

    [Fact]
    public async Task DeleteGame_EnqueuesTheDeletedEvent()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        await SendAdminAsync(
            admin,
            HttpMethod.Delete,
            $"/api/v1/admin/shared-games/{game.Id}",
            HttpStatusCode.NoContent,
            new { reason = "per provare l'outbox" });

        (await EnqueuedEventsOfAsync(game.Id)).Should().Contain(nameof(SharedGameDeletedEvent));
    }

    [Fact]
    public async Task SubmitForApproval_EnqueuesTheEvent_ThatNotifiesTheAdmins()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Draft);

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/submit-for-approval", HttpStatusCode.NoContent);

        // L'evento che SharedGameSubmittedForApprovalNotificationHandler ascolta per avvisare gli admin.
        (await EnqueuedEventsOfAsync(game.Id)).Should().Contain(nameof(SharedGameSubmittedForApprovalEvent));
    }

    [Fact]
    public async Task ApprovePublication_EnqueuesTheApprovedEvent()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.PendingApproval);

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/approve-publication", HttpStatusCode.NoContent);

        (await EnqueuedEventsOfAsync(game.Id)).Should().Contain(nameof(SharedGamePublicationApprovedEvent));
    }

    [Fact]
    public async Task RejectPublication_EnqueuesTheRejectedEvent()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.PendingApproval);

        await SendAdminAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/reject-publication",
            HttpStatusCode.NoContent,
            new { Reason = "Serve una descrizione migliore" });

        (await EnqueuedEventsOfAsync(game.Id)).Should().Contain(nameof(SharedGamePublicationRejectedEvent));
    }

    [Fact]
    public async Task QuickPublish_EnqueuesTheQuickPublishedEvent()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Draft);

        await SendAdminAsync(admin, HttpMethod.Post, $"/api/v1/admin/shared-games/{game.Id}/quick-publish", HttpStatusCode.NoContent);

        (await EnqueuedEventsOfAsync(game.Id)).Should().Contain(nameof(SharedGameQuickPublishedEvent));
    }

    [Fact]
    public async Task AddErrata_EnqueuesTheErrataAddedEvent()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        await SendAdminAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/errata",
            HttpStatusCode.Created,
            new { description = "Il costo della carta è 3, non 2", pageReference = "p. 14", publishedDate = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc) });

        (await EnqueuedEventsOfAsync(game.Id)).Should().Contain(nameof(GameErrataAddedEvent));
    }

    [Fact]
    public async Task AddFaq_EnqueuesTheFaqAddedEvent()
    {
        var admin = await CreateAdminTokenAsync();
        var game = await CreateTestGameAsync(GameStatus.Published);

        await SendAdminAsync(
            admin,
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/faq",
            HttpStatusCode.Created,
            new { question = "Si può commerciare col banco?", answer = "Sì, 4 a 1.", order = 1 });

        (await EnqueuedEventsOfAsync(game.Id)).Should().Contain(nameof(GameFaqAddedEvent));
    }

    /// <summary>
    /// I nomi (di classe) degli eventi che <c>domain_event_outbox</c> ha in coda per quel gioco. Il tipo
    /// dell'evento è l'alias di <c>EventTypeRegistry</c> o, per questi eventi, il <c>FullName</c> del tipo; il
    /// payload è l'evento serializzato e porta l'id del gioco.
    /// </summary>
    private async Task<List<string>> EnqueuedEventsOfAsync(Guid gameId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var rows = await db.DomainEventOutbox.AsNoTracking()
            .Select(o => new { o.EventType, o.PayloadJson })
            .ToListAsync(TestContext.Current.CancellationToken);

        var id = gameId.ToString();
        return rows
            .Where(r => r.PayloadJson.Contains(id, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.EventType[(r.EventType.LastIndexOf('.') + 1)..])
            .ToList();
    }
}
