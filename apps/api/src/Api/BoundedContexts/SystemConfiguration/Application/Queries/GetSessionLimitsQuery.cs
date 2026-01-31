using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to retrieve session tier limits configuration.
/// Issue #3070: Session limits backend implementation.
/// </summary>
internal record GetSessionLimitsQuery : IQuery<SessionLimitsDto>;
