using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ReuploadBggCover;

/// <summary>
/// Boundary validation per <see cref="ReuploadBggCoverCommand"/> — un 422 via la pipeline
/// FluentValidation invece di un 500 su identificativi vuoti.
/// </summary>
internal sealed class ReuploadBggCoverCommandValidator : AbstractValidator<ReuploadBggCoverCommand>
{
    public ReuploadBggCoverCommandValidator()
    {
        RuleFor(x => x.GameId).NotEqual(Guid.Empty).WithMessage("GameId is required");
        RuleFor(x => x.AdminId).NotEqual(Guid.Empty).WithMessage("AdminId is required");
    }
}
