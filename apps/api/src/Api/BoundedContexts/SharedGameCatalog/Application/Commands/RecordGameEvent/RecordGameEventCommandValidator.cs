using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.RecordGameEvent;

/// <summary>
/// Validator for RecordGameEventCommand.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
internal sealed class RecordGameEventCommandValidator : AbstractValidator<RecordGameEventCommand>
{
    public RecordGameEventCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required.");

        RuleFor(x => x.EventType)
            .IsInEnum()
            .WithMessage("Invalid event type.");
    }
}
