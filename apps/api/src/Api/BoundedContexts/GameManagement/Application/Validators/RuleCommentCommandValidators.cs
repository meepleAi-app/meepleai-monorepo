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

/// <summary>
/// Validator for CreateRuleCommentCommand.
/// Ensures required fields are provided and text is within bounds.
/// </summary>
internal sealed class CreateRuleCommentCommandValidator : AbstractValidator<CreateRuleCommentCommand>
{
    public CreateRuleCommentCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("Game ID is required");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Version is required")
            .MaximumLength(200).WithMessage("Version must not exceed 200 characters");

        RuleFor(x => x.CommentText)
            .NotEmpty().WithMessage("Comment text is required")
            .MaximumLength(2000).WithMessage("Comment text must not exceed 2000 characters");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}

/// <summary>
/// Validator for ReplyToRuleCommentCommand.
/// Ensures ParentCommentId and UserId are non-empty and CommentText is provided.
/// </summary>
internal sealed class ReplyToRuleCommentCommandValidator : AbstractValidator<ReplyToRuleCommentCommand>
{
    public ReplyToRuleCommentCommandValidator()
    {
        RuleFor(x => x.ParentCommentId)
            .NotEmpty().WithMessage("Parent comment ID is required");

        RuleFor(x => x.CommentText)
            .NotEmpty().WithMessage("Comment text is required")
            .MaximumLength(2000).WithMessage("Comment text must not exceed 2000 characters");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}

/// <summary>
/// Validator for UpdateRuleCommentCommand.
/// Ensures CommentId and UserId are non-empty and CommentText is provided.
/// </summary>
internal sealed class UpdateRuleCommentCommandValidator : AbstractValidator<UpdateRuleCommentCommand>
{
    public UpdateRuleCommentCommandValidator()
    {
        RuleFor(x => x.CommentId)
            .NotEmpty().WithMessage("Comment ID is required");

        RuleFor(x => x.CommentText)
            .NotEmpty().WithMessage("Comment text is required")
            .MaximumLength(2000).WithMessage("Comment text must not exceed 2000 characters");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}
