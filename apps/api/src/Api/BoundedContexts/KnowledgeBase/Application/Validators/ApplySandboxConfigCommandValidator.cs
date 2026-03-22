using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ApplySandboxConfigCommand.
/// </summary>
internal sealed class ApplySandboxConfigCommandValidator : AbstractValidator<ApplySandboxConfigCommand>
{
    public ApplySandboxConfigCommandValidator()
    {
        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("AdminUserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.Config)
            .NotNull()
            .WithMessage("Config is required");

        When(x => x.Config != null, () =>
        {
            RuleFor(x => x.Config.TopK)
                .GreaterThan(0)
                .When(x => x.Config.TopK.HasValue)
                .WithMessage("TopK must be greater than 0");

            RuleFor(x => x.Config.Temperature)
                .InclusiveBetween(0.0, 2.0)
                .When(x => x.Config.Temperature.HasValue)
                .WithMessage("Temperature must be between 0.0 and 2.0");

            RuleFor(x => x.Config.MaxTokens)
                .GreaterThan(0)
                .When(x => x.Config.MaxTokens.HasValue)
                .WithMessage("MaxTokens must be greater than 0");

            RuleFor(x => x.Config.Model)
                .MaximumLength(200)
                .When(x => x.Config.Model != null)
                .WithMessage("Model cannot exceed 200 characters");

            RuleFor(x => x.Config.SystemPromptOverride)
                .MaximumLength(2000)
                .When(x => x.Config.SystemPromptOverride != null)
                .WithMessage("SystemPromptOverride cannot exceed 2000 characters");

            RuleFor(x => x.Config.ChunkSize)
                .GreaterThan(0)
                .When(x => x.Config.ChunkSize.HasValue)
                .WithMessage("ChunkSize must be greater than 0");

            RuleFor(x => x.Config.ChunkOverlap)
                .GreaterThanOrEqualTo(0)
                .When(x => x.Config.ChunkOverlap.HasValue)
                .WithMessage("ChunkOverlap cannot be negative");
        });
    }
}
