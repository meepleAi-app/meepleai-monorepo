using Api.BoundedContexts.WorkflowIntegration.Domain.Entities;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;
using Api.BoundedContexts.WorkflowIntegration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.WorkflowIntegration;

/// <summary>
/// Integration tests for n8n workflow execution with mocked webhooks.
/// Week 9: WorkflowIntegration n8n execution layer (5 tests)
/// Tests: Workflow configuration, activation/deactivation, connection testing, webhook validation
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "WorkflowIntegration")]
[Trait("Week", "9")]
public sealed class N8nWorkflowExecutionIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IN8NConfigurationRepository? _repository;

    private static readonly Guid TestUserId = new("91000000-0000-0000-0000-000000000001");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public N8nWorkflowExecutionIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_week9_workflow_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        // Fix: Use PostgreSQL DbContext with Testcontainers, not in-memory
        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        // Seed required User for FK constraints
        await SeedTestUserAsync();

        _repository = new N8NConfigurationRepository(_dbContext, mockEventCollector.Object);
    }

    private async Task SeedTestUserAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-week9-workflow@meepleai.dev",
            DisplayName = "Test User Week 9 Workflow",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task N8nConfig_Create_ShouldPersistConfiguration()
    {
        // Arrange
        var config = new N8NConfiguration(
            Guid.NewGuid(),
            "Production N8n",
            new WorkflowUrl("https://n8n.meepleai.dev"),
            "encrypted_api_key_abc123",
            TestUserId,
            new WorkflowUrl("https://webhook.meepleai.dev/hooks/workflow1")
        );

        // Act
        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Production N8n");
        retrieved.BaseUrl.Value.Should().Be("https://n8n.meepleai.dev");
        retrieved.IsActive.Should().BeTrue();
        retrieved.CreatedByUserId.Should().Be(TestUserId);
    }

    [Fact]
    public async Task N8nConfig_Activate_Deactivate_ShouldToggleIsActiveFlag()
    {
        // Arrange
        var config = new N8NConfiguration(
            Guid.NewGuid(),
            "Test N8n",
            new WorkflowUrl("https://test-n8n.meepleai.dev"),
            "test_api_key",
            TestUserId
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Deactivate
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.Deactivate();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Deactivated
        var deactivated = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        deactivated.Should().NotBeNull();
        deactivated!.IsActive.Should().BeFalse();

        _dbContext.ChangeTracker.Clear();

        // Act - Reactivate
        var tracked2 = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked2!.Activate();
        await _repository.UpdateAsync(tracked2, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Reactivated
        var activated = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        activated.Should().NotBeNull();
        activated!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task N8nConfig_TestConnection_ShouldRecordTestResult()
    {
        // Arrange
        var config = new N8NConfiguration(
            Guid.NewGuid(),
            "Connection Test N8n",
            new WorkflowUrl("https://connection-test.meepleai.dev"),
            "connection_test_key",
            TestUserId
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Record successful test
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.RecordTestResult(true, "Connection successful");
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var tested = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tested.Should().NotBeNull();
        tested!.LastTestedAt.Should().NotBeNull();
        tested.LastTestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        tested.LastTestResult.Should().Be("Connection successful");
    }

    [Fact]
    public async Task N8nConfig_UpdateConfiguration_ShouldUpdateFields()
    {
        // Arrange
        var config = new N8NConfiguration(
            Guid.NewGuid(),
            "Original Config",
            new WorkflowUrl("https://original.meepleai.dev"),
            "original_key",
            TestUserId
        );

        await _repository!.AddAsync(config, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Update configuration
        var tracked = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        tracked!.UpdateConfiguration(
            name: "Updated Config",
            baseUrl: new WorkflowUrl("https://updated.meepleai.dev"),
            webhookUrl: new WorkflowUrl("https://webhook.updated.meepleai.dev/hook1")
        );
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(config.Id, TestCancellationToken);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Config");
        updated.BaseUrl.Value.Should().Be("https://updated.meepleai.dev");
        updated.WebhookUrl.Should().NotBeNull();
        updated.WebhookUrl!.Value.Should().Be("https://webhook.updated.meepleai.dev/hook1");
    }

    [Fact]
    public async Task N8nConfig_GetActiveOnly_ShouldFilterInactive()
    {
        // Arrange
        var activeConfig = new N8NConfiguration(
            Guid.NewGuid(),
            "Active Config",
            new WorkflowUrl("https://active.meepleai.dev"),
            "active_key",
            TestUserId
        );

        var inactiveConfig = new N8NConfiguration(
            Guid.NewGuid(),
            "Inactive Config",
            new WorkflowUrl("https://inactive.meepleai.dev"),
            "inactive_key",
            TestUserId
        );

        await _repository!.AddAsync(activeConfig, TestCancellationToken);
        await _repository.AddAsync(inactiveConfig, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Deactivate second config
        var tracked = await _repository.GetByIdAsync(inactiveConfig.Id, TestCancellationToken);
        tracked!.Deactivate();
        await _repository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var activeConfigs = await _repository.GetActiveConfigurationAsync(TestCancellationToken);

        // Assert
        activeConfigs.Should().NotBeNull();
        activeConfigs!.Name.Should().Be("Active Config");
    }
}