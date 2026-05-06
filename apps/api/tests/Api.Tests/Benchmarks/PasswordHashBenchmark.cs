using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using BenchmarkDotNet.Attributes;

namespace Api.Tests.Benchmarks;

/// <summary>
/// Benchmark for PasswordHash.Create / PasswordHash.Verify (PBKDF2-SHA256, 210k iterations).
/// Captures the baseline cost of the OWASP-recommended iteration count so future tuning
/// (issue I7) can be evaluated against a stable reference number.
///
/// Run via:
///   dotnet run -c Release --project apps/api/tests/Api.Tests/Api.Tests.csproj --filter "*PasswordHashBenchmark*"
/// (or use a dedicated runner project; this benchmark class is wired for invocation by
///  the BenchmarkDotNet console runner pattern).
/// </summary>
[MemoryDiagnoser]
public class PasswordHashBenchmark
{
    private const string TestPassword = "BenchmarkPassword123!";
    private PasswordHash _hash = null!;

    [GlobalSetup]
    public void Setup()
    {
        _hash = PasswordHash.Create(TestPassword);
    }

    /// <summary>Measures PBKDF2-SHA256 hashing at 210k iterations (OWASP recommendation).</summary>
    [Benchmark]
    public PasswordHash Create_210k() => PasswordHash.Create(TestPassword);

    /// <summary>Measures PBKDF2-SHA256 verification at 210k iterations.</summary>
    [Benchmark]
    public bool Verify_210k() => _hash.Verify(TestPassword);
}
