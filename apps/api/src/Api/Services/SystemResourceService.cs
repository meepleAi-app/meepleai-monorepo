using System.Diagnostics;
using System.Globalization;
using System.IO;
using Api.Models;

namespace Api.Services;

/// <summary>
/// Reads process/host resource metrics using only System.Diagnostics / GC / the OS.
/// Stateless and thread-safe: <see cref="SystemResourcesDto.ProcessCpuPercent"/> is the
/// process' AVERAGE CPU usage over its whole lifetime (TotalProcessorTime / (uptime × cores)),
/// so there is no shared mutable snapshot and no cross-request race. The physical host RAM
/// total is read once from /proc/meminfo on Linux (coherent with node_exporter's host series)
/// and cached for the process lifetime. Issue #3041.
/// </summary>
internal sealed class SystemResourceService : ISystemResourceService
{
    private readonly long _hostMemoryTotalBytes = ReadHostMemoryTotalBytes();

    public SystemResourcesDto GetSystemResources()
    {
        using var proc = Process.GetCurrentProcess();
        var nowUtc = DateTime.UtcNow;

        var uptime = nowUtc - proc.StartTime.ToUniversalTime();
        var uptimeSeconds = Math.Max(0d, uptime.TotalSeconds);

        // Average CPU% over the process lifetime — deterministic, stateless, race-free.
        // (TotalProcessorTime is CPU-time summed across all cores, so dividing by
        // uptime × cores yields a bounded [0,100] utilization figure.)
        double cpuPercent = 0d;
        var cores = Environment.ProcessorCount;
        if (uptime.TotalMilliseconds > 0 && cores > 0)
        {
            cpuPercent = proc.TotalProcessorTime.TotalMilliseconds
                         / (uptime.TotalMilliseconds * cores) * 100d;
            if (cpuPercent < 0) cpuPercent = 0;
            if (cpuPercent > 100) cpuPercent = 100;
        }

        return new SystemResourcesDto(
            ProcessWorkingSetBytes: proc.WorkingSet64,
            GcHeapBytes: GC.GetTotalMemory(forceFullCollection: false),
            ProcessorCount: cores,
            ProcessCpuPercent: Math.Round(cpuPercent, 2),
            ProcessUptimeSeconds: Math.Round(uptimeSeconds, 2),
            HostMemoryTotalBytes: _hostMemoryTotalBytes,
            MeasuredAt: nowUtc
        );
    }

    /// <summary>
    /// Physical host RAM in bytes. On Linux (prod/staging/containers) reads
    /// <c>/proc/meminfo</c> <c>MemTotal</c> — the host total, coherent with node_exporter's
    /// host-memory series that feeds the same KPI card. Falls back to the GC-visible limit
    /// on non-Linux/dev or when unreadable. Effectively constant for the process lifetime,
    /// so it is computed once and cached.
    /// </summary>
    private static long ReadHostMemoryTotalBytes()
    {
        try
        {
            if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    // Format: "MemTotal:       32817664 kB"
                    if (!line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2 &&
                        long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb) &&
                        kb > 0)
                    {
                        return kb * 1024L;
                    }

                    break; // MemTotal found but unparsable — stop and fall back
                }
            }
        }
        catch (IOException)
        {
            // fall through to GC fallback
        }
        catch (UnauthorizedAccessException)
        {
            // fall through to GC fallback
        }

        var gcTotal = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return gcTotal > 0 ? gcTotal : 0;
    }
}
