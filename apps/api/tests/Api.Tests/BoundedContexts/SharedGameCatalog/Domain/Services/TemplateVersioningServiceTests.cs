using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Services;

/// <summary>
/// Tests for the TemplateVersioningService domain service.
/// Issue #3025: Backend 90% Coverage Target - Phase 13
/// </summary>
[Trait("Category", "Unit")]
public sealed class TemplateVersioningServiceTests : IDisposable
{
    private readonly Mock<IGameStateTemplateRepository> _repositoryMock;
    private readonly TemplateVersioningService _service;
    private readonly List<JsonDocument> _documents = new();

    public TemplateVersioningServiceTests()
    {
        _repositoryMock = new Mock<IGameStateTemplateRepository>();
        _service = new TemplateVersioningService(_repositoryMock.Object);
    }

    private JsonDocument CreateSchema()
    {
        var doc = JsonDocument.Parse("{\"type\":\"object\"}");
        _documents.Add(doc);
        return doc;
    }

    public void Dispose()
    {
        foreach (var doc in _documents)
        {
            doc.Dispose();
        }
    }

    #region SetActiveVersionAsync Tests

    [Fact]
    public async Task SetActiveVersionAsync_DeactivatesOtherVersionsAndActivatesTemplate()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var template = new GameStateTemplate(
            templateId,
            sharedGameId,
            "Test Template",
            CreateSchema(),
            "1.0",
            false,
            GenerationSource.AI,
            0.9m,
            DateTime.UtcNow,
            Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.DeactivateOtherVersionsAsync(
                sharedGameId,
                templateId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetActiveVersionAsync(template);

        // Assert
        _repositoryMock.Verify(
            r => r.DeactivateOtherVersionsAsync(
                sharedGameId,
                templateId,
                It.IsAny<CancellationToken>()),
            Times.Once);
        template.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveVersionAsync_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var template = new GameStateTemplate(
            templateId,
            sharedGameId,
            "Test Template",
            CreateSchema(),
            "1.0",
            false,
            GenerationSource.Manual,
            null,
            DateTime.UtcNow,
            Guid.NewGuid());

        var cancellationToken = new CancellationToken();

        _repositoryMock
            .Setup(r => r.DeactivateOtherVersionsAsync(
                sharedGameId,
                templateId,
                cancellationToken))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SetActiveVersionAsync(template, cancellationToken);

        // Assert
        _repositoryMock.Verify(
            r => r.DeactivateOtherVersionsAsync(
                sharedGameId,
                templateId,
                cancellationToken),
            Times.Once);
    }

    #endregion

    #region GetVersionHistoryAsync Tests

    [Fact]
    public async Task GetVersionHistoryAsync_ReturnsTemplatesFromRepository()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var templates = new List<GameStateTemplate>
        {
            CreateTemplate(sharedGameId, "1.0", true),
            CreateTemplate(sharedGameId, "2.0", false)
        };

        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(
                sharedGameId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var result = await _service.GetVersionHistoryAsync(sharedGameId);

        // Assert
        result.Should().HaveCount(2);
        _repositoryMock.Verify(
            r => r.GetBySharedGameIdAsync(sharedGameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_WithNoTemplates_ReturnsEmptyList()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(
                sharedGameId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameStateTemplate>());

        // Act
        var result = await _service.GetVersionHistoryAsync(sharedGameId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVersionHistoryAsync_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var cancellationToken = new CancellationToken();

        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(sharedGameId, cancellationToken))
            .ReturnsAsync(new List<GameStateTemplate>());

        // Act
        await _service.GetVersionHistoryAsync(sharedGameId, cancellationToken);

        // Assert
        _repositoryMock.Verify(
            r => r.GetBySharedGameIdAsync(sharedGameId, cancellationToken),
            Times.Once);
    }

    #endregion

    #region GetActiveVersionAsync Tests

    [Fact]
    public async Task GetActiveVersionAsync_ReturnsActiveTemplate()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var template = CreateTemplate(sharedGameId, "1.0", true);

        _repositoryMock
            .Setup(r => r.GetActiveTemplateAsync(
                sharedGameId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _service.GetActiveVersionAsync(sharedGameId);

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveVersionAsync_WithNoActiveTemplate_ReturnsNull()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetActiveTemplateAsync(
                sharedGameId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameStateTemplate?)null);

        // Act
        var result = await _service.GetActiveVersionAsync(sharedGameId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveVersionAsync_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var cancellationToken = new CancellationToken();

        _repositoryMock
            .Setup(r => r.GetActiveTemplateAsync(sharedGameId, cancellationToken))
            .ReturnsAsync((GameStateTemplate?)null);

        // Act
        await _service.GetActiveVersionAsync(sharedGameId, cancellationToken);

        // Assert
        _repositoryMock.Verify(
            r => r.GetActiveTemplateAsync(sharedGameId, cancellationToken),
            Times.Once);
    }

    #endregion

    #region ValidateVersionDoesNotExistAsync Tests

    [Fact]
    public async Task ValidateVersionDoesNotExistAsync_WhenVersionDoesNotExist_DoesNotThrow()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.VersionExistsAsync(
                sharedGameId,
                "2.0",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var action = async () => await _service.ValidateVersionDoesNotExistAsync(
            sharedGameId,
            "2.0");

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateVersionDoesNotExistAsync_WhenVersionExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.VersionExistsAsync(
                sharedGameId,
                "1.0",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var action = async () => await _service.ValidateVersionDoesNotExistAsync(
            sharedGameId,
            "1.0");

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*Version 1.0 already exists for game {sharedGameId}*");
    }

    [Fact]
    public async Task ValidateVersionDoesNotExistAsync_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var cancellationToken = new CancellationToken();

        _repositoryMock
            .Setup(r => r.VersionExistsAsync(sharedGameId, "1.0", cancellationToken))
            .ReturnsAsync(false);

        // Act
        await _service.ValidateVersionDoesNotExistAsync(sharedGameId, "1.0", cancellationToken);

        // Assert
        _repositoryMock.Verify(
            r => r.VersionExistsAsync(sharedGameId, "1.0", cancellationToken),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private GameStateTemplate CreateTemplate(Guid sharedGameId, string version, bool isActive)
    {
        return new GameStateTemplate(
            Guid.NewGuid(),
            sharedGameId,
            "Test Template",
            CreateSchema(),
            version,
            isActive,
            GenerationSource.AI,
            0.85m,
            DateTime.UtcNow,
            Guid.NewGuid());
    }

    #endregion
}
