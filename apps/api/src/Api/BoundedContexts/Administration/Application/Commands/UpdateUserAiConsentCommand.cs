using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to create or update user AI consent preferences (Issue #5512)
/// </summary>
internal record UpdateUserAiConsentCommand(
    Guid UserId,
    bool ConsentedToAiProcessing,
    bool ConsentedToExternalProviders,
    string ConsentVersion
) : ICommand;
