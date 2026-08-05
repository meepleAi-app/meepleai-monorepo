using System;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>#3435 (SP4): the deterministic idempotency keys must be stable across region re-seed.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class TableRegionKeyTests
{
    [Fact]
    public void ComputeRegionHash_IsDeterministic_And64Hex()
    {
        var pdf = Guid.NewGuid();
        var a = TableRegionKey.ComputeRegionHash(pdf, 3, 0.1, 0.2, 0.8, 0.3);
        var b = TableRegionKey.ComputeRegionHash(pdf, 3, 0.1, 0.2, 0.8, 0.3);
        a.Should().Be(b);
        a.Length.Should().Be(64);
    }

    [Fact]
    public void ComputeRegionHash_StableUnderSubQuantumFloatNoise()
    {
        // Re-seed jitter below the 1e-4 quantum must NOT change the key (the whole point: the
        // region row id is regenerated on re-seed, so the key must ride the bbox values).
        var pdf = Guid.NewGuid();
        var a = TableRegionKey.ComputeRegionHash(pdf, 3, 0.10000, 0.20000, 0.80000, 0.30000);
        var b = TableRegionKey.ComputeRegionHash(pdf, 3, 0.100004, 0.199997, 0.800002, 0.299996);
        a.Should().Be(b);
    }

    [Fact]
    public void ComputeRegionHash_DiffersByPdf_Page_And_Bbox()
    {
        var pdf = Guid.NewGuid();
        var baseHash = TableRegionKey.ComputeRegionHash(pdf, 3, 0.1, 0.2, 0.8, 0.3);
        TableRegionKey.ComputeRegionHash(pdf, 4, 0.1, 0.2, 0.8, 0.3).Should().NotBe(baseHash);
        TableRegionKey.ComputeRegionHash(pdf, 3, 0.15, 0.2, 0.8, 0.3).Should().NotBe(baseHash);
        TableRegionKey.ComputeRegionHash(Guid.NewGuid(), 3, 0.1, 0.2, 0.8, 0.3).Should().NotBe(baseHash);
    }

    [Fact]
    public void ChunkIdFromRegionHash_IsDeterministic_DiffersByHash_AndNonEmpty()
    {
        var h1 = TableRegionKey.ComputeRegionHash(Guid.NewGuid(), 1, 0.1, 0.1, 0.5, 0.5);
        var h2 = TableRegionKey.ComputeRegionHash(Guid.NewGuid(), 2, 0.2, 0.2, 0.4, 0.4);
        TableRegionKey.ChunkIdFromRegionHash(h1).Should().Be(TableRegionKey.ChunkIdFromRegionHash(h1));
        TableRegionKey.ChunkIdFromRegionHash(h1).Should().NotBe(TableRegionKey.ChunkIdFromRegionHash(h2));
        TableRegionKey.ChunkIdFromRegionHash(h1).Should().NotBe(Guid.Empty);
    }
}
