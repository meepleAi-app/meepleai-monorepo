using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Handler-driven integration tests for the game-night summary + share-token + archive flow
/// through the MediatR pipeline — Issue #2702 (summary aggregation, IDOR, share-token, archive).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2702")]
public sealed class GameNightSummaryCommandTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider? _serviceProvider;

    public GameNightSummaryCommandTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider Sp => _serviceProvider ?? throw new InvalidOperationException("SP not initialized");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamenight_summary_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(Ct);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_connectionString);
        services.AddScoped<IGameNightEventRepository, GameNightEventRepository>();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable d) await d.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task<(Guid EventId, Guid Organizer, Guid Participant)> SeedAsync(string status = "Completed")
    {
        var eventId = Guid.NewGuid();
        var organizer = Guid.NewGuid();
        var participant = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = organizer,
            Title = "Serata riepilogo",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1),
            GameIdsJson = JsonSerializer.Serialize(new List<Guid> { gameId }),
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            Rsvps =
            {
                new GameNightRsvpEntity
                {
                    Id = Guid.NewGuid(), EventId = eventId, UserId = participant,
                    Status = "Accepted", CreatedAt = DateTimeOffset.UtcNow
                }
            },
            Sessions =
            {
                new GameNightSessionEntity
                {
                    Id = Guid.NewGuid(), GameNightEventId = eventId, SessionId = Guid.NewGuid(),
                    GameId = gameId, GameTitle = "Brass", PlayOrder = 1, Status = "Completed",
                    StartedAt = DateTimeOffset.UtcNow.AddHours(-3),
                    CompletedAt = DateTimeOffset.UtcNow.AddHours(-1)
                }
            }
        });
        await _dbContext.SaveChangesAsync(Ct);
        return (eventId, organizer, participant);
    }

    private async Task SendAsync(IRequest request)
    {
        using var scope = Sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, Ct);
    }

    private async Task<TResult> SendAsync<TResult>(IRequest<TResult> request)
    {
        using var scope = Sp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request, Ct);
    }

    [Fact(DisplayName = "Participant reads the summary with KPIs")]
    public async Task Summary_ByParticipant_ReturnsKpis()
    {
        var (eventId, _, participant) = await SeedAsync();

        var summary = await SendAsync(new GetGameNightSummaryQuery(eventId, participant));

        summary.Id.Should().Be(eventId);
        summary.Kpis.TotalGames.Should().Be(1);
        summary.Kpis.CompletedGames.Should().Be(1);
        summary.IsViewerOrganizer.Should().BeFalse();
        summary.ShareToken.Should().BeNull(); // organiser-only field
    }

    [Fact(DisplayName = "IDOR: a non-participant cannot read the summary (403)")]
    public async Task Summary_ByNonParticipant_Throws403()
    {
        var (eventId, _, _) = await SeedAsync();

        var act = async () => await SendAsync(new GetGameNightSummaryQuery(eventId, Guid.NewGuid()));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact(DisplayName = "Organizer generates a share token; the shared summary is publicly readable")]
    public async Task ShareToken_GeneratedByOrganizer_EnablesPublicRead()
    {
        var (eventId, organizer, _) = await SeedAsync();

        var token = await SendAsync(new GenerateGameNightShareTokenCommand(eventId, organizer));
        token.Should().NotBeNullOrWhiteSpace();

        var shared = await SendAsync(new GetGameNightSummaryByShareTokenQuery(token));
        shared.Id.Should().Be(eventId);
        shared.ShareToken.Should().BeNull(); // the public view never re-exposes the token

        // After revoke the token no longer resolves.
        await SendAsync(new RevokeGameNightShareTokenCommand(eventId, organizer));
        var act = async () => await SendAsync(new GetGameNightSummaryByShareTokenQuery(token));
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact(DisplayName = "IDOR: a non-organizer cannot generate a share token (403)")]
    public async Task ShareToken_ByNonOrganizer_Throws403()
    {
        var (eventId, _, participant) = await SeedAsync();

        var act = async () => await SendAsync(new GenerateGameNightShareTokenCommand(eventId, participant));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact(DisplayName = "Organizer archives a completed night; summary reflects it")]
    public async Task Archive_CompletedNight_ReflectedInSummary()
    {
        var (eventId, organizer, _) = await SeedAsync();

        await SendAsync(new SetGameNightArchivedCommand(eventId, organizer, Archived: true));

        var summary = await SendAsync(new GetGameNightSummaryQuery(eventId, organizer));
        summary.IsArchived.Should().BeTrue();
    }

    [Fact(DisplayName = "Archiving a non-completed night returns 409")]
    public async Task Archive_NonCompletedNight_Throws409()
    {
        var (eventId, organizer, _) = await SeedAsync(status: "Published");

        var act = async () => await SendAsync(new SetGameNightArchivedCommand(eventId, organizer, Archived: true));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact(DisplayName = "IDOR: a non-organizer cannot archive (403)")]
    public async Task Archive_ByNonOrganizer_Throws403()
    {
        var (eventId, _, participant) = await SeedAsync();

        var act = async () => await SendAsync(new SetGameNightArchivedCommand(eventId, participant, Archived: true));

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
