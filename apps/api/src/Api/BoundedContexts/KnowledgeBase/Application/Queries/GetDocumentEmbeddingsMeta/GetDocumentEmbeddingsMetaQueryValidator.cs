using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;

internal sealed class GetDocumentEmbeddingsMetaQueryValidator
    : AbstractValidator<GetDocumentEmbeddingsMetaQuery>
{
    public GetDocumentEmbeddingsMetaQueryValidator()
    {
        RuleFor(q => q.DocId)
            .NotEmpty()
            .WithMessage("DocId is required.");
    }
}
