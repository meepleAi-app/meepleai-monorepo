using Api.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using OtpNet;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// TEST-821: TOTP code expiration tests using FakeTimeProvider for deterministic time-travel testing
/// Tests verify TOTP 30-second window behavior with exact time control
/// Addresses TODO from TwoFactorDatabaseAndIntegrationTests.cs:444
/// </summary>
public class TotpExpirationTimeProviderTests
{
    private readonly ITestOutputHelper _output;
    private const string TestSecret = "JBSWY3DPEHPK3PXP"; // Example Base32 secret

    public TotpExpirationTimeProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TotpCode_WithinValidWindow_ShouldBeAccepted()
    {
        // Arrange: Create time provider starting at a specific moment
        var timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1, 12, 0, 0);

        // Act: Generate code at T=0
        var code = GenerateTotpCode(TestSecret, timeProvider);
        _output.WriteLine($"Generated code: {code} at {timeProvider.GetUtcNow():O}");

        // Assert: Code should be valid within 30-second window
        var isValid = VerifyTotpCode(TestSecret, code, timeProvider);
        isValid.Should().BeTrue("code generated at T=0 should be valid at T=0");

        // Advance 15 seconds (still within 30-second window)
        timeProvider.AdvanceSeconds(15);
        _output.WriteLine($"Advanced 15s to {timeProvider.GetUtcNow():O}");
        isValid = VerifyTotpCode(TestSecret, code, timeProvider);
        isValid.Should().BeTrue("code should still be valid 15 seconds later");

        // Advance another 14 seconds (T=29, still within window)
        timeProvider.AdvanceSeconds(14);
        _output.WriteLine($"Advanced 14s to {timeProvider.GetUtcNow():O}");
        isValid = VerifyTotpCode(TestSecret, code, timeProvider);
        isValid.Should().BeTrue("code should still be valid at T=29 seconds");
    }

    [Fact]
    public void TotpCode_AfterWindowExpiration_ShouldBeRejected()
    {
        // Arrange
        var timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1, 12, 0, 0);

        // Act: Generate code at T=0
        var code = GenerateTotpCode(TestSecret, timeProvider);
        var isValidAtStart = VerifyTotpCode(TestSecret, code, timeProvider);
        isValidAtStart.Should().BeTrue("code should be valid when generated");
        _output.WriteLine($"Code {code} valid at {timeProvider.GetUtcNow():O}");

        // Advance exactly 30 seconds (window boundary)
        timeProvider.AdvanceSeconds(30);
        _output.WriteLine($"Advanced 30s to {timeProvider.GetUtcNow():O}");

        // Assert: Code should now be expired (new 30-second window)
        var isValidAfterExpiration = VerifyTotpCode(TestSecret, code, timeProvider);
        isValidAfterExpiration.Should().BeFalse("code should be expired after 30 seconds");
    }

    [Fact]
    public void TotpCode_ExactExpirationBoundary_ShouldTestPrecisely()
    {
        // Arrange
        var timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1, 12, 0, 0);

        // Act & Assert: Test exact expiration boundary
        var code = GenerateTotpCode(TestSecret, timeProvider);

        // T=0: Valid
        VerifyTotpCode(TestSecret, code, timeProvider).Should().BeTrue("T=0 valid");

        // T=29: Still valid
        timeProvider.AdvanceSeconds(29);
        VerifyTotpCode(TestSecret, code, timeProvider).Should().BeTrue("T=29 valid");

        // T=30: New window, code expired
        timeProvider.AdvanceSeconds(1); // Total: 30 seconds
        VerifyTotpCode(TestSecret, code, timeProvider).Should().BeFalse("T=30 expired");
    }

    [Fact]
    public void TotpCode_MultipleWindowsWithTimeTravel_ShouldGenerateDifferentCodes()
    {
        // Arrange
        var timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1, 12, 0, 0);

        // Act: Generate codes in different 30-second windows
        var code1 = GenerateTotpCode(TestSecret, timeProvider);
        _output.WriteLine($"Window 1 (T=0): {code1}");

        timeProvider.AdvanceSeconds(30);
        var code2 = GenerateTotpCode(TestSecret, timeProvider);
        _output.WriteLine($"Window 2 (T=30): {code2}");

        timeProvider.AdvanceSeconds(30);
        var code3 = GenerateTotpCode(TestSecret, timeProvider);
        _output.WriteLine($"Window 3 (T=60): {code3}");

        // Assert: Each window should produce different codes
        code1.Should().NotBe(code2, "code in window 1 ≠ code in window 2");
        code2.Should().NotBe(code3, "code in window 2 ≠ code in window 3");
        code1.Should().NotBe(code3, "code in window 1 ≠ code in window 3");
    }

    [Fact]
    public void TotpCode_WithFastTimeAdvancement_ShouldHandleRapidExpiration()
    {
        // Arrange: Simulate rapid time changes
        var timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1, 12, 0, 0);

        // Act & Assert: Advance time rapidly and verify codes expire appropriately
        for (int i = 0; i < 5; i++)
        {
            var code = GenerateTotpCode(TestSecret, timeProvider);

            // Code valid at generation
            VerifyTotpCode(TestSecret, code, timeProvider).Should().BeTrue($"iteration {i}: code valid at generation");

            // Advance 15 seconds - still valid
            timeProvider.AdvanceSeconds(15);
            VerifyTotpCode(TestSecret, code, timeProvider).Should().BeTrue($"iteration {i}: code valid at T+15s");

            // Advance another 15 seconds (total 30) - expired
            timeProvider.AdvanceSeconds(15);
            VerifyTotpCode(TestSecret, code, timeProvider).Should().BeFalse($"iteration {i}: code expired at T+30s");
        }
    }

    [Fact]
    public void TotpCode_CrossingMidnight_ShouldMaintain30SecondWindows()
    {
        // Arrange: Start just before midnight
        var timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1, 23, 59, 45);

        // Act: Generate code before midnight
        var codeBeforeMidnight = GenerateTotpCode(TestSecret, timeProvider);
        VerifyTotpCode(TestSecret, codeBeforeMidnight, timeProvider).Should().BeTrue("code valid before midnight");

        // Advance 10 seconds (crosses midnight to 2025-01-02 00:00:05)
        timeProvider.AdvanceSeconds(10);
        _output.WriteLine($"Crossed midnight to {timeProvider.GetUtcNow():O}");

        // Assert: Code should still be valid (total elapsed: 10 seconds < 30)
        VerifyTotpCode(TestSecret, codeBeforeMidnight, timeProvider).Should().BeTrue("code still valid after midnight");

        // Advance another 20 seconds (total: 30 seconds)
        timeProvider.AdvanceSeconds(20);

        // Assert: Code should now be expired
        VerifyTotpCode(TestSecret, codeBeforeMidnight, timeProvider).Should().BeFalse("code expired after 30 seconds total");
    }

    [Fact]
    public void TotpCode_WithMillisecondPrecision_ShouldHandleEdgeCases()
    {
        // Arrange
        var timeProvider = TimeTestHelpers.CreateTimeProvider(2025, 1, 1, 12, 0, 0);

        // Act: Generate code at exact second
        var code = GenerateTotpCode(TestSecret, timeProvider);

        // Assert: Test millisecond-level precision at window boundary
        // T=29.999 seconds - should still be valid
        timeProvider.AdvanceMilliseconds(29999);
        VerifyTotpCode(TestSecret, code, timeProvider).Should().BeTrue("T=29.999s valid");

        // T=30.000 seconds - should be expired
        timeProvider.AdvanceMilliseconds(1); // Total: 30,000ms
        VerifyTotpCode(TestSecret, code, timeProvider).Should().BeFalse("T=30.000s expired");
    }

    [Fact]
    public void TotpCode_LongDurationTimeTravel_ShouldGenerateNewValidCodes()
    {
        // Test 1: Code at T=0
        var timeProvider1 = TimeTestHelpers.CreateTimeProvider(2025, 1, 1, 12, 0, 0);
        var code1 = GenerateTotpCode(TestSecret, timeProvider1);
        VerifyTotpCode(TestSecret, code1, timeProvider1).Should().BeTrue("code1 valid in its window");

        // Test 2: Code 30 days later
        var timeProvider2 = TimeTestHelpers.CreateTimeProvider(2025, 1, 31, 12, 0, 0);
        var code2 = GenerateTotpCode(TestSecret, timeProvider2);
        VerifyTotpCode(TestSecret, code2, timeProvider2).Should().BeTrue("code2 valid in its window");

        // Test 3: Code 1 year later
        var timeProvider3 = TimeTestHelpers.CreateTimeProvider(2026, 1, 1, 12, 0, 0);
        var code3 = GenerateTotpCode(TestSecret, timeProvider3);
        VerifyTotpCode(TestSecret, code3, timeProvider3).Should().BeTrue("code3 valid in its window");

        // Assert: All codes should be different (time-based generation)
        code1.Should().NotBe(code2, "30 days later should have different code");
        code2.Should().NotBe(code3, "1 year later should have different code");
        code1.Should().NotBe(code3, "1 year span should have different codes");
    }

    /// <summary>
    /// Helper: Generate TOTP code at current fake time
    /// Uses RFC 6238 algorithm with 30-second window (OtpNet default)
    /// </summary>
    private string GenerateTotpCode(string secret, FakeTimeProvider timeProvider)
    {
        var secretBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(secretBytes);

        // ComputeTotp uses DateTime, so we pass the fake time explicitly
        return totp.ComputeTotp(timeProvider.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Helper: Verify TOTP code at current fake time
    /// Uses no window tolerance (exact 30-second boundary testing)
    /// </summary>
    private bool VerifyTotpCode(string secret, string code, FakeTimeProvider timeProvider)
    {
        var secretBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(secretBytes);

        // VerifyTotp with no window tolerance for precise boundary testing
        // window: null = default window (previous: 1, future: 1) gives ±30 seconds
        // For strict testing, we use VerificationWindow(0, 0) = exact window only
        return totp.VerifyTotp(timeProvider.GetUtcNow().UtcDateTime, code,
            out _, new VerificationWindow(previous: 0, future: 0));
    }
}
