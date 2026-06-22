using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Anonymous query to retrieve a play record by its public share token.
/// No user authentication required — possessing the token IS the authorization.
/// Issue #2437-2.
/// </summary>
internal record GetPlayRecordByShareTokenQuery(string ShareToken) : IQuery<PlayRecordDto>;
