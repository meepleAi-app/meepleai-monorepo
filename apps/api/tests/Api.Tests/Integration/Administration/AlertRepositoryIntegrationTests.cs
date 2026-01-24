using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for AlertRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, filtered queries, and alert lifecycle management.
/// Issue #2307: Week 3 - Administration repository integration testing
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "2307")]
public sealed class AlertRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IAlertRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestAlertId1 = new("20000000-0000-0000-0000-000000000001");
    private static readonly Guid TestAlertId2 = new("20000000-0000-0000-0000-000000000002");
    private static readonly Guid TestAlertId3 = new("20000000-0000-0000-0000-000000000003");

    public AlertRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_alertrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IAlertRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema
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
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Helper Methods

    private static Alert CreateTestAlert(
        Guid id,
        string alertType = "SystemError",
        AlertSeverity? severity = null,
        string? message = null,
        string? metadata = null)
    {
        return new Alert(
            id: id,
            alertType: alertType,
            severity: severity ?? AlertSeverity.Warning,
            message: message ?? "Test alert message",
            metadata: metadata
        );
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        _dbContext.Set<Api.Infrastructure.Entities.AlertEntity>().RemoveRange(
            _dbContext.Set<Api.Infrastructure.Entities.AlertEntity>());
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingAlert_ShouldReturnAlert()
    {
        // Arrange
        await CleanDatabaseAsync();
        var alert = CreateTestAlert(TestAlertId1, "DatabaseError", AlertSeverity.Critical, "Critical database error");
        await _repository!.AddAsync(alert, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(TestAlertId1, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestAlertId1);
        result.AlertType.Should().Be("DatabaseError");
        result.Severity.Should().Be(AlertSeverity.Critical);
        result.Message.Should().Be("Critical database error");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingAlert_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository!.GetByIdAsync(nonExistingId, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetActiveAlertsAsync Tests (Filtered Query)

    [Fact]
    public async Task GetActiveAlertsAsync_WithActiveAlerts_ShouldReturnOnlyActive()
    {
        // Arrange
        await CleanDatabaseAsync();
        var activeAlert1 = CreateTestAlert(TestAlertId1, "DiskSpace", AlertSeverity.Warning);
        var activeAlert2 = CreateTestAlert(TestAlertId2, "MemoryUsage", AlertSeverity.Info);
        var resolvedAlert = CreateTestAlert(TestAlertId3, "OldError", AlertSeverity.Critical);
        resolvedAlert.Resolve(); // Mark as resolved

        await _repository!.AddAsync(activeAlert1, TestCancellationToken);
        await _repository.AddAsync(activeAlert2, TestCancellationToken);
        await _repository.AddAsync(resolvedAlert, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetActiveAlertsAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(a => a.IsActive.Should().BeTrue());
        result.Should().Contain(a => a.AlertType == "DiskSpace");
        result.Should().Contain(a => a.AlertType == "MemoryUsage");
        result.Should().NotContain(a => a.AlertType == "OldError");
    }

    [Fact]
    public async Task GetActiveAlertsAsync_NoActiveAlerts_ShouldReturnEmpty()
    {
        // Arrange
        await CleanDatabaseAsync();
        var resolvedAlert = CreateTestAlert(TestAlertId1, "Resolved", AlertSeverity.Info);
        resolvedAlert.Resolve();

        await _repository!.AddAsync(resolvedAlert, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetActiveAlertsAsync(TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region AddAsync + SaveChanges Tests

    [Fact]
    public async Task AddAsync_NewAlert_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var alert = CreateTestAlert(
            TestAlertId1,
            "ApiRateLimit",
            AlertSeverity.Warning,
            "API rate limit exceeded",
            "{\"userId\":\"123\",\"endpoint\":\"/api/v1/chat\"}"
        );

        // Act
        await _repository!.AddAsync(alert, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestAlertId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.AlertType.Should().Be("ApiRateLimit");
        persisted.Metadata.Should().Contain("userId");
        persisted.IsActive.Should().BeTrue();
        persisted.ResolvedAt.Should().BeNull();
    }

    #endregion

    #region UpdateAsync + SaveChanges Tests (Alert Resolution)

    [Fact]
    public async Task UpdateAsync_ResolveAlert_ShouldMarkAsResolved()
    {
        // Arrange
        await CleanDatabaseAsync();
        var alert = CreateTestAlert(TestAlertId1, "TransientError", AlertSeverity.Warning);
        await _repository!.AddAsync(alert, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid conflicts
        _dbContext!.ChangeTracker.Clear();

        // Act - Resolve alert
        var loadedAlert = await _repository.GetByIdAsync(TestAlertId1, TestCancellationToken);
        loadedAlert!.Resolve();
        await _repository.UpdateAsync(loadedAlert, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _repository.GetByIdAsync(TestAlertId1, TestCancellationToken);
        updated!.IsActive.Should().BeFalse();
        updated.ResolvedAt.Should().NotBeNull();
        updated.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    #endregion

    #region DeleteAsync Cascade Tests

    [Fact]
    public async Task DeleteAsync_ExistingAlert_ShouldRemoveFromDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var alert = CreateTestAlert(TestAlertId1, "ToDelete", AlertSeverity.Info);
        await _repository!.AddAsync(alert, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Clear tracking to avoid identity conflict
        _dbContext!.ChangeTracker.Clear();

        // Act
        var loadedAlert = await _repository.GetByIdAsync(TestAlertId1, TestCancellationToken);
        await _repository.DeleteAsync(loadedAlert!, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var deleted = await _repository.GetByIdAsync(TestAlertId1, TestCancellationToken);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingAlert_ShouldThrowDbUpdateConcurrencyException()
    {
        // Arrange
        await CleanDatabaseAsync();
        var alert = CreateTestAlert(Guid.NewGuid(), "NonExisting", AlertSeverity.Info);

        // Act
        var act = async () =>
        {
            await _repository!.DeleteAsync(alert, TestCancellationToken);
            await _unitOfWork!.SaveChangesAsync(TestCancellationToken);
        };

        // Assert - EF Core throws concurrency exception when deleting non-tracked entities
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    #endregion
}
