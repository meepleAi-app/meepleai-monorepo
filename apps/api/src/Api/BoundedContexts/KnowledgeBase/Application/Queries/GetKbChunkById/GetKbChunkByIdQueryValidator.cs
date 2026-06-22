using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;

/// <summary>
/// Validator for <see cref="GetKbChunkByIdQuery"/>.
/// Wave 3 Phase 3, PR #732 §6.3.3 / Issue #805.
/// </summary>
internal sealed class GetKbChunkByIdQueryValidator : AbstractValidator<GetKbChunkByIdQuery>
{
    public GetKbChunkByIdQueryValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
        RuleFor(x => x.ChunkId).NotEmpty();
    }
}
