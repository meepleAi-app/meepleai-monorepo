using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Services;
using Api.BoundedContexts.UserLibrary.Infrastructure.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Services;
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
    private readonly Mock<ITierEnforcementService> _tiersMock;
    private readonly GameLibraryQuotaService _service;

    public GameLibraryQuotaServiceTests()
    {
        _libraryRepositoryMock = new Mock<IUserLibraryRepository>();
        _tiersMock = new Mock<ITierEnforcementService>();

        _service = new GameLibraryQuotaService(
            _libraryRepositoryMock.Object,
            _tiersMock.Object,
            NullLogger<GameLibraryQuotaService>.Instance);
    }

    /// <summary>Issue #4212: the library limit comes from the user's plan.</summary>
    private void PlanAllows(Guid userId, int maxGames) =>
        _tiersMock.Setup(t => t.GetLimitsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.FreeTier with { MaxLibraryGames = maxGames });

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

    /// <summary>
    /// Issue #3873 — DoD 5. Tabella dell'esenzione da quota sui CINQUE ruoli.
    /// L'esenzione deve coincidere con <c>HasPermission(Role.Editor)</c> = {editor, admin, superadmin};
    /// <c>creator</c> e <c>user</c> restano soggetti alla quota di tier.
    /// La libreria e' esattamente AL limite free (5): un ruolo esente passa comunque,
    /// un ruolo soggetto a quota viene negato — cosi' il caso non-bypass non puo' passare per caso.
    /// </summary>
    [Theory]
    [InlineData("superadmin", true, int.MaxValue, 0)]
    [InlineData("admin", true, int.MaxValue, 0)]
    [InlineData("editor", true, int.MaxValue, 0)]
    [InlineData("creator", false, 5, 5)]
    [InlineData("user", false, 5, 5)]
    public async Task CheckQuotaAsync_AllFiveRoles_ExemptsOnlyEditorAdminSuperAdmin(
        string roleValue, bool expectedIsAllowed, int expectedMaxAllowed, int expectedCurrentCount)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.Parse(roleValue);

        // I ruoli soggetti a quota percorrono repository + piano: senza questi setup
        // fallirebbero per la ragione sbagliata (conteggio 0 di default).
        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5); // esattamente al limite free

        PlanAllows(userId, 5);

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().Be(
            expectedIsAllowed,
            "il ruolo '{0}' {1} essere esente dalla quota (HasPermission(Role.Editor))",
            roleValue,
            expectedIsAllowed ? "deve" : "NON deve");
        result.MaxAllowed.Should().Be(expectedMaxAllowed);
        result.CurrentCount.Should().Be(expectedCurrentCount);
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

        PlanAllows(userId, maxGames);

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

        PlanAllows(userId, maxGames);

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

        PlanAllows(userId, maxGames);

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

        PlanAllows(userId, 5);

        // Act
        var result = await _service.CheckQuotaAsync(userId, userTier, userRole);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.CurrentCount.Should().Be(0);
        result.MaxAllowed.Should().Be(5);
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

    /// <summary>
    /// Issue #3873 — DoD 5. Stessa tabella di <see cref="CheckQuotaAsync_AllFiveRoles_ExemptsOnlyEditorAdminSuperAdmin"/>
    /// applicata a <c>GetQuotaInfoAsync</c>: l'esenzione vive in una seconda guardia (riga 86) che
    /// va fissata separatamente. Tier free (limite 5), libreria a 3.
    /// </summary>
    [Theory]
    [InlineData("superadmin", true, int.MaxValue, int.MaxValue)]
    [InlineData("admin", true, int.MaxValue, int.MaxValue)]
    [InlineData("editor", true, int.MaxValue, int.MaxValue)]
    [InlineData("creator", false, 5, 2)]
    [InlineData("user", false, 5, 2)]
    public async Task GetQuotaInfoAsync_AllFiveRoles_ExemptsOnlyEditorAdminSuperAdmin(
        string roleValue, bool expectedIsUnlimited, int expectedMaxGames, int expectedRemainingSlots)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = AuthRole.Parse(roleValue);
        const int actualCount = 3;

        // Entrambi i rami leggono il conteggio reale; solo il ramo non-esente legge il piano.
        _libraryRepositoryMock.Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actualCount);

        PlanAllows(userId, 5);

        // Act
        var info = await _service.GetQuotaInfoAsync(userId, userTier, userRole);

        // Assert
        info.IsUnlimited.Should().Be(
            expectedIsUnlimited,
            "il ruolo '{0}' {1} avere quota illimitata (HasPermission(Role.Editor))",
            roleValue,
            expectedIsUnlimited ? "deve" : "NON deve");
        info.MaxGames.Should().Be(expectedMaxGames);
        info.RemainingSlots.Should().Be(expectedRemainingSlots);
        info.GamesInLibrary.Should().Be(actualCount, "il conteggio reale e' restituito in entrambi i rami");
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

        PlanAllows(userId, maxGames);

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

        PlanAllows(userId, 5);

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

        PlanAllows(userId, 50);

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

}
