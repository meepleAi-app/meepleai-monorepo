using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.UserProfile;

/// <summary>
/// Command to save user's onboarding interest selections.
/// Issue #124: Invitation system onboarding wizard.
/// </summary>
internal record SaveUserInterestsCommand(
    Guid UserId,
    List<string> Interests
) : ICommand;
