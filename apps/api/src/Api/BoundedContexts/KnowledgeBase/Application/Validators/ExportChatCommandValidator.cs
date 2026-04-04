using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ExportChatCommand.
/// </summary>
internal sealed class ExportChatCommandValidator : AbstractValidator<ExportChatCommand>
{
    private static readonly string[] SupportedFormats = ["json", "markdown", "txt"];

    public ExportChatCommandValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty()
            .WithMessage("ThreadId is required");

        RuleFor(x => x.Format)
            .NotEmpty()
            .WithMessage("Format is required")
            .Must(f => SupportedFormats.Contains(f, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Format must be one of: json, markdown, txt");
    }
}
