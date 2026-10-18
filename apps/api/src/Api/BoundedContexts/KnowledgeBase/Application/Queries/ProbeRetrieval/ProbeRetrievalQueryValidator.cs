using Api.Helpers;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.ProbeRetrieval;

/// <summary>#4482: la sonda accetta le domande che accetta la chat (<see cref="QueryValidator"/>), su un gioco.</summary>
internal sealed class ProbeRetrievalQueryValidator : AbstractValidator<ProbeRetrievalQuery>
{
    public ProbeRetrievalQueryValidator()
    {
        RuleFor(q => q.GameId).NotEmpty();

        RuleFor(q => q.Question)
            .Must(question => QueryValidator.ValidateQuery(question) is null)
            .WithMessage(q => QueryValidator.ValidateQuery(q.Question) ?? string.Empty);
    }
}
