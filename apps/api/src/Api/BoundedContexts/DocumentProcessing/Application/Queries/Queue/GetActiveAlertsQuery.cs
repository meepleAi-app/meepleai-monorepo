using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Query to get currently active alerts for the processing queue.
/// Issue #5460: Proactive alerts endpoint.
/// </summary>
internal sealed record GetActiveAlertsQuery : IQuery<IReadOnlyList<QueueAlertDto>>;
