using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to retrieve user profile information.
/// Returns detailed profile data for the authenticated user.
/// </summary>
internal record GetUserProfileQuery : IQuery<UserProfileDto?>
{
    public Guid UserId { get; init; }
}
