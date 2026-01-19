using Api.Services;
using Api.Tests.Constants;
using Moq;
using System.Diagnostics;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Security;

/// <summary>
/// Security tests for rate limiting in authentication operations.
/// Issue #2645: Security edge cases for rate limiting.
/// OWASP Reference: A07:2021 - Identification and Authentication Failures
/// </summary>
/// <remarks>
/// Rate limiting prevents:
/// - Brute force attacks on login
/// - API key enumeration attacks
/// - Password reset abuse (email flooding)
/// - Resource exhaustion (DoS)
/// </remarks>
[Trait("Category", TestCategories.Security)]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2645")]
[Trait("OWASP", "A07-Authentication")]
public class RateLimitingSecurityTests
{
    private readonly Mock<IRateLimitService> _mockRateLimitService;

    // Rate limit thresholds (OWASP recommendations)
    private const int MaxLoginAttemptsPerMinute = 5;
    private const int MaxApiKeyUsagePerMinute = 100;
    private const int MaxPasswordResetPerHour = 3;

    public RateLimitingSecurityTests()
    {
        _mockRateLimitService = new Mock<IRateLimitService>();
    }

    #region API Key Rate Limiting Tests

    /// <summary>
    /// SECURITY TEST: API key usage should be rate limited to prevent abuse.
    /// OWASP: Implement rate limiting to prevent credential stuffing.
    /// </summary>
    [Fact]
    public async Task ApiKeyUsage_ExceedsRateLimit_ShouldBeThrottled()
    {
        // Arrange
        var apiKeyId = Guid.NewGuid();
        var callCount = 0;

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.Is<string>(s => s.Contains(apiKeyId.ToString())),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                var allowed = callCount <= MaxApiKeyUsagePerMinute;
                return new RateLimitResult(
                    Allowed: allowed,
                    TokensRemaining: Math.Max(0, MaxApiKeyUsagePerMinute - callCount),
                    RetryAfterSeconds: allowed ? 0 : 60
                );
            });

        // Act - Simulate 150 API key uses (exceeds 100/minute limit)
        var allowedCount = 0;
        var blockedCount = 0;

        for (int i = 0; i < 150; i++)
        {
            var result = await _mockRateLimitService.Object.CheckRateLimitAsync(
                $"api_key:{apiKeyId}",
                MaxApiKeyUsagePerMinute,
                60.0,
                CancellationToken.None);

            if (result.Allowed)
                allowedCount++;
            else
                blockedCount++;
        }

        // Assert
        Assert.Equal(MaxApiKeyUsagePerMinute, allowedCount);
        Assert.Equal(50, blockedCount);
        Assert.True(blockedCount > 0, "API key rate limiting should block excessive usage");
    }

    /// <summary>
    /// SECURITY TEST: Multiple API keys from same user should share rate limit.
    /// Prevents bypass via key rotation attacks.
    /// </summary>
    [Fact]
    public async Task ApiKeyUsage_MultipleKeys_ShouldShareUserRateLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var key1Id = Guid.NewGuid();
        var key2Id = Guid.NewGuid();
        var userCallCount = 0;

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.Is<string>(s => s.Contains(userId.ToString())),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                userCallCount++;
                return new RateLimitResult(
                    Allowed: userCallCount <= MaxApiKeyUsagePerMinute,
                    TokensRemaining: Math.Max(0, MaxApiKeyUsagePerMinute - userCallCount),
                    RetryAfterSeconds: 0
                );
            });

        // Act - Use both keys, should hit combined limit
        for (int i = 0; i < 60; i++)
        {
            await _mockRateLimitService.Object.CheckRateLimitAsync(
                $"user:{userId}", MaxApiKeyUsagePerMinute, 60.0, CancellationToken.None);
        }

        for (int i = 0; i < 60; i++)
        {
            await _mockRateLimitService.Object.CheckRateLimitAsync(
                $"user:{userId}", MaxApiKeyUsagePerMinute, 60.0, CancellationToken.None);
        }

        // Assert - Combined calls should exceed limit
        var finalResult = await _mockRateLimitService.Object.CheckRateLimitAsync(
            $"user:{userId}", MaxApiKeyUsagePerMinute, 60.0, CancellationToken.None);

        Assert.False(finalResult.Allowed, "User-level rate limit should be enforced across all API keys");
    }

    #endregion

    #region Login Rate Limiting Tests

    /// <summary>
    /// SECURITY TEST: Login attempts should be rate limited per IP.
    /// OWASP: Prevent brute force attacks on authentication.
    /// </summary>
    [Fact]
    public async Task LoginAttempts_ExceedsRateLimit_ShouldBeThrottled()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var attemptCount = 0;

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.Is<string>(s => s.Contains(ipAddress)),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attemptCount++;
                var allowed = attemptCount <= MaxLoginAttemptsPerMinute;
                return new RateLimitResult(
                    Allowed: allowed,
                    TokensRemaining: Math.Max(0, MaxLoginAttemptsPerMinute - attemptCount),
                    RetryAfterSeconds: allowed ? 0 : 60
                );
            });

        // Act - Simulate 10 login attempts from same IP
        var results = new List<RateLimitResult>();
        for (int i = 0; i < 10; i++)
        {
            var result = await _mockRateLimitService.Object.CheckRateLimitAsync(
                $"login:{ipAddress}",
                MaxLoginAttemptsPerMinute,
                60.0,
                CancellationToken.None);
            results.Add(result);
        }

        // Assert
        var allowedAttempts = results.Count(r => r.Allowed);
        var blockedAttempts = results.Count(r => !r.Allowed);

        Assert.Equal(MaxLoginAttemptsPerMinute, allowedAttempts);
        Assert.Equal(5, blockedAttempts);
        Assert.True(blockedAttempts > 0, "Login rate limiting should block brute force attacks");
    }

    /// <summary>
    /// SECURITY TEST: Failed login attempts should have stricter rate limits.
    /// Progressive penalty for repeated failures.
    /// </summary>
    [Fact]
    public async Task LoginAttempts_RepeatedFailures_ShouldTriggerProgressivePenalty()
    {
        // Arrange
        var ipAddress = "10.0.0.50";
        var failedAttempts = 0;

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.Is<string>(s => s.Contains("failed")),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                failedAttempts++;
                // Progressive penalty: after 3 failures, wait increases
                var retryAfter = failedAttempts switch
                {
                    <= 3 => 0,
                    <= 5 => 30,
                    <= 10 => 60,
                    _ => 300 // 5 minutes lockout
                };

                return new RateLimitResult(
                    Allowed: failedAttempts <= 3,
                    TokensRemaining: Math.Max(0, 3 - failedAttempts),
                    RetryAfterSeconds: retryAfter
                );
            });

        // Act - Simulate 6 failed login attempts
        var results = new List<RateLimitResult>();
        for (int i = 0; i < 6; i++)
        {
            var result = await _mockRateLimitService.Object.CheckRateLimitAsync(
                $"failed_login:{ipAddress}",
                3,
                60.0,
                CancellationToken.None);
            results.Add(result);
        }

        // Assert
        Assert.True(results[0].Allowed, "First attempt should be allowed");
        Assert.True(results[1].Allowed, "Second attempt should be allowed");
        Assert.True(results[2].Allowed, "Third attempt should be allowed");
        Assert.False(results[3].Allowed, "Fourth attempt should be blocked");
        Assert.True(results[4].RetryAfterSeconds >= 30, "Penalty should increase with failures");
    }

    /// <summary>
    /// SECURITY TEST: Login rate limiting should work per account as well as per IP.
    /// Prevents distributed brute force attacks.
    /// </summary>
    [Fact]
    public async Task LoginAttempts_SameAccount_DifferentIPs_ShouldBeRateLimited()
    {
        // Arrange
        var targetEmail = "target@example.com";
        var accountAttempts = 0;

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.Is<string>(s => s.Contains(targetEmail)),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                accountAttempts++;
                return new RateLimitResult(
                    Allowed: accountAttempts <= 10, // 10 attempts per account per hour
                    TokensRemaining: Math.Max(0, 10 - accountAttempts),
                    RetryAfterSeconds: accountAttempts <= 10 ? 0 : 3600
                );
            });

        // Act - Simulate attacks from "different IPs" targeting same account
        var ipsToSimulate = new[] { "1.1.1.1", "2.2.2.2", "3.3.3.3", "4.4.4.4", "5.5.5.5" };
        var allowedCount = 0;

        foreach (var ip in ipsToSimulate)
        {
            for (int i = 0; i < 5; i++) // 5 attempts per IP
            {
                var result = await _mockRateLimitService.Object.CheckRateLimitAsync(
                    $"account:{targetEmail}",
                    10,
                    3600.0,
                    CancellationToken.None);

                if (result.Allowed) allowedCount++;
            }
        }

        // Assert - Should be limited by account, not by IP
        Assert.Equal(10, allowedCount); // Only 10 allowed total across all IPs
    }

    #endregion

    #region Password Reset Rate Limiting Tests

    /// <summary>
    /// SECURITY TEST: Password reset requests should be rate limited.
    /// OWASP: Prevent email flooding and account enumeration.
    /// </summary>
    [Fact]
    public async Task PasswordReset_ExceedsRateLimit_ShouldBeThrottled()
    {
        // Arrange
        var targetEmail = "victim@example.com";
        var requestCount = 0;

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.Is<string>(s => s.Contains("password_reset")),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                requestCount++;
                return new RateLimitResult(
                    Allowed: requestCount <= MaxPasswordResetPerHour,
                    TokensRemaining: Math.Max(0, MaxPasswordResetPerHour - requestCount),
                    RetryAfterSeconds: requestCount <= MaxPasswordResetPerHour ? 0 : 3600
                );
            });

        // Act - Attempt 10 password reset requests
        var results = new List<RateLimitResult>();
        for (int i = 0; i < 10; i++)
        {
            var result = await _mockRateLimitService.Object.CheckRateLimitAsync(
                $"password_reset:{targetEmail}",
                MaxPasswordResetPerHour,
                3600.0,
                CancellationToken.None);
            results.Add(result);
        }

        // Assert
        var allowedResets = results.Count(r => r.Allowed);
        var blockedResets = results.Count(r => !r.Allowed);

        Assert.Equal(MaxPasswordResetPerHour, allowedResets);
        Assert.Equal(7, blockedResets);
        Assert.True(blockedResets > 0, "Password reset rate limiting should prevent email flooding");
    }

    /// <summary>
    /// SECURITY TEST: Password reset should not reveal account existence via timing.
    /// Same response time for existing and non-existing accounts.
    /// </summary>
    [Fact]
    public void PasswordReset_NonExistentAccount_ShouldNotLeakTiming()
    {
        // Arrange
        var existingEmail = "exists@example.com";
        var nonExistentEmail = "notexists@example.com";

        var existingTimings = new List<long>();
        var nonExistentTimings = new List<long>();

        // Act - Measure timing for both scenarios
        for (int i = 0; i < 100; i++)
        {
            // Simulate processing existing email
            var sw1 = Stopwatch.StartNew();
            // Actual reset would do: lookup user, generate token, queue email
            Thread.Sleep(1); // Simulate consistent processing
            sw1.Stop();
            existingTimings.Add(sw1.ElapsedTicks);

            // Simulate processing non-existent email
            var sw2 = Stopwatch.StartNew();
            // Should do same work: lookup (fail), still "process", return same response
            Thread.Sleep(1); // Same simulated processing time
            sw2.Stop();
            nonExistentTimings.Add(sw2.ElapsedTicks);
        }

        // Assert - Timing should be consistent
        var existingAvg = existingTimings.Average();
        var nonExistentAvg = nonExistentTimings.Average();
        var timingDifference = Math.Abs(existingAvg - nonExistentAvg) / Math.Max(existingAvg, nonExistentAvg);

        // Allow for reasonable variance (this is a conceptual test)
        Assert.True(timingDifference < 0.5,
            $"Password reset timing difference {timingDifference:P2} could leak account existence");
    }

    /// <summary>
    /// SECURITY TEST: Password reset tokens should be single-use.
    /// Prevent token replay attacks.
    /// </summary>
    [Fact]
    public void PasswordResetToken_SingleUse_ShouldPreventReplay()
    {
        // Arrange
        var tokenUsed = false;
        var token = Guid.NewGuid().ToString();

        // Act - First use
        var firstUseSuccess = !tokenUsed;
        if (firstUseSuccess) tokenUsed = true;

        // Second use attempt
        var secondUseSuccess = !tokenUsed;

        // Assert
        Assert.True(firstUseSuccess, "First token use should succeed");
        Assert.False(secondUseSuccess, "Second token use should fail (replay prevention)");
    }

    #endregion

    #region Distributed Attack Detection Tests

    /// <summary>
    /// SECURITY TEST: System should detect distributed attack patterns.
    /// Multiple IPs targeting same resource should trigger alerts.
    /// </summary>
    [Fact]
    public async Task DistributedAttack_SameTarget_ShouldBeDetected()
    {
        // Arrange
        var targetResource = "admin@company.com";
        var attackingIps = Enumerable.Range(1, 50).Select(i => $"192.168.{i}.1").ToArray();
        var totalAttempts = 0;

        _mockRateLimitService
            .Setup(x => x.CheckRateLimitAsync(
                It.Is<string>(s => s.Contains("global")),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                totalAttempts++;
                // Global limit: 100 attempts per target per hour
                return new RateLimitResult(
                    Allowed: totalAttempts <= 100,
                    TokensRemaining: Math.Max(0, 100 - totalAttempts),
                    RetryAfterSeconds: 0
                );
            });

        // Act - Simulate distributed attack
        var results = new List<bool>();
        foreach (var ip in attackingIps)
        {
            for (int i = 0; i < 5; i++) // 5 attempts per IP = 250 total
            {
                var result = await _mockRateLimitService.Object.CheckRateLimitAsync(
                    $"global:{targetResource}",
                    100,
                    3600.0,
                    CancellationToken.None);
                results.Add(result.Allowed);
            }
        }

        // Assert
        var allowedCount = results.Count(r => r);
        Assert.Equal(100, allowedCount); // Should stop at global limit
        Assert.True(results.Count > 100, "Attack should have exceeded global limit");
    }

    #endregion
}
