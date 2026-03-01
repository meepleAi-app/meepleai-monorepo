using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.GameWizard;

/// <summary>
/// Command to create a game from the admin wizard using BGG data.
/// Orchestrates SharedGame creation via existing ImportGameFromBggCommand.
/// </summary>
/// <param name="BggId">BoardGameGeek game ID to import</param>
/// <param name="CreatedByUserId">Admin user performing the import</param>
public record CreateGameFromWizardCommand(
    int BggId,
    Guid CreatedByUserId
) : ICommand<CreateGameFromWizardResult>;

/// <summary>
/// Result of game creation from wizard.
/// </summary>
public record CreateGameFromWizardResult
{
    public required Guid SharedGameId { get; init; }
    public required string Title { get; init; }
    public required int BggId { get; init; }
    public required string Status { get; init; }
}
