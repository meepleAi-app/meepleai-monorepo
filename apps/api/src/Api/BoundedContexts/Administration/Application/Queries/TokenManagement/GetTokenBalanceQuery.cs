using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.TokenManagement;

/// <summary>
/// Query to get current token balance and projection (Issue #3692)
/// </summary>
public sealed record GetTokenBalanceQuery : IQuery<TokenBalanceDto>;
