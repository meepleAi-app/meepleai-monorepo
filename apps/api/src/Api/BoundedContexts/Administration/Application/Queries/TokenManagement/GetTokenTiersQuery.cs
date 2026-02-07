using Api.BoundedContexts.Administration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.TokenManagement;

/// <summary>
/// Query to get all token tier configurations (Issue #3787)
/// </summary>
public sealed record GetTokenTiersQuery : IRequest<List<TierConfigDto>>;

/// <summary>
/// DTO for tier configuration response
/// </summary>
public sealed record TierConfigDto(
    string Name,
    int TokensPerMonth,
    int TokensPerDay,
    int MessagesPerDay,
    decimal MonthlyPrice,
    bool IsActive
);
