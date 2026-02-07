using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Operations;

/// <summary>
/// Query to retrieve sent email records with pagination and filtering.
/// Issue #3696: Operations - Service Control Panel.
/// Shows system emails sent by the application for monitoring purposes.
/// </summary>
internal record GetSentEmailsQuery(
    int Limit,
    int Offset,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Status
) : IQuery<SentEmailsResponseDto>;
