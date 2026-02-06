using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.TokenManagement;

/// <summary>
/// Query to get token usage breakdown per tier (Issue #3692)
/// </summary>
public sealed record GetTokenTierUsageQuery : IQuery<TierUsageListDto>;
