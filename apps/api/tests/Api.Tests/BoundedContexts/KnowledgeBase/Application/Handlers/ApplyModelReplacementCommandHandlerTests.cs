using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for ApplyModelReplacementCommandHandler.
/// Issue #5499: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ApplyModelReplacementCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IModelCompatibilityRepository> _compatibilityRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ApplyModelReplacementCommandHandler _handler;

    public ApplyModelReplacementCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _compatibilityRepoMock = new Mock<IModelCompatibilityRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new ApplyModelReplacementCommandHandler(
            _dbContext,
            _compatibilityRepoMock.Object,
            _unitOfWorkMock.Object,
            Mock.Of<ILogger<ApplyModelReplacementCommandHandler>>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task SeedMappings(params (string strategy, string primaryModel, string provider)[] mappings)
    {
        foreach (var (strategy, primaryModel, provider) in mappings)
        {
            _dbContext.Set<StrategyModelMappingEntity>().Add(new StrategyModelMappingEntity
            {
                Id = Guid.NewGuid(),
                Strategy = strategy,
                PrimaryModel = primaryModel,
                FallbackModels = Array.Empty<string>(),
                Provider = provider,
                IsCustomizable = true,
                AdminOnly = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithAffectedMappings_UpdatesPrimaryModel()
    {
        // Arrange
        await SeedMappings(
            ("BALANCED", "meta-llama/llama-3.3-70b-instruct:free", "openrouter"),
            ("FAST", "meta-llama/llama-3.3-70b-instruct:free", "openrouter"));

        var command = new ApplyModelReplacementCommand(
            "meta-llama/llama-3.3-70b-instruct:free",
            "qwen/qwen-2.5-72b:free");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TotalUpdated.Should().Be(2);
        result.UpdatedStrategies.Should().BeEquivalentTo(new[] { "BALANCED", "FAST" });

        var updatedMappings = await _dbContext.Set<StrategyModelMappingEntity>().ToListAsync();
        updatedMappings.Should().AllSatisfy(m =>
            m.PrimaryModel.Should().Be("qwen/qwen-2.5-72b:free"));
    }

    [Fact]
    public async Task Handle_WithNoAffectedMappings_ThrowsNotFoundException()
    {
        // Arrange
        await SeedMappings(("FAST", "other-model", "openrouter"));

        var command = new ApplyModelReplacementCommand(
            "non-existent-model",
            "replacement-model");

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*non-existent-model*");
    }

    [Fact]
    public async Task Handle_LogsChangeInCompatibilityRepository()
    {
        // Arrange
        await SeedMappings(("PRECISE", "old-model", "openrouter"));

        var command = new ApplyModelReplacementCommand("old-model", "new-model");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _compatibilityRepoMock.Verify(
            r => r.LogChangeAsync(
                It.Is<ModelChangeLogEntry>(e =>
                    e.ModelId == "old-model" &&
                    e.ChangeType == "replaced" &&
                    e.NewModelId == "new-model" &&
                    !e.IsAutomatic),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SavesChangesViaUnitOfWork()
    {
        // Arrange
        await SeedMappings(("FAST", "deprecated-model", "openrouter"));

        var command = new ApplyModelReplacementCommand("deprecated-model", "replacement-model");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_OnlyUpdatesMatchingMappings_LeavesOthersUntouched()
    {
        // Arrange
        await SeedMappings(
            ("BALANCED", "deprecated-model", "openrouter"),
            ("FAST", "other-model", "openrouter"));

        var command = new ApplyModelReplacementCommand("deprecated-model", "replacement-model");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TotalUpdated.Should().Be(1);
        result.UpdatedStrategies.Should().ContainSingle("BALANCED");

        var fastMapping = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "FAST");
        fastMapping.PrimaryModel.Should().Be("other-model");
    }
}
