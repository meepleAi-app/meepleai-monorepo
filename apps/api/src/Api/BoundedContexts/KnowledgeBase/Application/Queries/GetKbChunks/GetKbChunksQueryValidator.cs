using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Validator for <see cref="GetKbChunksQuery"/>.
/// Wave 3 Phase 3, PR #732 §6.3.2 / Issue #805.
/// </summary>
internal sealed class GetKbChunksQueryValidator : AbstractValidator<GetKbChunksQuery>
{
    public GetKbChunksQueryValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}
