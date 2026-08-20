using Api.BoundedContexts.Testing.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.Testing;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-8) — Integration tests for
/// SeedTestPlayerCommandHandler.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "1928")]
public sealed class SeedTestPlayerCommandHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private WebApplicationFactory<Program>? _factory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SeedTestPlayerCommandHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_seed_player_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.EnsureCreatedAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task<(Guid gameNightId, Guid organizerId)> SeedParentGameNightAsync(MeepleAiDbContext db, string testRunId)
    {
        var gnHandler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        var gnResponse = await gnHandler.Handle(new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Published",
            OwnerEmail = $"owner-{testRunId[..16]}@e2e.test",
        }, TestCancellationToken);
        return (gnResponse.GameNightId, gnResponse.OwnerId);
    }

    [Fact]
    public async Task Handle_HostRole_ReturnsOrganizerId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-hostrole01234-1717603200000";
        var (gameNightId, organizerId) = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestPlayerCommandHandler(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            Role = "host",
        };

        var response = await handler.Handle(cmd, TestCancellationToken);

        response.PlayerId.Should().Be(organizerId);
        response.Role.Should().Be("host");
        response.IsGuest.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PlayerRole_CreatesUserLinkedRsvpAccepted()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-playerrole012-1717603200000";
        var (gameNightId, _) = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestPlayerCommandHandler(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            Role = "player",
        };

        var response = await handler.Handle(cmd, TestCancellationToken);

        var rsvp = await db.Set<GameNightRsvpEntity>()
            .SingleOrDefaultAsync(r => r.EventId == gameNightId && r.UserId == response.PlayerId, TestCancellationToken);
        rsvp.Should().NotBeNull();
        rsvp!.Status.Should().Be("Accepted");
        rsvp.TestRunId.Should().Be(testRunId);
        response.IsGuest.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_GuestRole_CreatesGuestInvitationStub()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-guestrole0123-1717603200000";
        var (gameNightId, _) = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestPlayerCommandHandler(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            Role = "guest",
            DisplayName = "E2E Guest Name",
        };

        var response = await handler.Handle(cmd, TestCancellationToken);

        var invitation = await db.Set<GameNightInvitationEntity>()
            .SingleOrDefaultAsync(i => i.Id == response.PlayerId, TestCancellationToken);
        invitation.Should().NotBeNull();
        invitation!.RespondedByName.Should().Be("E2E Guest Name");
        invitation.RespondedByUserId.Should().BeNull();
        invitation.TestRunId.Should().Be(testRunId);
        response.IsGuest.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_GameNightNotFound_ThrowsNotFoundException()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new SeedTestPlayerCommandHandler(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = "e2e-notfound00001-1717603200000",
            GameNightId = Guid.NewGuid(),
            Role = "player",
        };

        Func<Task> act = async () => await handler.Handle(cmd, TestCancellationToken);

        await act.Should().ThrowAsync<Api.Middleware.Exceptions.NotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_PlayerRole_WithExplicitUserId_UsesProvidedUserId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-explicituser0-1717603200000";
        var (gameNightId, organizerId) = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestPlayerCommandHandler(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            Role = "player",
            UserId = organizerId,
        };

        var response = await handler.Handle(cmd, TestCancellationToken);

        response.PlayerId.Should().Be(organizerId);
    }

    [Fact]
    public async Task Handle_ResponseShape_CorrectFields()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-shapecase0123-1717603200000";
        var (gameNightId, _) = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestPlayerCommandHandler(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);
        var cmd = new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            Role = "guest",
            DisplayName = "ShapeGuest",
        };

        var response = await handler.Handle(cmd, TestCancellationToken);

        response.GameNightId.Should().Be(gameNightId);
        response.Role.Should().Be("guest");
        response.IsGuest.Should().BeTrue();
        response.TestRunId.Should().Be(testRunId);
    }
}
