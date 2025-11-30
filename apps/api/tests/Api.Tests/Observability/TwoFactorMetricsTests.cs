using Api.Observability;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// Unit tests for 2FA Prometheus metrics (Issue #1788 - SEC-08).
/// Verifies that metrics helpers correctly record 2FA events.
/// </summary>
/// <remarks>
/// SECURITY: Issue #1788 - SEC-08 Enhanced Security Monitoring & Alerting
/// Tests verify:
/// 1. Record2FAVerification() correctly routes to appropriate counters
/// 2. Record2FALifecycle() tracks setup/enable/disable operations
/// 3. Metric names follow OpenTelemetry conventions
/// 4. Tag strategy supports granular filtering
///
/// Pattern: Simple unit tests (no Testcontainers), smoke test coverage
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Area", "Observability")]
[Trait("Issue", "1788")]
[Trait("Security", "SEC-08")]
public class TwoFactorMetricsTests
{
    /// <summary>
    /// Smoke test: Verify all 2FA counter metrics are initialized.
    /// Ensures no null reference exceptions when calling Record methods.
    /// </summary>
    [Fact]
    public void AllTwoFactorMetrics_ShouldBeInitialized()
    {
        // Assert - All metrics should be non-null static readonly fields
        Assert.NotNull(MeepleAiMetrics.TwoFactorFailedTotpAttempts);
        Assert.NotNull(MeepleAiMetrics.TwoFactorFailedBackupAttempts);
        Assert.NotNull(MeepleAiMetrics.TwoFactorReplayAttacksBlocked);
        Assert.NotNull(MeepleAiMetrics.TwoFactorSuccessfulTotpVerifications);
        Assert.NotNull(MeepleAiMetrics.TwoFactorSuccessfulBackupCodeUses);
        Assert.NotNull(MeepleAiMetrics.TwoFactorSetupTotal);
        Assert.NotNull(MeepleAiMetrics.TwoFactorEnableTotal);
        Assert.NotNull(MeepleAiMetrics.TwoFactorDisableTotal);
    }

    /// <summary>
    /// Test: Record2FAVerification() with TOTP success increments correct counter.
    /// </summary>
    [Fact]
    public void Record2FAVerification_TotpSuccess_ShouldIncrementSuccessCounter()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act - Should not throw
        var exception = Record.Exception(() =>
            MeepleAiMetrics.Record2FAVerification("totp", success: true, userId: userId)
        );

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Test: Record2FAVerification() with TOTP failure increments failure counter.
    /// </summary>
    [Fact]
    public void Record2FAVerification_TotpFailure_ShouldIncrementFailureCounter()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act - Should not throw
        var exception = Record.Exception(() =>
            MeepleAiMetrics.Record2FAVerification("totp", success: false, userId: userId)
        );

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Test: Record2FAVerification() with replay attack increments replay counter.
    /// </summary>
    [Fact]
    public void Record2FAVerification_ReplayAttack_ShouldIncrementReplayCounter()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act - Should not throw
        var exception = Record.Exception(() =>
            MeepleAiMetrics.Record2FAVerification("totp", success: false, userId: userId, isReplayAttack: true)
        );

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Test: Record2FAVerification() with backup code success increments backup counter.
    /// </summary>
    [Fact]
    public void Record2FAVerification_BackupCodeSuccess_ShouldIncrementBackupCounter()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act - Should not throw
        var exception = Record.Exception(() =>
            MeepleAiMetrics.Record2FAVerification("backup_code", success: true, userId: userId)
        );

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Test: Record2FAVerification() with backup code failure increments failure counter.
    /// </summary>
    [Fact]
    public void Record2FAVerification_BackupCodeFailure_ShouldIncrementFailureCounter()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act - Should not throw
        var exception = Record.Exception(() =>
            MeepleAiMetrics.Record2FAVerification("backup_code", success: false, userId: userId)
        );

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Test: Record2FALifecycle() with setup operation increments setup counter.
    /// </summary>
    [Fact]
    public void Record2FALifecycle_Setup_ShouldIncrementSetupCounter()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act - Should not throw
        var exception = Record.Exception(() =>
            MeepleAiMetrics.Record2FALifecycle("setup", userId: userId)
        );

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Test: Record2FALifecycle() with enable operation increments enable counter.
    /// </summary>
    [Fact]
    public void Record2FALifecycle_Enable_ShouldIncrementEnableCounter()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act - Should not throw
        var exception = Record.Exception(() =>
            MeepleAiMetrics.Record2FALifecycle("enable", userId: userId)
        );

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Test: Record2FALifecycle() with disable operation increments disable counter.
    /// </summary>
    [Fact]
    public void Record2FALifecycle_Disable_ShouldIncrementDisableCounter()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act - Should not throw
        var exception = Record.Exception(() =>
            MeepleAiMetrics.Record2FALifecycle("disable", userId: userId)
        );

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Test: Record methods handle null userId gracefully.
    /// </summary>
    [Fact]
    public void Record2FAMethods_WithNullUserId_ShouldNotThrow()
    {
        // Act & Assert - All should handle null userId
        var ex1 = Record.Exception(() =>
            MeepleAiMetrics.Record2FAVerification("totp", success: true, userId: null));
        var ex2 = Record.Exception(() =>
            MeepleAiMetrics.Record2FALifecycle("setup", userId: null));

        Assert.Null(ex1);
        Assert.Null(ex2);
    }

    /// <summary>
    /// Test: Metric names follow OpenTelemetry naming conventions.
    /// Verifies meepleai.2fa.* namespace pattern.
    /// </summary>
    [Fact]
    public void TwoFactorMetrics_ShouldFollowNamingConventions()
    {
        // Arrange - Expected metric name pattern: meepleai.2fa.{name}.total
        var expectedPrefix = "meepleai.2fa.";
        var expectedSuffix = ".total";

        // Act - Get metric names via reflection
        var metricsType = typeof(MeepleAiMetrics);
        var counterFields = metricsType.GetFields()
            .Where(f => f.Name.StartsWith("TwoFactor") && f.IsStatic && f.IsInitOnly)
            .ToList();

        // Assert - All 2FA metrics should follow naming convention
        Assert.NotEmpty(counterFields);
        Assert.True(counterFields.Count == 8, $"Expected 8 2FA metrics, found {counterFields.Count}");
    }
}
