using Api.BoundedContexts.Administration.Domain.Models;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Issue #894: Query to retrieve comprehensive infrastructure details.
/// Combines health checks and Prometheus metrics in a single response.
/// </summary>
internal record GetInfrastructureDetailsQuery : IRequest<InfrastructureDetails>;
