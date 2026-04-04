using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Integration tests for ModelCompatibilityRepository.
/// Issue #5496: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ModelCompatibilityRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IModelCompatibilityRepository? _repository;
    private IServiceProvider? _serviceProvider;

    public ModelCompatibilityRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var databaseName = $"test_model_compat_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped<IModelCompatibilityRepository, ModelCompatibilityRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IModelCompatibilityRepository>();

        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    private async Task SeedModelAsync(
        string modelId = "meta-llama/llama-3.3-70b-instruct:free",
        string displayName = "Llama 3.3 70B",
        string provider = "openrouter",
        bool isAvailable = true,
        bool isDeprecated = false,
        string[]? alternatives = null,
        string[]? strengths = null)
    {
        var entity = new ModelCompatibilityEntryEntity
        {
            ModelId = modelId,
            DisplayName = displayName,
            Provider = provider,
            Alternatives = alternatives ?? new[] { "qwen/qwen-2.5-72b:free" },
            ContextWindow = 128000,
            Strengths = strengths ?? new[] { "reasoning", "multilingual" },
            IsCurrentlyAvailable = isAvailable,
            IsDeprecated = isDeprecated,
            LastVerifiedAt = DateTime.UtcNow,
        };
        _dbContext!.Set<ModelCompatibilityEntryEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedChangeLogAsync(
        string modelId = "meta-llama/llama-3.3-70b-instruct:free",
        string changeType = "deprecated",
        string reason = "Model deprecated by provider")
    {
        var entity = new ModelChangeLogEntity
        {
            ModelId = modelId,
            ChangeType = changeType,
            Reason = reason,
            IsAutomatic = true,
            OccurredAt = DateTime.UtcNow,
        };
        _dbContext!.Set<ModelChangeLogEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();
    }

    // ── GetByModelIdAsync ──

    [Fact]
    public async Task GetByModelIdAsync_ExistingModel_ReturnsEntry()
    {
        // Arrange
        await SeedModelAsync();

        // Act
        var result = await _repository!.GetByModelIdAsync("meta-llama/llama-3.3-70b-instruct:free");

        // Assert
        result.Should().NotBeNull();
        result!.ModelId.Should().Be("meta-llama/llama-3.3-70b-instruct:free");
        result.DisplayName.Should().Be("Llama 3.3 70B");
        result.Provider.Should().Be("openrouter");
        result.IsCurrentlyAvailable.Should().BeTrue();
        result.Alternatives.Should().Contain("qwen/qwen-2.5-72b:free");
    }

    [Fact]
    public async Task GetByModelIdAsync_NonExistingModel_ReturnsNull()
    {
        // Act
        var result = await _repository!.GetByModelIdAsync("nonexistent/model");

        // Assert
        result.Should().BeNull();
    }

    // ── GetAllAsync ──

    [Fact]
    public async Task GetAllAsync_MultipleModels_ReturnsAll()
    {
        // Arrange
        await SeedModelAsync("model/a", "Model A", "openrouter");
        await SeedModelAsync("model/b", "Model B", "ollama");
        await SeedModelAsync("model/c", "Model C", "openrouter");

        // Act
        var result = await _repository!.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        // Act
        var result = await _repository!.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ── GetAvailableByProviderAsync ──

    [Fact]
    public async Task GetAvailableByProviderAsync_FiltersCorrectly()
    {
        // Arrange
        await SeedModelAsync("model/available", "Available", "openrouter", isAvailable: true);
        await SeedModelAsync("model/deprecated", "Deprecated", "openrouter", isDeprecated: true);
        await SeedModelAsync("model/unavailable", "Unavailable", "openrouter", isAvailable: false);
        await SeedModelAsync("model/ollama", "Ollama Model", "ollama", isAvailable: true);

        // Act
        var result = await _repository!.GetAvailableByProviderAsync("openrouter");

        // Assert
        result.Should().HaveCount(1);
        result[0].ModelId.Should().Be("model/available");
    }

    // ── GetUnavailableModelsAsync ──

    [Fact]
    public async Task GetUnavailableModelsAsync_ReturnsDeprecatedAndUnavailable()
    {
        // Arrange
        await SeedModelAsync("model/available", "Available", "openrouter", isAvailable: true);
        await SeedModelAsync("model/deprecated", "Deprecated", "openrouter", isDeprecated: true);
        await SeedModelAsync("model/unavailable", "Unavailable", "openrouter", isAvailable: false);

        // Act
        var result = await _repository!.GetUnavailableModelsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(m => m.ModelId).Should().Contain("model/deprecated");
        result.Select(m => m.ModelId).Should().Contain("model/unavailable");
    }

    // ── UpdateAvailabilityAsync ──

    [Fact]
    public async Task UpdateAvailabilityAsync_UpdatesStatusCorrectly()
    {
        // Arrange
        await SeedModelAsync("model/test", "Test Model", "openrouter", isAvailable: true);

        // Act
        await _repository!.UpdateAvailabilityAsync("model/test", false, true);

        // Assert
        var updated = await _repository.GetByModelIdAsync("model/test");
        updated.Should().NotBeNull();
        updated!.IsCurrentlyAvailable.Should().BeFalse();
        updated.IsDeprecated.Should().BeTrue();
        updated.LastVerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAvailabilityAsync_NonExistingModel_DoesNotThrow()
    {
        // Act & Assert — should not throw
        await _repository!.UpdateAvailabilityAsync("nonexistent/model", false, true);
    }

    // ── UpsertAsync ──

    [Fact]
    public async Task UpsertAsync_NewModel_CreatesEntry()
    {
        // Arrange
        var entry = new ModelCompatibilityEntry(
            Guid.NewGuid(),
            "new/model",
            "New Model",
            "openrouter",
            new[] { "alt/model" },
            64000,
            new[] { "speed" },
            true,
            false,
            DateTime.UtcNow);

        // Act
        await _repository!.UpsertAsync(entry);

        // Assert
        var result = await _repository.GetByModelIdAsync("new/model");
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("New Model");
        result.ContextWindow.Should().Be(64000);
    }

    [Fact]
    public async Task UpsertAsync_ExistingModel_UpdatesEntry()
    {
        // Arrange
        await SeedModelAsync("model/existing", "Old Name", "openrouter");

        var updated = new ModelCompatibilityEntry(
            Guid.NewGuid(),
            "model/existing",
            "Updated Name",
            "openrouter",
            new[] { "new/alternative" },
            256000,
            new[] { "speed", "reasoning" },
            true,
            false,
            DateTime.UtcNow);

        // Act
        await _repository!.UpsertAsync(updated);

        // Assert
        var result = await _repository.GetByModelIdAsync("model/existing");
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Updated Name");
        result.ContextWindow.Should().Be(256000);
        result.Alternatives.Should().Contain("new/alternative");
    }

    // ── Change Log ──

    [Fact]
    public async Task LogChangeAsync_CreatesLogEntry()
    {
        // Arrange
        var entry = new ModelChangeLogEntry(
            Guid.NewGuid(),
            "model/test",
            "deprecated",
            null,
            null,
            "Balanced",
            "Model removed by OpenRouter",
            true,
            null,
            DateTime.UtcNow);

        // Act
        await _repository!.LogChangeAsync(entry);

        // Assert
        var history = await _repository.GetChangeHistoryAsync("model/test");
        history.Should().HaveCount(1);
        history[0].ChangeType.Should().Be("deprecated");
        history[0].Reason.Should().Be("Model removed by OpenRouter");
        history[0].IsAutomatic.Should().BeTrue();
    }

    [Fact]
    public async Task GetChangeHistoryAsync_NoFilter_ReturnsAll()
    {
        // Arrange
        await SeedChangeLogAsync("model/a", "deprecated", "Reason A");
        await SeedChangeLogAsync("model/b", "swapped", "Reason B");

        // Act
        var result = await _repository!.GetChangeHistoryAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetChangeHistoryAsync_WithModelFilter_ReturnsFiltered()
    {
        // Arrange
        await SeedChangeLogAsync("model/a", "deprecated", "Reason A");
        await SeedChangeLogAsync("model/b", "swapped", "Reason B");

        // Act
        var result = await _repository!.GetChangeHistoryAsync("model/a");

        // Assert
        result.Should().HaveCount(1);
        result[0].ModelId.Should().Be("model/a");
    }

    [Fact]
    public async Task GetChangeHistoryAsync_RespectsLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await SeedChangeLogAsync($"model/test", $"change_{i}", $"Reason {i}");
        }

        // Act
        var result = await _repository!.GetChangeHistoryAsync(limit: 5);

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetChangeHistoryAsync_OrderedByOccurredAtDescending()
    {
        // Arrange
        var entry1 = new ModelChangeLogEntity
        {
            ModelId = "model/test",
            ChangeType = "first",
            Reason = "First change",
            IsAutomatic = true,
            OccurredAt = DateTime.UtcNow.AddHours(-2),
        };
        var entry2 = new ModelChangeLogEntity
        {
            ModelId = "model/test",
            ChangeType = "second",
            Reason = "Second change",
            IsAutomatic = true,
            OccurredAt = DateTime.UtcNow,
        };
        _dbContext!.Set<ModelChangeLogEntity>().AddRange(entry1, entry2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository!.GetChangeHistoryAsync("model/test");

        // Assert
        result.Should().HaveCount(2);
        result[0].ChangeType.Should().Be("second"); // Most recent first
        result[1].ChangeType.Should().Be("first");
    }
}
