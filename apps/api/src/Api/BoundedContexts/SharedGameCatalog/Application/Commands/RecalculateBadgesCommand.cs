using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to recalculate badges for one or all users.
/// Awards missing badges and revokes invalid badges based on current contributions.
/// </summary>
public sealed record RecalculateBadgesCommand : IRequest<RecalculateBadgesResponse>
{
    /// <summary>
    /// Gets the user ID to recalculate badges for.
    /// If null, recalculates badges for all users.
    /// </summary>
    public Guid? UserId { get; init; }
}
