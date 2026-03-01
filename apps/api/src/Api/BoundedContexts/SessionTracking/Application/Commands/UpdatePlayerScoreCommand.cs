using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command for a player to update their own score.
/// Implements IRequireSessionRole to enforce Player-level authorization via the
/// ValidatePlayerRoleBehavior MediatR pipeline behavior.
/// Includes optimistic concurrency RowVersion for conflict detection.
/// Issue #4765 - Player Action Endpoints + Host Validation + Conflict Resolution
/// </summary>
public record UpdatePlayerScoreCommand(
    Guid SessionId,
    Guid ParticipantId,
    Guid RequesterId,
    decimal ScoreValue,
    int? RoundNumber = null,
    string? Category = null
) : IRequest<UpdatePlayerScoreResult>, IRequireSessionRole
{
    /// <summary>Minimum role: Player (Spectators cannot update scores).</summary>
    public ParticipantRole MinimumRole => ParticipantRole.Player;
}

/// <summary>
/// Result of a successful player score update.
/// </summary>
public record UpdatePlayerScoreResult(
    Guid ScoreEntryId,
    decimal NewTotal,
    int NewRank
);

/// <summary>
/// FluentValidation validator for UpdatePlayerScoreCommand.
/// </summary>
public class UpdatePlayerScoreCommandValidator : AbstractValidator<UpdatePlayerScoreCommand>
{
    public UpdatePlayerScoreCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x.ScoreValue)
            .NotEqual(0m)
            .WithMessage("ScoreValue cannot be zero")
            .InclusiveBetween(-9999m, 9999m)
            .WithMessage("ScoreValue must be between -9999 and 9999");

        RuleFor(x => x.RoundNumber)
            .GreaterThanOrEqualTo(1)
            .When(x => x.RoundNumber.HasValue)
            .WithMessage("RoundNumber must be >= 1 when specified");

        RuleFor(x => x)
            .Must(x => x.RoundNumber.HasValue || !string.IsNullOrWhiteSpace(x.Category))
            .WithMessage("Either RoundNumber or Category must be provided");
    }
}
