using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

/// <summary>
/// Validator for <see cref="GetKbDocumentByIdQuery"/>.
/// Wave 3 Phase 3, PR #732 §6.3.1 / Issue #805.
/// </summary>
internal sealed class GetKbDocumentByIdQueryValidator : AbstractValidator<GetKbDocumentByIdQuery>
{
    public GetKbDocumentByIdQueryValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
    }
}
