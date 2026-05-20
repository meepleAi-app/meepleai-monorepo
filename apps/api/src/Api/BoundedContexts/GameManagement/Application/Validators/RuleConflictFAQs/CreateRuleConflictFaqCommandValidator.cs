using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.RuleConflictFAQs;

/// <summary>
/// Validator for CreateRuleConflictFaqCommand.
/// Issue #3966: CQRS validation for conflict FAQ creation.
/// </summary>
internal sealed class CreateRuleConflictFaqCommandValidator : AbstractValidator<CreateRuleConflictFaqCommand>
{
    private readonly IGameCoreDataProvider _gameCoreData;

    public CreateRuleConflictFaqCommandValidator(IGameCoreDataProvider gameCoreData)
    {
        _gameCoreData = gameCoreData ?? throw new ArgumentNullException(nameof(gameCoreData));

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required")
            .MustAsync(GameExists)
            .WithMessage("Game not found");

        RuleFor(x => x.ConflictType)
            .IsInEnum()
            .WithMessage("Invalid conflict type");

        RuleFor(x => x.Pattern)
            .NotEmpty()
            .WithMessage("Pattern is required")
            .MaximumLength(200)
            .WithMessage("Pattern cannot exceed 200 characters");

        RuleFor(x => x.Resolution)
            .NotEmpty()
            .WithMessage("Resolution is required")
            .MaximumLength(2000)
            .WithMessage("Resolution cannot exceed 2000 characters");

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 10)
            .WithMessage("Priority must be between 1 and 10");
    }

    private async Task<bool> GameExists(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await _gameCoreData.GetCoreDataAsync(GameRef.Shared(gameId), cancellationToken).ConfigureAwait(false);
        return game is not null;
    }
}
