using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.TokenManagement;

/// <summary>
/// Query to get token consumption trend data (Issue #3692)
/// </summary>
public sealed record GetTokenConsumptionQuery(int Days = 30) : IQuery<TokenConsumptionDataDto>;
