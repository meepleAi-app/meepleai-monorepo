using System.Diagnostics;
using Api.Models;

namespace Api.Services;

/// <summary>
/// Reads process/host resource metrics using only System.Diagnostics / GC / Environment.
/// Registered as Singleton to hold the previous CPU snapshot across requests, so the
/// process CPU% can be computed as a delta without a per-request Task.Delay.
/// Issue #3041.
/// </summary>
internal sealed class SystemResourceService : ISystemResourceService
{
    private readonly System.Threading.Lock _lock = new();
    private DateTime _lastSampleUtc;   // default(DateTime) => cold (first call)
    private TimeSpan _lastCpuTime;

    public SystemResourcesDto GetSystemResources()
    {
        using var proc = Process.GetCurrentProcess();
        var nowUtc = DateTime.UtcNow;
        var cpuTime = proc.TotalProcessorTime;

        double cpuPercent = 0d;
        lock (_lock)
        {
            if (_lastSampleUtc != default)
            {
                var wallMs = (nowUtc - _lastSampleUtc).TotalMilliseconds;
                var cpuMs = (cpuTime - _lastCpuTime).TotalMilliseconds;
                if (wallMs > 0 && Environment.ProcessorCount > 0)
                {
                    cpuPercent = cpuMs / (wallMs * Environment.ProcessorCount) * 100d;
                    if (cpuPercent < 0) cpuPercent = 0;
                    if (cpuPercent > 100) cpuPercent = 100;
                }
            }

            _lastSampleUtc = nowUtc;
            _lastCpuTime = cpuTime;
        }

        var gcInfo = GC.GetGCMemoryInfo();
        var uptimeSeconds = Math.Max(0d, (nowUtc - proc.StartTime.ToUniversalTime()).TotalSeconds);

        return new SystemResourcesDto(
            ProcessWorkingSetBytes: proc.WorkingSet64,
            GcHeapBytes: GC.GetTotalMemory(forceFullCollection: false),
            ProcessorCount: Environment.ProcessorCount,
            ProcessCpuPercent: Math.Round(cpuPercent, 2),
            ProcessUptimeSeconds: Math.Round(uptimeSeconds, 2),
            HostMemoryTotalBytes: gcInfo.TotalAvailableMemoryBytes,
            MeasuredAt: nowUtc
        );
    }
}
