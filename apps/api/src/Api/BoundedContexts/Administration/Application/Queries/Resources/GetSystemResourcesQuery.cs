using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Query for self-contained system/process resource metrics (CPU/RAM/uptime).
/// Issue #3041: does not depend on Prometheus/exporters.
/// </summary>
internal record GetSystemResourcesQuery : IQuery<SystemResourcesDto>;
