using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Issue #1089: Admin query — returns full banner state (including inactive).
/// </summary>
internal sealed record GetAdminStatusBannerQuery : IQuery<AdminStatusBannerResponse>;
