using MediatR;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class FinalizeSessionCommandValidator : AbstractValidator<FinalizeSessionCommand>
{
    public FinalizeSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.FinalRanks)
            .NotEmpty()
            .WithMessage("FinalRanks required")
            .Must(ranks => HasConsecutiveRanks(ranks))
            .WithMessage("Ranks must be consecutive from 1 to N with no gaps");
    }

    private bool HasConsecutiveRanks(Dictionary<Guid, int> ranks)
    {
        if (ranks == null || ranks.Count == 0)
            return false;

        var sortedRanks = ranks.Values.OrderBy(r => r).ToList();

        // Check if starts at 1 and is consecutive
        for (int i = 0; i < sortedRanks.Count; i++)
        {
            if (sortedRanks[i] != i + 1)
                return false;
        }

        return true;
    }
}
