using Api.SharedKernel.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Usage;

/// <summary>
/// Query to retrieve the current tier usage snapshot for a user.
/// E2-2: Game Night Improvvisata - User Usage Endpoint.
/// </summary>
internal sealed record GetUserUsageQuery(Guid UserId) : IRequest<UsageSnapshot>;
