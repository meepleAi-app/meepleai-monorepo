using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.TokenManagement;

/// <summary>
/// Query to get top token consumers (Issue #3692)
/// </summary>
public sealed record GetTopConsumersQuery(int Limit = 10) : IQuery<TopConsumersListDto>;
