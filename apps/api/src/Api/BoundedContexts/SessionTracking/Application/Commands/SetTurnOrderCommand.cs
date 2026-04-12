using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Session Flow v2.1 — T6.
/// Sets the turn order for a session using either an explicit manual order
/// or a cryptographically-seeded random shuffle. Emits a <c>turn_order_set</c>
/// diary event carrying the seed (for audit/replay of Random rolls).
/// </summary>
public sealed record SetTurnOrderCommand(
    Guid SessionId,
    Guid UserId,
    TurnOrderMethod Method,
    IReadOnlyList<Guid>? ManualOrder
) : ICommand<SetTurnOrderResult>;

public sealed record SetTurnOrderResult(
    string Method,
    int? Seed,
    IReadOnlyList<Guid> Order
);

public sealed class SetTurnOrderCommandValidator : AbstractValidator<SetTurnOrderCommand>
{
    public SetTurnOrderCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.ManualOrder)
            .NotNull()
            .NotEmpty()
            .When(x => x.Method == TurnOrderMethod.Manual)
            .WithMessage("ManualOrder is required when Method=Manual.");
    }
}
