using Api.BoundedContexts.SessionTracking.Domain.Scoring;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Asse A semantic alignment #1896 (T10, DEC-1): FluentValidation rule that delegates
/// JSON schema validation to the matching <see cref="IScoringStrategy"/> resolved via
/// <see cref="ScoringStrategyFactory"/>.
///
/// <para>Per-rule semantics:
/// <list type="bullet">
///   <item><c>SessionId</c>: not empty.</item>
///   <item><c>ScoreData</c>: not empty AND must pass the polymorphic strategy validator
///   (Points/BinaryWin/Objectives/Ranking schema check).</item>
/// </list>
/// </para>
/// </summary>
public class UpdateSessionScoresCommandValidator : AbstractValidator<UpdateSessionScoresCommand>
{
    public UpdateSessionScoresCommandValidator(ScoringStrategyFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.RequestedBy)
            .NotEmpty()
            .WithMessage("RequestedBy is required (extracted from authenticated user claims)");

        RuleFor(x => x.ScoreData)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("ScoreData is required")
            .Custom((scoreData, context) =>
            {
                var cmd = context.InstanceToValidate;
                IScoringStrategy strategy;
                try
                {
                    strategy = factory.GetStrategy(cmd.ScoringType);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    context.AddFailure(nameof(UpdateSessionScoresCommand.ScoringType), ex.Message);
                    return;
                }

                var result = strategy.Validate(scoreData);
                if (!result.IsValid)
                {
                    foreach (var err in result.Errors)
                    {
                        context.AddFailure(nameof(UpdateSessionScoresCommand.ScoreData), err);
                    }
                }
            });
    }
}
