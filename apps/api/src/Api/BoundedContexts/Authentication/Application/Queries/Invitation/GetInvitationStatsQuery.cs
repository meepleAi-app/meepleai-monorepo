using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Response record
namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Query to get invitation statistics (counts by status).
/// Issue #124: User invitation system.
/// </summary>
internal record GetInvitationStatsQuery() : IQuery<InvitationStatsResponse>;

/// <summary>
/// Invitation statistics response.
/// </summary>
public sealed record InvitationStatsResponse(
    int Pending,
    int Accepted,
    int Expired,
    int Total);
