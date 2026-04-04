using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for GetPdfChunksPreviewQuery.
/// RAG Sandbox: Chunk preview panel pagination and search validation.
/// </summary>
internal sealed class GetPdfChunksPreviewQueryValidator : AbstractValidator<GetPdfChunksPreviewQuery>
{
    public GetPdfChunksPreviewQueryValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF document ID is required");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");
    }
}
