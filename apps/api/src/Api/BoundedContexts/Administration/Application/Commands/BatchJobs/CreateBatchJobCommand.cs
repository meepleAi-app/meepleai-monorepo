using Api.BoundedContexts.Administration.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.BatchJobs;

/// <summary>
/// Command to create a new batch job (Issue #3693)
/// </summary>
internal sealed record CreateBatchJobCommand(
    JobType Type,
    string Parameters,
    Guid CreatedBy) : ICommand<Guid>;

/// <summary>
/// Validator for CreateBatchJobCommand (Issue #3693)
/// </summary>
internal sealed class CreateBatchJobCommandValidator : AbstractValidator<CreateBatchJobCommand>
{
    public CreateBatchJobCommandValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Invalid job type");

        RuleFor(x => x.Parameters)
            .NotEmpty()
            .WithMessage("Parameters cannot be empty")
            .MaximumLength(10000)
            .WithMessage("Parameters must not exceed 10000 characters")
            .Must(BeValidJson)
            .WithMessage("Parameters must be valid JSON");

        RuleFor(x => x.CreatedBy)
            .NotEmpty()
            .WithMessage("CreatedBy is required");
    }

    private static bool BeValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
