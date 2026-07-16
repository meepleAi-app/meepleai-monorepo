using System;
using Api.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.ResourcesTests;

/// <summary>
/// Unit tests for <see cref="SystemResourceService"/> (Issue #3041).
/// Pure unit — reads live process/host metrics via System.Diagnostics, so
/// numeric CPU% is non-deterministic: we assert presence + non-negative bounds only.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class SystemResourceServiceTests
{
    [Fact]
    public void GetSystemResources_ReturnsPopulatedMetrics()
    {
        var svc = new SystemResourceService();

        var dto = svc.GetSystemResources();

        dto.Should().NotBeNull();
        dto.ProcessorCount.Should().Be(Environment.ProcessorCount).And.BeGreaterThan(0);
        dto.HostMemoryTotalBytes.Should().BeGreaterThan(0);
        dto.ProcessWorkingSetBytes.Should().BeGreaterThan(0);
        dto.GcHeapBytes.Should().BeGreaterThan(0);
        dto.ProcessCpuPercent.Should().BeGreaterThanOrEqualTo(0); // cold call => 0
        dto.ProcessUptimeSeconds.Should().BeGreaterThanOrEqualTo(0);
        dto.MeasuredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetSystemResources_SecondCall_ComputesCpuWithinBounds()
    {
        var svc = new SystemResourceService();

        _ = svc.GetSystemResources();       // primes the CPU snapshot
        var dto = svc.GetSystemResources();  // delta path

        dto.ProcessCpuPercent.Should().BeInRange(0, 100);
    }
}
