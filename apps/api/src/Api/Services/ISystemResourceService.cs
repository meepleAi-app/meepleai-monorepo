using Api.Models;

namespace Api.Services;

/// <summary>
/// Self-contained host/process resource metrics via System.Diagnostics.
/// Independent from Prometheus/exporters (Issue #3041): returns a live snapshot
/// even when the observability stack is unavailable.
/// </summary>
internal interface ISystemResourceService
{
    SystemResourcesDto GetSystemResources();
}
