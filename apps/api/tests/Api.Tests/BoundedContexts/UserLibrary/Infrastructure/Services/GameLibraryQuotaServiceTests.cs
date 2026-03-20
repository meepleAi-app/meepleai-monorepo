using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Services;
using Api.BoundedContexts.UserLibrary.Infrastructure.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;
using UserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;

namespace Api.Tests.BoundedContexts.UserLibrary.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
public class GameLibraryQuotaServiceTests
{
    private readonly Mock<IUserLibraryRepository> _libraryRepositoryMock;
    private readonly Mock<IConfigurationService> _configServiceMock;
    private readonly GameLibraryQuotaService _service;

    public GameLibraryQuotaServiceTests()
    {
        _libraryRepositoryMock = new Mock<IUserLibraryRepository>();
        _configServiceMock = new Mock<IConfigurationService>();

        _service = new GameLibraryQuotaService(
            _libraryRepositoryMock.Object,
            _configServiceMock.Object,
            NullLogger<GameLibraryQuotaService>.Instance);
    }

    #region CheckQuotaAsync - Role Bypass Tests

    [Fact]
    public async Task CheckQuotaAsync_AdminUser_ReturnsUnlimited()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.Admin;

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.DenialReason.Should().BeNull();
        result.CurrentCount.Should().Be(0);
        result.MaxAllowed.Should().Be(int.MaxValue);

        // Verify repository was never called
        _libraryRepositoryMock.Verify(
            r => r.GetUserLibraryCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckQuotaAsync_EditorUser_ReturnsUnlimited()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = AuthRole.Editor;

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.DenialReason.Should().BeNull();
        result.MaxAllowed.Should().Be(int.MaxValue);

        // Verify repository was never called
        _libraryRepositoryMock.Verify(
            r => r.GetUserLibraryCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region CheckQuotaAsync - Tier Limit Tests

    [Theory]
    [InlineData("free", 5)]
    [InlineData("normal", 20)]
    [InlineData("premium", 50)]
    public async Task CheckQuotaAsync_WithinLimit_ReturnsAllowed(string tierValue, int maxGames)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Parse(tierValue);
        var userRole = AuthRole.User;
        var currentCount = maxGames - 1; // One below limit

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null); // Use defaults

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.DenialReason.Should().BeNull();
        result.CurrentCount.Should().Be(currentCount);
        result.MaxAllowed.Should().Be(maxGames);
    }

    [Theory]
    [InlineData("free", 5)]
    [InlineData("normal", 20)]
    [InlineData("premium", 50)]
    public async Task CheckQuotaAsync_AtLimit_ReturnsDenied(string tierValue, int maxGames)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Parse(tierValue);
        var userRole = AuthRole.User;
        var currentCount = maxGames; // At limit

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null); // Use defaults

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenialReason.Should().NotBeNull();
        result.DenialReason.Should().Contain($"{maxGames} games");
        result.DenialReason.Should().Contain($"{tierValue} tier");
        result.DenialReason.Should().Contain("Upgrade your subscription");
        result.CurrentCount.Should().Be(currentCount);
        result.MaxAllowed.Should().Be(maxGames);
    }

    [Theory]
    [InlineData("free", 5)]
    [InlineData("normal", 20)]
    [InlineData("premium", 50)]
    public async Task CheckQuotaAsync_OverLimit_ReturnsDenied(string tierValue, int maxGames)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Parse(tierValue);
        var userRole = AuthRole.User;
        var currentCount = maxGames + 5; // Over limit

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null); // Use defaults

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenialReason.Should().NotBeNull();
        result.CurrentCount.Should().Be(currentCount);
        result.MaxAllowed.Should().Be(maxGames);
    }

    [Fact]
    public async Task CheckQuotaAsync_FreeTierEmptyLibrary_ReturnsAllowed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null);

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.CurrentCount.Should().Be(0);
        result.MaxAllowed.Should().Be(5);
    }

    #endregion

    #region CheckQuotaAsync - Custom Configuration Tests

    [Fact]
    public async Task CheckQuotaAsync_CustomConfiguredLimit_UsesConfigValue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;
        var customLimit = 100;
        var currentCount = 50;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>("LibraryLimits:free:MaxGames", null, null))
            .ReturnsAsync(customLimit);

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.CurrentCount.Should().Be(currentCount);
        result.MaxAllowed.Should().Be(customLimit); // Custom limit, not default 5
    }

    [Fact]
    public async Task CheckQuotaAsync_CustomConfiguredLimitExceeded_ReturnsDenied()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = AuthRole.User;
        var customLimit = 200;
        var currentCount = 200;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>("LibraryLimits:premium:MaxGames", null, null))
            .ReturnsAsync(customLimit);

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.DenialReason.Should().Contain("200 games");
        result.MaxAllowed.Should().Be(customLimit);
    }

    #endregion

    #region GetQuotaInfoAsync - Role Tests

    [Fact]
    public async Task GetQuotaInfoAsync_AdminUser_ReturnsUnlimitedWithActualCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.Admin;
        var actualCount = 25;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actualCount);

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        info.IsUnlimited.Should().BeTrue();
        info.GamesInLibrary.Should().Be(actualCount); // Admin still gets actual count
        info.MaxGames.Should().Be(int.MaxValue);
        info.RemainingSlots.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task GetQuotaInfoAsync_EditorUser_ReturnsUnlimitedWithActualCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = AuthRole.Editor;
        var actualCount = 50;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actualCount);

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        info.IsUnlimited.Should().BeTrue();
        info.GamesInLibrary.Should().Be(actualCount);
        info.MaxGames.Should().Be(int.MaxValue);
    }

    #endregion

    #region GetQuotaInfoAsync - Quota Info Tests

    [Theory]
    [InlineData("free", 5, 3, 2)]
    [InlineData("normal", 20, 15, 5)]
    [InlineData("premium", 50, 30, 20)]
    public async Task GetQuotaInfoAsync_RegularUser_ReturnsCorrectQuotaInfo(
        string tierValue, int maxGames, int currentCount, int expectedRemaining)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Parse(tierValue);
        var userRole = AuthRole.User;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null); // Use defaults

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        info.IsUnlimited.Should().BeFalse();
        info.GamesInLibrary.Should().Be(currentCount);
        info.MaxGames.Should().Be(maxGames);
        info.RemainingSlots.Should().Be(expectedRemaining);
    }

    [Fact]
    public async Task GetQuotaInfoAsync_UsageExceedsLimit_RemainingIsZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;
        var currentCount = 10; // Over the 5 limit

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCount);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null);

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        info.GamesInLibrary.Should().Be(currentCount);
        info.MaxGames.Should().Be(5);
        info.RemainingSlots.Should().Be(0); // Math.Max(0, 5 - 10) = 0
    }

    [Fact]
    public async Task GetQuotaInfoAsync_EmptyLibrary_ReturnsFullRemaining()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = AuthRole.User;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null);

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        info.GamesInLibrary.Should().Be(0);
        info.MaxGames.Should().Be(50);
        info.RemainingSlots.Should().Be(50);
    }

    #endregion

    #region Edge Cases and Validation

    [Fact]
    public async Task CheckQuotaAsync_UnknownTier_DefaultsToFreeLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free; // Using Free as we can't create unknown tiers easily
        var userRole = AuthRole.User;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null);

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.MaxAllowed.Should().Be(5); // Default free tier limit
    }

    [Fact]
    public async Task CheckQuotaAsync_NullUserTier_ThrowsArgumentNullException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        UserTier userTier = null!;
        var userRole = AuthRole.User;

        // Act & Assert
        var act = () =>
            _service.CheckQuotaAsync(userId, userTier, userRole);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckQuotaAsync_NullUserRole_ThrowsArgumentNullException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        AuthRole userRole = null!;

        // Act & Assert
        var act2 = () =>
            _service.CheckQuotaAsync(userId, userTier, userRole);
        await act2.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetQuotaInfoAsync_NullUserTier_ThrowsArgumentNullException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        UserTier userTier = null!;
        var userRole = AuthRole.User;

        // Act & Assert
        var act3 = () =>
            _service.GetQuotaInfoAsync(userId, userTier, userRole);
        await act3.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetQuotaInfoAsync_NullUserRole_ThrowsArgumentNullException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        AuthRole userRole = null!;

        // Act & Assert
        var act4 = () =>
            _service.GetQuotaInfoAsync(userId, userTier, userRole);
        await act4.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Configuration Key Tests

    [Fact]
    public async Task CheckQuotaAsync_UsesCorrectConfigKey_FreeTier()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.User;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null);

        // Act
        await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert - Verify correct config key was used
        _configServiceMock.Verify(
            c => c.GetValueAsync<int?>("LibraryLimits:free:MaxGames", null, null),
            Times.Once);
    }

    [Fact]
    public async Task CheckQuotaAsync_UsesCorrectConfigKey_NormalTier()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = AuthRole.User;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null);

        // Act
        await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        _configServiceMock.Verify(
            c => c.GetValueAsync<int?>("LibraryLimits:normal:MaxGames", null, null),
            Times.Once);
    }

    [Fact]
    public async Task CheckQuotaAsync_UsesCorrectConfigKey_PremiumTier()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = AuthRole.User;

        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((int?)null);

        // Act
        await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        _configServiceMock.Verify(
            c => c.GetValueAsync<int?>("LibraryLimits:premium:MaxGames", null, null),
            Times.Once);
    }

    #endregion
}
