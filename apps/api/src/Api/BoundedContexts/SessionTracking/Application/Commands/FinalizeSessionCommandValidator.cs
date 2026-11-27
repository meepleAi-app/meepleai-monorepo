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
            .Must(ranks => IsCompetitionRanking(ranks))
            .WithMessage("Ranks must start at 1; tied players share a rank and the next rank skips the places they took (1, 1, 3)");
    }

    /// <summary>
    /// #4780: a competition ranking, the one the live game computes. Sorted, every rank is either the rank before it
    /// (a tie) or its own position: 1, 1, 3 and 1, 2, 2 are valid; 1, 1, 2 and 1, 3 are not. Without ties it is
    /// exactly 1..N, the rule this replaced, so every ranking accepted before is still accepted.
    /// </summary>
    private static bool IsCompetitionRanking(Dictionary<Guid, int> ranks)
    {
        if (ranks == null || ranks.Count == 0)
            return false;

        var sortedRanks = ranks.Values.OrderBy(r => r).ToList();

        for (int i = 0; i < sortedRanks.Count; i++)
        {
            var isTieWithThePrevious = i > 0 && sortedRanks[i] == sortedRanks[i - 1];
            if (!isTieWithThePrevious && sortedRanks[i] != i + 1)
                return false;
        }

        return true;
    }
}
