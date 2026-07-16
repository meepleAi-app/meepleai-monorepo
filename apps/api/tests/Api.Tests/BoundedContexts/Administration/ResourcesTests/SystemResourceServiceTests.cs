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
    public void GetSystemResources_CpuPercent_IsBoundedLifetimeAverage()
    {
        var svc = new SystemResourceService();

        // CPU% is the stateless lifetime average (TotalProcessorTime / (uptime × cores)),
        // so it is always within [0, 100] with no priming/snapshot required.
        var dto = svc.GetSystemResources();

        dto.ProcessCpuPercent.Should().BeInRange(0, 100);
    }

    [Fact]
    public void GetSystemResources_HostMemoryTotal_IsStableAcrossCalls()
    {
        var svc = new SystemResourceService();

        // Host memory total is read once and cached → identical across calls (no race).
        var a = svc.GetSystemResources();
        var b = svc.GetSystemResources();

        a.HostMemoryTotalBytes.Should().Be(b.HostMemoryTotalBytes);
        a.HostMemoryTotalBytes.Should().BeGreaterThan(0);
    }
}
