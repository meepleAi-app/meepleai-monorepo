using Api.BoundedContexts.UserLibrary.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Validator for GetLibraryQuotaQuery to ensure UserId is not empty.
/// </summary>
internal sealed class GetLibraryQuotaQueryValidator : AbstractValidator<GetLibraryQuotaQuery>
{
    public GetLibraryQuotaQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
