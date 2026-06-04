using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for <see cref="AlertChannelRepository"/> using a real
/// Testcontainers PostgreSQL instance (Issue #1840 SP5 F4-C7).
///
/// Covers: insert-on-upsert, update-on-upsert, GetByType, GetAll, optimistic
/// concurrency via stale RowVersion.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1840")]
public sealed class AlertChannelRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IAlertChannelRepository? _repository;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AlertChannelRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_alertchannelrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped<IAlertChannelRepository, AlertChannelRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IAlertChannelRepository>();

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
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewChannel_WhenAbsent()
    {
        var channel = AlertChannel.Create(
            AlertChannelType.Slack,
            """{"webhookUrl":"https://hooks.slack.com/services/T/B/x","channel":"#alerts"}""",
            isEnabled: true,
            createdBy: "admin@meepleai.dev");

        await _repository!.UpsertAsync(channel, TestCancellationToken);

        var fetched = await _repository.GetByTypeAsync(AlertChannelType.Slack, TestCancellationToken);

        fetched.Should().NotBeNull();
        fetched!.Type.Should().Be(AlertChannelType.Slack);
        fetched.IsEnabled.Should().BeTrue();
        fetched.UpdatedBy.Should().Be("admin@meepleai.dev");
        fetched.RowVersion.Should().NotBeEmpty(
            "Postgres xmin must populate the concurrency token on insert");
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingChannel_WhenPresent()
    {
        var channel = AlertChannel.Create(AlertChannelType.Email, """{"smtpHost":"smtp1.example.com"}""", true, "admin");
        await _repository!.UpsertAsync(channel, TestCancellationToken);

        var loaded = await _repository.GetByTypeAsync(AlertChannelType.Email, TestCancellationToken);
        loaded!.UpdateConfig("""{"smtpHost":"smtp2.example.com"}""", isEnabled: false, "admin2");

        await _repository.UpsertAsync(loaded, TestCancellationToken);

        var refetched = await _repository.GetByTypeAsync(AlertChannelType.Email, TestCancellationToken);
        refetched.Should().NotBeNull();
        refetched!.ConfigJson.Should().Contain("smtp2.example.com");
        refetched.IsEnabled.Should().BeFalse();
        refetched.UpdatedBy.Should().Be("admin2");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllConfiguredChannels()
    {
        await _repository!.UpsertAsync(
            AlertChannel.Create(AlertChannelType.Email, """{"smtpHost":"smtp.example.com"}""", true, "admin"),
            TestCancellationToken);
        await _repository.UpsertAsync(
            AlertChannel.Create(AlertChannelType.Slack, """{"webhookUrl":"https://hooks.slack.com/X"}""", true, "admin"),
            TestCancellationToken);

        var all = await _repository.GetAllAsync(TestCancellationToken);

        all.Should().HaveCount(2);
        all.Select(c => c.Type).Should().BeEquivalentTo(new[] { AlertChannelType.Email, AlertChannelType.Slack });
    }

    [Fact]
    public async Task UpsertAsync_OnStaleRowVersion_ThrowsConcurrencyException()
    {
        // Admin A and Admin B both load the same channel.
        var initial = AlertChannel.Create(
            AlertChannelType.Slack,
            """{"webhookUrl":"https://hooks.slack.com/v1","channel":"#alerts"}""",
            true,
            "admin");
        await _repository!.UpsertAsync(initial, TestCancellationToken);

        var loadedByA = await _repository.GetByTypeAsync(AlertChannelType.Slack, TestCancellationToken);
        var loadedByB = await _repository.GetByTypeAsync(AlertChannelType.Slack, TestCancellationToken);

        // Admin A commits first → bumps RowVersion.
        loadedByA!.UpdateConfig("""{"webhookUrl":"https://hooks.slack.com/v2"}""", true, "adminA");
        await _repository.UpsertAsync(loadedByA, TestCancellationToken);

        // Admin B attempts to commit with the stale RowVersion captured BEFORE
        // Admin A's update.
        loadedByB!.UpdateConfig("""{"webhookUrl":"https://hooks.slack.com/v3"}""", true, "adminB");

        var act = async () => await _repository.UpsertAsync(loadedByB, TestCancellationToken);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task GetByTypeAsync_ReturnsNull_WhenChannelMissing()
    {
        var result = await _repository!.GetByTypeAsync(AlertChannelType.Slack, TestCancellationToken);
        result.Should().BeNull();
    }
}
