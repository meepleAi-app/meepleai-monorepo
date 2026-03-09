using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Services;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class UserAiConsentCheckServiceTests
{
    private readonly Mock<IUserAiConsentRepository> _consentRepositoryMock = new();
    private readonly UserAiConsentCheckService _sut;

    public UserAiConsentCheckServiceTests()
    {
        _sut = new UserAiConsentCheckService(_consentRepositoryMock.Object);
    }

    [Fact]
    public async Task IsAiProcessingAllowedAsync_WhenNoConsentRecord_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _consentRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAiConsent?)null);

        // Act
        var result = await _sut.IsAiProcessingAllowedAsync(userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAiProcessingAllowedAsync_WhenConsentedTrue_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var consent = UserAiConsent.Create(userId, true, false, "1.0.0");
        _consentRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consent);

        // Act
        var result = await _sut.IsAiProcessingAllowedAsync(userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAiProcessingAllowedAsync_WhenConsentedFalse_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var consent = UserAiConsent.Create(userId, false, false, "1.0.0");
        _consentRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consent);

        // Act
        var result = await _sut.IsAiProcessingAllowedAsync(userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsExternalProviderAllowedAsync_WhenBothTrue_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var consent = UserAiConsent.Create(userId, true, true, "1.0.0");
        _consentRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consent);

        // Act
        var result = await _sut.IsExternalProviderAllowedAsync(userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsExternalProviderAllowedAsync_WhenAiDisabledExternalEnabled_ReturnsFalse()
    {
        // Arrange — edge case: external consent but AI processing disabled
        var userId = Guid.NewGuid();
        var consent = UserAiConsent.Create(userId, true, true, "1.0.0");
        consent.UpdateConsent(false, true, "1.0.0"); // AI off but external left "on"
        _consentRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consent);

        // Act
        var result = await _sut.IsExternalProviderAllowedAsync(userId);

        // Assert
        result.Should().BeFalse("external providers require AI processing to be enabled");
    }

    [Fact]
    public async Task IsExternalProviderAllowedAsync_WhenNoConsentRecord_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _consentRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAiConsent?)null);

        // Act
        var result = await _sut.IsExternalProviderAllowedAsync(userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsExternalProviderAllowedAsync_WhenAiEnabledExternalDisabled_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var consent = UserAiConsent.Create(userId, true, false, "1.0.0");
        _consentRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consent);

        // Act
        var result = await _sut.IsExternalProviderAllowedAsync(userId);

        // Assert
        result.Should().BeFalse();
    }
}
