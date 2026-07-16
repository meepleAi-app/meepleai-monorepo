using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.UserNotifications;

/// <summary>
/// Integration tests for <see cref="NotificationQueueRepository"/> payload materialization against a
/// real Testcontainers PostgreSQL instance — the only harness that reproduces jsonb key reordering.
///
/// Regression guards for #3057: the <c>payload</c> jsonb column reorders object keys by
/// (length, then bytewise), moving the polymorphic <c>"$type"</c> discriminator out of first
/// position for <see cref="GenericPayload"/> ({"body":..,"$type":..,"title":..}). STJ rejected that,
/// so <see cref="NotificationQueueRepository.GetPendingByChannelAsync"/> threw for the whole batch,
/// <c>EmailNotificationProcessorJob</c> crash-looped, and one poison row blocked every email.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotificationQueueRepositoryPayloadResilienceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private INotificationQueueRepository? _repository;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public NotificationQueueRepositoryPayloadResilienceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_notifqueuepayload_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<INotificationQueueRepository, NotificationQueueRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<INotificationQueueRepository>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch (NpgsqlException)
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public async Task GetPendingByChannelAsync_GenericPayloadEmailItem_RoundTripsThroughJsonb()
    {
        // A GenericPayload serialized with "$type" first, stored in the jsonb column, comes back
        // reordered ("$type" no longer first). Post-#3057 it must still deserialize to GenericPayload.
        var id = Guid.NewGuid();
        await SeedRawAsync(RawEmailEntity(
            id,
            payload: Serialize(new GenericPayload("System Update", "Maintenance window at 2 AM"))));

        var pending = await _repository!.GetPendingByChannelAsync(
            NotificationChannelType.Email, 10, TestCancellationToken);

        pending.Should().ContainSingle();
        pending[0].Payload.Should().BeOfType<GenericPayload>();
        var payload = (GenericPayload)pending[0].Payload;
        payload.Title.Should().Be("System Update");
        payload.Body.Should().Be("Maintenance window at 2 AM");
    }

    [Fact]
    public async Task GetPendingByChannelAsync_PoisonRowAmongGoodRows_ReturnsGoodAndDeadLettersPoison()
    {
        // A row whose payload can never be deserialized (no discriminator at all) must not fail the
        // whole batch: the good row is returned and the poison row is dead-lettered so it stops being
        // re-queried every cycle.
        var poisonId = Guid.NewGuid();
        var goodId = Guid.NewGuid();
        await SeedRawAsync(
            RawEmailEntity(poisonId, payload: "{\"missing\":\"discriminator\"}"),
            RawEmailEntity(goodId, payload: Serialize(
                new BadgePayload(Guid.NewGuid(), "First Upload", "Uploaded your first PDF"))));

        var pending = await _repository!.GetPendingByChannelAsync(
            NotificationChannelType.Email, 10, TestCancellationToken);

        pending.Should().ContainSingle().Which.Id.Should().Be(goodId);

        var poisonRow = await _dbContext!.Set<NotificationQueueEntity>()
            .AsNoTracking()
            .FirstAsync(e => e.Id == poisonId, TestCancellationToken);
        poisonRow.Status.Should().Be("dead_letter");
    }

    private static string Serialize(INotificationPayload payload) =>
        JsonSerializer.Serialize(payload, NotificationPayloadSerializer.CreateOptions());

    private async Task SeedRawAsync(params NotificationQueueEntity[] items)
    {
        await _dbContext!.Set<NotificationQueueEntity>().AddRangeAsync(items, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    private static NotificationQueueEntity RawEmailEntity(Guid id, string payload)
    {
        return new NotificationQueueEntity
        {
            Id = id,
            ChannelType = NotificationChannelType.Email.Value,
            RecipientUserId = Guid.NewGuid(),
            NotificationType = "badge_earned",
            Payload = payload,
            Status = "pending",
            RetryCount = 0,
            MaxRetries = 3,
            NextRetryAt = null,
            CreatedAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            SourceEventId = null,
        };
    }
}
