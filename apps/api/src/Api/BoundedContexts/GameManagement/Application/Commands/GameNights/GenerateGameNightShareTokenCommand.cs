using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Organiser generates (or rotates) a public share token for the night's summary — Issue #2702.
/// Returns the new token.
/// </summary>
internal record GenerateGameNightShareTokenCommand(Guid GameNightId, Guid UserId) : ICommand<string>;
