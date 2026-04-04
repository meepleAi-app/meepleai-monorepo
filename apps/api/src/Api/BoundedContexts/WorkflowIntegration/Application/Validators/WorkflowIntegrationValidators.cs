#pragma warning disable MA0048 // File name must match type name - Contains related validators
using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8NConfig;
using Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8NTemplates;
using FluentValidation;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Validators;

internal sealed class CreateN8NConfigCommandValidator : AbstractValidator<CreateN8NConfigCommand>
{
    public CreateN8NConfigCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Name is required (max 200 chars)");

        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("BaseUrl is required (max 500 chars)");

        RuleFor(x => x.ApiKeyEncrypted)
            .NotEmpty()
            .WithMessage("ApiKeyEncrypted is required");

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty()
            .WithMessage("CreatedByUserId is required");

        RuleFor(x => x.WebhookUrl)
            .MaximumLength(500)
            .When(x => x.WebhookUrl != null)
            .WithMessage("WebhookUrl cannot exceed 500 characters");
    }
}

internal sealed class DeleteN8NConfigCommandValidator : AbstractValidator<DeleteN8NConfigCommand>
{
    public DeleteN8NConfigCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty()
            .WithMessage("ConfigId is required");
    }
}

internal sealed class UpdateN8NConfigCommandValidator : AbstractValidator<UpdateN8NConfigCommand>
{
    public UpdateN8NConfigCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty()
            .WithMessage("ConfigId is required");

        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name != null)
            .WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.BaseUrl)
            .MaximumLength(500)
            .When(x => x.BaseUrl != null)
            .WithMessage("BaseUrl cannot exceed 500 characters");

        RuleFor(x => x.WebhookUrl)
            .MaximumLength(500)
            .When(x => x.WebhookUrl != null)
            .WithMessage("WebhookUrl cannot exceed 500 characters");
    }
}

internal sealed class TestN8NConnectionCommandValidator : AbstractValidator<TestN8NConnectionCommand>
{
    public TestN8NConnectionCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty()
            .WithMessage("ConfigId is required");
    }
}

internal sealed class ImportN8NTemplateCommandValidator : AbstractValidator<ImportN8NTemplateCommand>
{
    public ImportN8NTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("TemplateId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
