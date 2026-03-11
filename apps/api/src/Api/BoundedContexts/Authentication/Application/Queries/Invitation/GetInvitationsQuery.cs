using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Response record
namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Query to get a paginated list of invitations, optionally filtered by status.
/// Issue #124: User invitation system.
/// </summary>
internal record GetInvitationsQuery(
    string? Status,
    int Page = 1,
    int PageSize = 50
) : IQuery<GetInvitationsResponse>;

/// <summary>
/// Paginated response for invitations query.
/// </summary>
public sealed record GetInvitationsResponse(
    IReadOnlyList<InvitationDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
