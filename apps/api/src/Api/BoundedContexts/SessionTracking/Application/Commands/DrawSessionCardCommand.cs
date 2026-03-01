using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command for a player to draw cards from a deck in a session.
/// Requires at least Player role — spectators cannot draw cards.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
public record DrawSessionCardCommand(
    Guid SessionId,
    Guid DeckId,
    Guid ParticipantId,
    Guid RequesterId,
    int Count = 1
) : IRequest<DrawSessionCardResult>, IRequireSessionRole
{
    /// <summary>Minimum role: Player.</summary>
    public ParticipantRole MinimumRole => ParticipantRole.Player;
}

/// <summary>
/// Result of drawing cards in a player action.
/// </summary>
public record DrawSessionCardResult(
    Guid DeckId,
    Guid ParticipantId,
    IReadOnlyList<CardDrawDto> Cards,
    int RemainingCards
);

/// <summary>
/// Lightweight card DTO for the draw-card action response.
/// </summary>
public record CardDrawDto(
    Guid Id,
    string Name,
    string? ImageUrl,
    string? Suit,
    string? Value
);

/// <summary>
/// FluentValidation validator for DrawSessionCardCommand.
/// </summary>
public class DrawSessionCardCommandValidator : AbstractValidator<DrawSessionCardCommand>
{
    public DrawSessionCardCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.DeckId)
            .NotEmpty()
            .WithMessage("DeckId is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x.Count)
            .GreaterThan(0)
            .WithMessage("Count must be at least 1")
            .LessThanOrEqualTo(10)
            .WithMessage("Count must be 10 or fewer");
    }
}
