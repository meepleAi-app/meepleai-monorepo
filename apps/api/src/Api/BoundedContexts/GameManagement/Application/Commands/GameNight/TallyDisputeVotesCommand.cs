using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Tallies all cast votes on a dispute and determines the final outcome.
/// Optionally sets an override rule if the verdict was overridden.
/// Appends a legacy entry to the session for backward compatibility.
/// </summary>
internal record TallyDisputeVotesCommand(
    Guid DisputeId,
    string? OverrideRule = null
) : ICommand;
