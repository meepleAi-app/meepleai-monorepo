using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Issue #1089: Public query — returns the currently visible status banner, or null if hidden.
/// </summary>
internal sealed record GetPublicStatusBannerQuery : IQuery<PublicStatusBannerResponse?>;
