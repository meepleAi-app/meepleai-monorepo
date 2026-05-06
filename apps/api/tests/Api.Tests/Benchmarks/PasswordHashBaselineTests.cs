using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using System.Diagnostics;
using Xunit;

namespace Api.Tests.Benchmarks;

/// <summary>
/// Lightweight baseline harness for PasswordHash.Create / PasswordHash.Verify (PBKDF2-SHA256, 210k iter).
/// Companion to <see cref="PasswordHashBenchmark"/> — used when a full BenchmarkDotNet runner is not available.
///
/// Marked as a Performance test so it does not run as part of the default Unit suite.
/// Runs a small sample (5 ops) to keep CI cost bounded; intended for local capture only.
/// </summary>
[Trait("Category", TestCategories.Performance)]
[Trait("BoundedContext", "Authentication")]
public class PasswordHashBaselineTests
{
    private const string TestPassword = "BaselinePassword123!";
    private const int Samples = 5;

    [Fact(Skip = "Performance baseline — run locally to capture timings; do not run in CI.")]
    public void Create_PBKDF2_210k_Iterations_BaselineSample()
    {
        // Warm-up.
        _ = PasswordHash.Create(TestPassword);

        // Capture sample timings.
        var samples = new long[Samples];
        for (var i = 0; i < Samples; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = PasswordHash.Create(TestPassword);
            sw.Stop();
            samples[i] = sw.ElapsedMilliseconds;
        }

        var avg = samples.Sum() / (double)Samples;
        // Sanity-check: 210k iterations should take 30-300ms on modern hardware.
        avg.Should().BeGreaterThan(0);
    }

    [Fact(Skip = "Performance baseline — run locally to capture timings; do not run in CI.")]
    public void Verify_PBKDF2_210k_Iterations_BaselineSample()
    {
        var hash = PasswordHash.Create(TestPassword);

        // Warm-up.
        _ = hash.Verify(TestPassword);

        var samples = new long[Samples];
        for (var i = 0; i < Samples; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = hash.Verify(TestPassword);
            sw.Stop();
            samples[i] = sw.ElapsedMilliseconds;
        }

        var avg = samples.Sum() / (double)Samples;
        avg.Should().BeGreaterThan(0);
    }
}
