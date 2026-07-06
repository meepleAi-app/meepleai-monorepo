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
/// Handler-driven integration tests for candidate voting through the MediatR pipeline — Issue #2700.
/// Exercises the real command/query flow (guards + persistence + IDOR) rather than fixture-only DTOs.
/// Voting open/closed is controlled purely via ScheduledAt relative to real UtcNow (closes at -1h).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2700")]
public sealed class GameNightVotingCommandTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider? _serviceProvider;

    public GameNightVotingCommandTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider Sp => _serviceProvider ?? throw new InvalidOperationException("SP not initialized");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamenight_vote_cmd_{Guid.NewGuid():N}";
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

    private async Task<(Guid EventId, Guid Organizer, Guid Voter, Guid GameA, Guid GameB)> SeedAsync(
        DateTimeOffset scheduledAt)
    {
        var eventId = Guid.NewGuid();
        var organizer = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();

        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = organizer,
            Title = "Voting Night",
            ScheduledAt = scheduledAt,
            GameIdsJson = JsonSerializer.Serialize(new List<Guid> { gameA, gameB }),
            Status = "Published",
            CreatedAt = DateTimeOffset.UtcNow,
            Rsvps =
            {
                new GameNightRsvpEntity
                {
                    Id = Guid.NewGuid(), EventId = eventId, UserId = voter,
                    Status = "Accepted", CreatedAt = DateTimeOffset.UtcNow
                }
            }
        });
        await _dbContext.SaveChangesAsync(Ct);
        return (eventId, organizer, voter, gameA, gameB);
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

    [Fact(DisplayName = "Confirmed participant casts a vote; tally reflects it with VotedByMe")]
    public async Task Cast_ByConfirmedParticipant_ReflectedInTally()
    {
        var (eventId, _, voter, gameA, _) = await SeedAsync(DateTimeOffset.UtcNow.AddDays(2));

        await SendAsync(new CastGameNightVoteCommand(eventId, voter, gameA));

        var tally = await SendAsync(new GetGameNightVoteTallyQuery(eventId, voter));
        tally.Candidates.Single(c => c.GameId == gameA).VoteCount.Should().Be(1);
        tally.Candidates.Single(c => c.GameId == gameA).VotedByMe.Should().BeTrue();
        tally.WinnerGameId.Should().Be(gameA);
        tally.IsTie.Should().BeFalse();
    }

    [Fact(DisplayName = "IDOR: a non-confirmed user cannot cast a vote (403)")]
    public async Task Cast_ByNonConfirmedUser_Throws403()
    {
        var (eventId, _, _, gameA, _) = await SeedAsync(DateTimeOffset.UtcNow.AddDays(2));
        var stranger = Guid.NewGuid();

        var act = async () => await SendAsync(new CastGameNightVoteCommand(eventId, stranger, gameA));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact(DisplayName = "IDOR: a non-participant cannot read the vote tally (403)")]
    public async Task Tally_ByNonParticipant_Throws403()
    {
        var (eventId, _, _, _, _) = await SeedAsync(DateTimeOffset.UtcNow.AddDays(2));
        var stranger = Guid.NewGuid();

        var act = async () => await SendAsync(new GetGameNightVoteTallyQuery(eventId, stranger));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact(DisplayName = "Retract removes the vote from the tally")]
    public async Task Retract_RemovesVote()
    {
        var (eventId, _, voter, gameA, _) = await SeedAsync(DateTimeOffset.UtcNow.AddDays(2));
        await SendAsync(new CastGameNightVoteCommand(eventId, voter, gameA));

        await SendAsync(new RetractGameNightVoteCommand(eventId, voter, gameA));

        var tally = await SendAsync(new GetGameNightVoteTallyQuery(eventId, voter));
        tally.Candidates.Single(c => c.GameId == gameA).VoteCount.Should().Be(0);
    }

    [Fact(DisplayName = "Casting after voting has closed returns 409")]
    public async Task Cast_AfterClose_Throws409()
    {
        // ScheduledAt 30 minutes out → now is already past ScheduledAt - 1h → closed.
        var (eventId, _, voter, gameA, _) = await SeedAsync(DateTimeOffset.UtcNow.AddMinutes(30));

        var act = async () => await SendAsync(new CastGameNightVoteCommand(eventId, voter, gameA));

        await act.Should().ThrowAsync<ConflictException>();
    }
}
