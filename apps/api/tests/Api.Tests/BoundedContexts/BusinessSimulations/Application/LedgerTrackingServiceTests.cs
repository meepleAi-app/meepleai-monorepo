using Api.BoundedContexts.BusinessSimulations.Application.Interfaces;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.BoundedContexts.BusinessSimulations.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application;

/// <summary>
/// Unit tests for LedgerTrackingService (Issue #3721)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class LedgerTrackingServiceTests
{
    private readonly Mock<ILedgerEntryRepository> _repositoryMock;
    private readonly Mock<ILogger<LedgerTrackingService>> _loggerMock;
    private readonly ILedgerTrackingService _service;

    public LedgerTrackingServiceTests()
    {
        _repositoryMock = new Mock<ILedgerEntryRepository>();
        _loggerMock = new Mock<ILogger<LedgerTrackingService>>();
        _service = new LedgerTrackingService(_repositoryMock.Object, _loggerMock.Object);
    }

    #region TrackTokenUsageAsync

    [Fact]
    public async Task TrackTokenUsageAsync_WithValidData_ShouldCreateLedgerEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        // Act
        await _service.TrackTokenUsageAsync(userId, "openai/gpt-4o-mini", 1500, 0.005m, "chat");

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Type.Should().Be(LedgerEntryType.Expense);
        capturedEntry.Category.Should().Be(LedgerCategory.TokenUsage);
        capturedEntry.Amount.Amount.Should().Be(0.005m);
        capturedEntry.Amount.Currency.Should().Be("USD");
        capturedEntry.Source.Should().Be(LedgerEntrySource.Auto);
        capturedEntry.Description.Should().Contain("1500 tokens");
        capturedEntry.Description.Should().Contain("openai/gpt-4o-mini");
        capturedEntry.Metadata.Should().Contain("openai/gpt-4o-mini");
        capturedEntry.CreatedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task TrackTokenUsageAsync_WithZeroCost_ShouldSkipEntry()
    {
        // Act
        await _service.TrackTokenUsageAsync(Guid.NewGuid(), "free-model", 100, 0m, "chat");

        // Assert - no repository call should be made
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TrackTokenUsageAsync_WithEmptyUserId_ShouldThrow()
    {
        // Act
        var act = () => _service.TrackTokenUsageAsync(Guid.Empty, "model", 100, 0.01m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public async Task TrackTokenUsageAsync_WithNullModelId_ShouldThrow()
    {
        // Act
        var act = () => _service.TrackTokenUsageAsync(Guid.NewGuid(), null!, 100, 0.01m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("modelId");
    }

    [Fact]
    public async Task TrackTokenUsageAsync_WithEmptyModelId_ShouldThrow()
    {
        // Act
        var act = () => _service.TrackTokenUsageAsync(Guid.NewGuid(), "", 100, 0.01m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("modelId");
    }

    [Fact]
    public async Task TrackTokenUsageAsync_WithZeroTokens_ShouldThrow()
    {
        // Act
        var act = () => _service.TrackTokenUsageAsync(Guid.NewGuid(), "model", 0, 0.01m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("tokensConsumed");
    }

    [Fact]
    public async Task TrackTokenUsageAsync_WithNegativeTokens_ShouldThrow()
    {
        // Act
        var act = () => _service.TrackTokenUsageAsync(Guid.NewGuid(), "model", -5, 0.01m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("tokensConsumed");
    }

    [Fact]
    public async Task TrackTokenUsageAsync_WithNegativeCost_ShouldThrow()
    {
        // Act
        var act = () => _service.TrackTokenUsageAsync(Guid.NewGuid(), "model", 100, -0.01m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("costUsd");
    }

    [Fact]
    public async Task TrackTokenUsageAsync_MetadataContainsEndpoint()
    {
        // Arrange
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        // Act
        await _service.TrackTokenUsageAsync(Guid.NewGuid(), "model", 100, 0.01m, "qa");

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Metadata.Should().Contain("qa");
    }

    [Fact]
    public async Task TrackTokenUsageAsync_WithNullEndpoint_ShouldSucceed()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.TrackTokenUsageAsync(Guid.NewGuid(), "model", 100, 0.01m, null);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region TrackSubscriptionPaymentAsync

    [Fact]
    public async Task TrackSubscriptionPaymentAsync_WithValidData_ShouldCreateIncomeEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        // Act
        await _service.TrackSubscriptionPaymentAsync(userId, 9.99m, "EUR", "Pro");

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Type.Should().Be(LedgerEntryType.Income);
        capturedEntry.Category.Should().Be(LedgerCategory.Subscription);
        capturedEntry.Amount.Amount.Should().Be(9.99m);
        capturedEntry.Amount.Currency.Should().Be("EUR");
        capturedEntry.Source.Should().Be(LedgerEntrySource.Auto);
        capturedEntry.Description.Should().Contain("Pro");
    }

    [Fact]
    public async Task TrackSubscriptionPaymentAsync_WithCustomMetadata_ShouldUseProvidedMetadata()
    {
        // Arrange
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        var customMetadata = """{"stripePaymentId":"pi_123"}""";

        // Act
        await _service.TrackSubscriptionPaymentAsync(Guid.NewGuid(), 19.99m, "USD", "Enterprise", customMetadata);

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Metadata.Should().Be(customMetadata);
    }

    [Fact]
    public async Task TrackSubscriptionPaymentAsync_WithEmptyUserId_ShouldThrow()
    {
        var act = () => _service.TrackSubscriptionPaymentAsync(Guid.Empty, 9.99m, "EUR", "Pro");
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public async Task TrackSubscriptionPaymentAsync_WithZeroAmount_ShouldThrow()
    {
        var act = () => _service.TrackSubscriptionPaymentAsync(Guid.NewGuid(), 0m, "EUR", "Pro");
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public async Task TrackSubscriptionPaymentAsync_WithNegativeAmount_ShouldThrow()
    {
        var act = () => _service.TrackSubscriptionPaymentAsync(Guid.NewGuid(), -1m, "EUR", "Pro");
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public async Task TrackSubscriptionPaymentAsync_WithEmptyCurrency_ShouldThrow()
    {
        var act = () => _service.TrackSubscriptionPaymentAsync(Guid.NewGuid(), 9.99m, "", "Pro");
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("currency");
    }

    [Fact]
    public async Task TrackSubscriptionPaymentAsync_WithEmptySubscriptionType_ShouldThrow()
    {
        var act = () => _service.TrackSubscriptionPaymentAsync(Guid.NewGuid(), 9.99m, "EUR", "");
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("subscriptionType");
    }

    #endregion

    #region TrackInfrastructureCostAsync

    [Fact]
    public async Task TrackInfrastructureCostAsync_WithValidData_ShouldCreateExpenseEntry()
    {
        // Arrange
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        // Act
        await _service.TrackInfrastructureCostAsync(45.50m, "Daily LLM API costs");

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Type.Should().Be(LedgerEntryType.Expense);
        capturedEntry.Category.Should().Be(LedgerCategory.Infrastructure);
        capturedEntry.Amount.Amount.Should().Be(45.50m);
        capturedEntry.Amount.Currency.Should().Be("USD");
        capturedEntry.Source.Should().Be(LedgerEntrySource.Auto);
        capturedEntry.Description.Should().Be("Daily LLM API costs");
    }

    [Fact]
    public async Task TrackInfrastructureCostAsync_WithCustomDate_ShouldUseProvidedDate()
    {
        // Arrange
        var customDate = DateTime.UtcNow.AddDays(-1);
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        // Act
        await _service.TrackInfrastructureCostAsync(10m, "Costs", date: customDate);

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Date.Should().BeCloseTo(customDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TrackInfrastructureCostAsync_WithZeroAmount_ShouldThrow()
    {
        var act = () => _service.TrackInfrastructureCostAsync(0m, "Costs");
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public async Task TrackInfrastructureCostAsync_WithNegativeAmount_ShouldThrow()
    {
        var act = () => _service.TrackInfrastructureCostAsync(-5m, "Costs");
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public async Task TrackInfrastructureCostAsync_WithEmptyDescription_ShouldThrow()
    {
        var act = () => _service.TrackInfrastructureCostAsync(10m, "");
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("description");
    }

    [Fact]
    public async Task TrackInfrastructureCostAsync_WithNullDescription_ShouldThrow()
    {
        var act = () => _service.TrackInfrastructureCostAsync(10m, null!);
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("description");
    }

    [Fact]
    public async Task TrackInfrastructureCostAsync_WithCustomMetadata_ShouldUseProvidedMetadata()
    {
        // Arrange
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);

        var customMetadata = """{"breakdown":{"openai":30,"anthropic":15}}""";

        // Act
        await _service.TrackInfrastructureCostAsync(45m, "Costs", metadata: customMetadata);

        // Assert
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Metadata.Should().Be(customMetadata);
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrow()
    {
        var act = () => new LedgerTrackingService(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        var act = () => new LedgerTrackingService(_repositoryMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion
}
