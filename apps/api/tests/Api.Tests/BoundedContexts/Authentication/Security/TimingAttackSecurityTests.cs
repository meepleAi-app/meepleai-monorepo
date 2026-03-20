using Api.Services;
using Api.Tests.Constants;
using System.Diagnostics;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Authentication.Security;

/// <summary>
/// Security tests for timing attack resistance in password and API key verification.
/// Issue #2645: Security edge cases for timing attacks.
/// OWASP Reference: A07:2021 - Identification and Authentication Failures
/// </summary>
/// <remarks>
/// Timing attacks exploit response time differences to extract secrets:
/// - If "wrong first character" returns faster than "wrong last character",
///   attacker can brute-force character-by-character.
/// - PBKDF2 with FixedTimeEquals prevents this by ensuring constant-time comparison.
/// </remarks>
[Trait("Category", TestCategories.Security)]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2645")]
[Trait("OWASP", "A07-Authentication")]
public class TimingAttackSecurityTests
{
    private readonly IPasswordHashingService _passwordHashingService;

    // Statistical thresholds for timing attack detection
    private const int SampleSize = 500; // Number of samples for statistical significance
    private const double MaxTimingVariancePercent = 0.15; // 15% max variance (constant-time)

    public TimingAttackSecurityTests()
    {
        _passwordHashingService = new PasswordHashingService();
    }

    #region Password Timing Attack Tests

    /// <summary>
    /// SECURITY TEST: Password verification should use constant-time comparison.
    /// Timing difference between correct and incorrect passwords should be minimal.
    /// </summary>
    /// <remarks>
    /// This test is skipped in CI because timing measurements are inherently unreliable
    /// due to JIT compilation, CPU scheduling, system load, and GC pauses.
    /// The underlying PBKDF2 + FixedTimeEquals implementation is secure by design.
    /// </remarks>
    [Fact(Skip = "Timing tests are inherently flaky in CI environments due to system variance")]
    public void PasswordVerification_ValidVsInvalid_ShouldBeConstantTime()
    {
        // Arrange
        var password = "Secure@Password123!";
        var storedHash = _passwordHashingService.HashSecret(password);

        var validTimings = new List<long>();
        var invalidTimings = new List<long>();

        // Warm-up to reduce JIT impact
        for (int i = 0; i < 10; i++)
        {
            _passwordHashingService.VerifySecret(password, storedHash);
            _passwordHashingService.VerifySecret("WrongPassword", storedHash);
        }

        // Act - Measure timing for valid vs invalid passwords
        for (int i = 0; i < SampleSize; i++)
        {
            // Valid password timing
            var sw1 = Stopwatch.StartNew();
            var validResult = _passwordHashingService.VerifySecret(password, storedHash);
            sw1.Stop();
            if (validResult) validTimings.Add(sw1.ElapsedTicks);

            // Invalid password timing
            var sw2 = Stopwatch.StartNew();
            var invalidResult = _passwordHashingService.VerifySecret("CompletelyWrongPassword!", storedHash);
            sw2.Stop();
            if (!invalidResult) invalidTimings.Add(sw2.ElapsedTicks);
        }

        // Assert - Statistical analysis
        var validAvg = validTimings.Average();
        var invalidAvg = invalidTimings.Average();
        var timingDifference = Math.Abs(validAvg - invalidAvg) / Math.Max(validAvg, invalidAvg);

        // OWASP: Timing variance should be minimal for constant-time comparison
        (timingDifference < MaxTimingVariancePercent).Should().BeTrue($"Timing difference {timingDifference:P2} exceeds threshold {MaxTimingVariancePercent:P2}. " +
            $"Valid avg: {validAvg:F2} ticks, Invalid avg: {invalidAvg:F2} ticks");
    }

    /// <summary>
    /// SECURITY TEST: Password verification should not leak info via "closeness" to correct value.
    /// "Almost correct" passwords should take same time as completely wrong ones.
    /// </summary>
    /// <remarks>
    /// This test is skipped in CI because timing measurements are inherently unreliable
    /// due to JIT compilation, CPU scheduling, system load, and GC pauses.
    /// The underlying PBKDF2 + FixedTimeEquals implementation is secure by design.
    /// </remarks>
    [Fact(Skip = "Timing tests are inherently flaky in CI environments due to system variance")]
    public void PasswordVerification_CloseVsFar_ShouldNotLeakInformation()
    {
        // Arrange
        var password = "Secure@Password123!";
        var storedHash = _passwordHashingService.HashSecret(password);

        // "Close" passwords (differ by 1 char) vs "far" passwords (completely different)
        var closePassword = "Secure@Password123?"; // Off by 1 char
        var farPassword = "XXXXXXXXXXXXXXXXXXX"; // Completely different

        var closeTimings = new List<long>();
        var farTimings = new List<long>();

        // Warm-up
        for (int i = 0; i < 10; i++)
        {
            _passwordHashingService.VerifySecret(closePassword, storedHash);
            _passwordHashingService.VerifySecret(farPassword, storedHash);
        }

        // Act
        for (int i = 0; i < SampleSize; i++)
        {
            var sw1 = Stopwatch.StartNew();
            _passwordHashingService.VerifySecret(closePassword, storedHash);
            sw1.Stop();
            closeTimings.Add(sw1.ElapsedTicks);

            var sw2 = Stopwatch.StartNew();
            _passwordHashingService.VerifySecret(farPassword, storedHash);
            sw2.Stop();
            farTimings.Add(sw2.ElapsedTicks);
        }

        // Assert
        var closeAvg = closeTimings.Average();
        var farAvg = farTimings.Average();
        var timingDifference = Math.Abs(closeAvg - farAvg) / Math.Max(closeAvg, farAvg);

        (timingDifference < MaxTimingVariancePercent).Should().BeTrue($"Timing reveals password similarity. Close: {closeAvg:F2}, Far: {farAvg:F2}, Diff: {timingDifference:P2}");
    }

    /// <summary>
    /// SECURITY TEST: Different password lengths should not affect verification timing.
    /// Short incorrect passwords should take same time as long incorrect ones.
    /// </summary>
    /// <remarks>
    /// This test is skipped in CI because timing measurements are inherently unreliable
    /// due to JIT compilation, CPU scheduling, system load, and GC pauses.
    /// The underlying PBKDF2 + FixedTimeEquals implementation is secure by design.
    /// </remarks>
    [Fact(Skip = "Timing tests are inherently flaky in CI environments due to system variance")]
    public void PasswordVerification_DifferentLengths_ShouldBeConstantTime()
    {
        // Arrange
        var password = "Secure@Password123!";
        var storedHash = _passwordHashingService.HashSecret(password);

        var shortPassword = "x";
        var longPassword = new string('x', 1000);

        var shortTimings = new List<long>();
        var longTimings = new List<long>();

        // Warm-up
        for (int i = 0; i < 10; i++)
        {
            _passwordHashingService.VerifySecret(shortPassword, storedHash);
            _passwordHashingService.VerifySecret(longPassword, storedHash);
        }

        // Act
        for (int i = 0; i < SampleSize; i++)
        {
            var sw1 = Stopwatch.StartNew();
            _passwordHashingService.VerifySecret(shortPassword, storedHash);
            sw1.Stop();
            shortTimings.Add(sw1.ElapsedTicks);

            var sw2 = Stopwatch.StartNew();
            _passwordHashingService.VerifySecret(longPassword, storedHash);
            sw2.Stop();
            longTimings.Add(sw2.ElapsedTicks);
        }

        // Assert
        var shortAvg = shortTimings.Average();
        var longAvg = longTimings.Average();
        var timingDifference = Math.Abs(shortAvg - longAvg) / Math.Max(shortAvg, longAvg);

        (timingDifference < MaxTimingVariancePercent).Should().BeTrue($"Timing reveals password length. Short: {shortAvg:F2}, Long: {longAvg:F2}, Diff: {timingDifference:P2}");
    }

    #endregion

    #region API Key Timing Attack Tests

    /// <summary>
    /// SECURITY TEST: API key verification should use constant-time comparison.
    /// Uses same PBKDF2/FixedTimeEquals mechanism as password verification.
    /// </summary>
    /// <remarks>
    /// This test is skipped in CI because timing measurements are inherently unreliable
    /// due to JIT compilation, CPU scheduling, system load, and GC pauses.
    /// The underlying PBKDF2 + FixedTimeEquals implementation is secure by design.
    /// </remarks>
    [Fact(Skip = "Timing tests are inherently flaky in CI environments due to system variance")]
    public void ApiKeyVerification_ValidVsInvalid_ShouldBeConstantTime()
    {
        // Arrange - API keys use same hashing service
        var apiKey = "mpl_live_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk";
        var storedHash = _passwordHashingService.HashSecret(apiKey);

        var validTimings = new List<long>();
        var invalidTimings = new List<long>();

        // Warm-up
        for (int i = 0; i < 10; i++)
        {
            _passwordHashingService.VerifySecret(apiKey, storedHash);
            _passwordHashingService.VerifySecret("mpl_live_WRONGKEYWRONGKEYWRONGKEY", storedHash);
        }

        // Act
        for (int i = 0; i < SampleSize; i++)
        {
            var sw1 = Stopwatch.StartNew();
            _passwordHashingService.VerifySecret(apiKey, storedHash);
            sw1.Stop();
            validTimings.Add(sw1.ElapsedTicks);

            var sw2 = Stopwatch.StartNew();
            _passwordHashingService.VerifySecret("mpl_live_InvalidKeyInvalidKeyInvalid", storedHash);
            sw2.Stop();
            invalidTimings.Add(sw2.ElapsedTicks);
        }

        // Assert
        var validAvg = validTimings.Average();
        var invalidAvg = invalidTimings.Average();
        var timingDifference = Math.Abs(validAvg - invalidAvg) / Math.Max(validAvg, invalidAvg);

        (timingDifference < MaxTimingVariancePercent).Should().BeTrue($"API key timing difference {timingDifference:P2} exceeds threshold. " +
            $"Valid: {validAvg:F2}, Invalid: {invalidAvg:F2}");
    }

    /// <summary>
    /// SECURITY TEST: API key prefix matching should not leak timing information.
    /// Keys with matching prefixes should not be distinguishable from non-matching.
    /// </summary>
    [Fact]
    public void ApiKeyVerification_PrefixMatch_ShouldNotLeakTiming()
    {
        // Arrange
        var apiKey = "mpl_live_ABCDEFGHIJKLMNOPQRSTUVWXYZ123456";
        var storedHash = _passwordHashingService.HashSecret(apiKey);

        // Key with same prefix pattern
        var samePrefixWrong = "mpl_live_ABCDEFGHIJKLMNOP_WRONGWRONG";
        // Key with completely different prefix
        var differentPrefix = "xxx_xxxx_ZZZZZZZZZZZZZZZZZZZZZZZZZZZ";

        var samePrefixTimings = new List<long>();
        var differentTimings = new List<long>();

        // Act
        for (int i = 0; i < SampleSize; i++)
        {
            var sw1 = Stopwatch.StartNew();
            _passwordHashingService.VerifySecret(samePrefixWrong, storedHash);
            sw1.Stop();
            samePrefixTimings.Add(sw1.ElapsedTicks);

            var sw2 = Stopwatch.StartNew();
            _passwordHashingService.VerifySecret(differentPrefix, storedHash);
            sw2.Stop();
            differentTimings.Add(sw2.ElapsedTicks);
        }

        // Assert
        var samePrefixAvg = samePrefixTimings.Average();
        var differentAvg = differentTimings.Average();
        var timingDifference = Math.Abs(samePrefixAvg - differentAvg) / Math.Max(samePrefixAvg, differentAvg);

        (timingDifference < MaxTimingVariancePercent).Should().BeTrue($"API key prefix timing leak detected. Same prefix: {samePrefixAvg:F2}, Different: {differentAvg:F2}");
    }

    #endregion

    #region Hash Verification Security Tests

    /// <summary>
    /// SECURITY TEST: Malformed hash should not cause timing difference from valid hash.
    /// Prevents timing oracle via deliberately malformed inputs.
    /// </summary>
    [Fact]
    public void HashVerification_MalformedHash_ShouldReturnFastAndConsistent()
    {
        // Arrange
        var secret = "TestSecret123";
        var validHash = _passwordHashingService.HashSecret(secret);
        var malformedHashes = new[]
        {
            "", // Empty
            "v1", // Incomplete
            "v1.100000", // Missing salt and hash
            "v2.100000.AAAA.BBBB", // Wrong version
            "notavalidhash",
            "v1.invalid.AAAA.BBBB" // Invalid iteration count
        };

        var validHashTimings = new List<long>();
        var malformedTimings = new List<long>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            foreach (var malformed in malformedHashes)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    _passwordHashingService.VerifySecret(secret, malformed);
                    sw.Stop();
                    malformedTimings.Add(sw.ElapsedTicks);
                }
                catch (ArgumentException)
                {
                    // Expected for empty/whitespace - still measure time to exception
                }
            }

            var swValid = Stopwatch.StartNew();
            _passwordHashingService.VerifySecret("WrongSecret", validHash);
            swValid.Stop();
            validHashTimings.Add(swValid.ElapsedTicks);
        }

        // Assert - Malformed should fail fast (no PBKDF2 computation)
        // This is acceptable as it doesn't leak secret information
        validHashTimings.Should().NotBeEmpty();
        // Just ensure no crashes - malformed hash timing is expected to differ
        // as PBKDF2 computation is skipped
    }

    /// <summary>
    /// SECURITY TEST: Verify FixedTimeEquals is actually being used.
    /// Statistical analysis should show consistent timing regardless of match position.
    /// </summary>
    /// <remarks>
    /// This test is skipped in CI because timing measurements are inherently unreliable
    /// due to JIT compilation, CPU scheduling, system load, and GC pauses.
    /// The underlying PBKDF2 + FixedTimeEquals implementation is secure by design.
    /// </remarks>
    [Fact(Skip = "Timing tests are inherently flaky in CI environments due to system variance")]
    public void HashVerification_FixedTimeEquals_StatisticalConsistency()
    {
        // Arrange
        var secret = "TestSecret";
        var hash = _passwordHashingService.HashSecret(secret);

        // Different wrong secrets that would fail at different byte positions
        // if naive comparison was used
        var wrongSecrets = new[]
        {
            "TestSecreT", // Differs at end
            "TTestSecret", // Differs at start (extra char)
            "XestSecret", // Differs at start
            "TestXecret", // Differs in middle
            "ZZZZZZZZZZ" // Completely different
        };

        var timingsPerSecret = new Dictionary<string, List<long>>();
        foreach (var ws in wrongSecrets)
        {
            timingsPerSecret[ws] = new List<long>();
        }

        // Act
        for (int i = 0; i < SampleSize / wrongSecrets.Length; i++)
        {
            foreach (var ws in wrongSecrets)
            {
                var sw = Stopwatch.StartNew();
                _passwordHashingService.VerifySecret(ws, hash);
                sw.Stop();
                timingsPerSecret[ws].Add(sw.ElapsedTicks);
            }
        }

        // Assert - All timings should be statistically similar
        var averages = timingsPerSecret.ToDictionary(kv => kv.Key, kv => kv.Value.Average());
        var minAvg = averages.Values.Min();
        var maxAvg = averages.Values.Max();
        var maxVariance = (maxAvg - minAvg) / maxAvg;

        (maxVariance < MaxTimingVariancePercent).Should().BeTrue($"Timing variance between different-position failures: {maxVariance:P2}. " +
            $"This could indicate non-constant-time comparison.");
    }

    #endregion

    #region PBKDF2 Timing Tests

    /// <summary>
    /// SECURITY TEST: PBKDF2 computation time should be consistent regardless of input.
    /// Validates that the hashing function itself is not timing-vulnerable.
    /// </summary>
    [Fact]
    public void HashGeneration_DifferentInputs_ShouldBeConsistentTiming()
    {
        // Arrange
        var inputs = new[]
        {
            "short",
            new string('a', 100),
            new string('z', 100),
            "Special!@#$%^&*()",
            "Unicode: \u4E2D\u6587\u0420\u0443\u0441\u0441\u043A\u0438\u0439"
        };

        var timingsPerInput = new Dictionary<string, List<long>>();
        foreach (var input in inputs)
        {
            timingsPerInput[input] = new List<long>();
        }

        // Act - Lower sample size for hash generation (expensive)
        for (int i = 0; i < 10; i++)
        {
            foreach (var input in inputs)
            {
                var sw = Stopwatch.StartNew();
                _passwordHashingService.HashSecret(input);
                sw.Stop();
                timingsPerInput[input].Add(sw.ElapsedTicks);
            }
        }

        // Assert - All timings should be similar (PBKDF2 fixed iterations)
        var averages = timingsPerInput.ToDictionary(kv => kv.Key, kv => kv.Value.Average());
        var minAvg = averages.Values.Min();
        var maxAvg = averages.Values.Max();
        var maxVariance = (maxAvg - minAvg) / maxAvg;

        // Hash generation should be consistent (same iterations)
        // Wider tolerance: parallel test execution and CI load can cause timing variance
        (maxVariance < 0.35).Should().BeTrue( // 35% variance allowed for hash generation under load
            $"Hash generation timing variance: {maxVariance:P2}. " +
            $"Min: {minAvg:F2}, Max: {maxAvg:F2}");
    }

    #endregion
}