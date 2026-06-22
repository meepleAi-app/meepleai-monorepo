using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests.Benchmarks;

/// <summary>
/// Benchmark for <see cref="NgramCopyrightLeakGuard.ScanAsync"/> over 5 chunks of
/// 1500 words. Captures the algorithmic baseline so gradual O(n) → O(n log n) drift
/// is detected with statistical confidence intervals — not via CI-noise-prone xUnit
/// timeouts.
///
/// Issue #892 (follow-up to PR #891 §4): replaces the xUnit assertion
/// <c>Given_5ChunksOf1500Words_WhenScanned_ThenCompletesUnder50ms</c> whose threshold has
/// drifted 50ms → 500ms → 1000ms tracking CI environment noise rather than algorithm
/// behavior. At 1000ms the assertion catches only 20x+ regressions vs the 50ms local
/// target — so the signal is dominated by noise.
///
/// Run via:
///   dotnet run -c Release --project apps/api/tests/Api.Tests/Api.Tests.csproj \
///       --filter "*NgramCopyrightLeakGuardBenchmark*"
/// </summary>
[MemoryDiagnoser]
public class NgramCopyrightLeakGuardBenchmark
{
    private const int VerbatimThreshold = 12;
    private const int ScanTimeoutMs = 5000; // generous so the algorithm always runs to completion

    private NgramCopyrightLeakGuard _guard = null!;
    private string _body = null!;
    private ChunkCitation[] _chunks = null!;

    [GlobalSetup]
    public void Setup()
    {
        _guard = new NgramCopyrightLeakGuard(
            Options.Create(new CopyrightLeakGuardOptions
            {
                VerbatimRunThreshold = VerbatimThreshold,
                ScanTimeoutMs = ScanTimeoutMs
            }),
            NullLogger<NgramCopyrightLeakGuard>.Instance);

        // Mirror the original xUnit workload exactly: 5 chunks of 1500 unique words each.
        _chunks = Enumerable.Range(0, 5).Select(i =>
            new ChunkCitation(
                DocumentId: $"doc-{i}",
                PageNumber: 1,
                RelevanceScore: 0.9f,
                SnippetPreview: $"chunk-{i}-preview",
                CopyrightTier: CopyrightTier.Protected)
            {
                FullText = string.Join(' ', Enumerable.Range(0, 1500).Select(j => $"word{i}_{j}"))
            }).ToArray();

        _body = string.Join(' ', Enumerable.Range(0, 500).Select(i => $"bodytok{i}"));
    }

    /// <summary>Scan 500 body tokens against 5 chunks of 1500 words. Local target: &lt;50ms.</summary>
    [Benchmark]
    public async Task<bool> Scan_5ChunksOf1500Words()
    {
        // Return HasLeak to consume the result and prevent dead-code elimination in Release.
        var result = await _guard.ScanAsync(_body, _chunks, CancellationToken.None);
        return result.HasLeak;
    }
}
