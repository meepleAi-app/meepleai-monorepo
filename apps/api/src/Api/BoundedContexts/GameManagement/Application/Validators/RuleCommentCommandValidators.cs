using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for DeleteRuleCommentCommand.
/// Ensures GUID properties are non-empty.
/// </summary>
internal sealed class DeleteRuleCommentCommandValidator : AbstractValidator<DeleteRuleCommentCommand>
{
    public DeleteRuleCommentCommandValidator()
    {
        RuleFor(x => x.CommentId)
            .NotEmpty().WithMessage("Comment ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}

/// <summary>
/// Validator for ResolveRuleCommentCommand.
/// Ensures GUID properties are non-empty.
/// </summary>
internal sealed class ResolveRuleCommentCommandValidator : AbstractValidator<ResolveRuleCommentCommand>
{
    public ResolveRuleCommentCommandValidator()
    {
        RuleFor(x => x.CommentId)
            .NotEmpty().WithMessage("Comment ID is required");

        RuleFor(x => x.ResolvedByUserId)
            .NotEmpty().WithMessage("Resolved-by user ID is required");
    }
}

/// <summary>
/// Validator for UnresolveRuleCommentCommand.
/// Ensures GUID properties are non-empty.
/// </summary>
internal sealed class UnresolveRuleCommentCommandValidator : AbstractValidator<UnresolveRuleCommentCommand>
{
    public UnresolveRuleCommentCommandValidator()
    {
        RuleFor(x => x.CommentId)
            .NotEmpty().WithMessage("Comment ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}
