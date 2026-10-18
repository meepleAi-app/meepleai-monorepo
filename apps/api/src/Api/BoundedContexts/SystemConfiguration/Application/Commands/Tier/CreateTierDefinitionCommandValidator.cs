using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;

internal sealed class CreateTierDefinitionCommandValidator : AbstractValidator<CreateTierDefinitionCommand>
{
    public CreateTierDefinitionCommandValidator(IOptions<PdfProcessingOptions> pdfOptions)
    {
        ArgumentNullException.ThrowIfNull(pdfOptions);
        var maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("DisplayName is required")
            .MaximumLength(200)
            .WithMessage("DisplayName must not exceed 200 characters");

        RuleFor(x => x.Limits)
            .NotNull()
            .WithMessage("Limits is required");

        // #4212: a client that omits the field sends 0. Named here so the caller learns which
        // field is wrong; the domain would reject it too, but as a 400 that names nothing.
        RuleFor(x => x.Limits!.MaxLibraryGames)
            .GreaterThanOrEqualTo(1)
            .WithMessage("MaxLibraryGames must be at least 1")
            .When(x => x.Limits is not null);

        // #4449: a plan's PDF size is a commercial cap under the technical limit, not a second
        // limit above it. A larger value would promise uploads that every upload path refuses.
        RuleFor(x => x.Limits!.MaxPdfSizeBytes)
            .LessThanOrEqualTo(maxFileSizeBytes)
            .WithMessage($"MaxPdfSizeBytes must not exceed the PDF size limit ({maxFileSizeBytes} bytes, {PdfSizeLimit.Describe(maxFileSizeBytes)})")
            .When(x => x.Limits is not null);

        RuleFor(x => x.LlmModelTier)
            .NotEmpty()
            .WithMessage("LlmModelTier is required")
            .MaximumLength(100)
            .WithMessage("LlmModelTier must not exceed 100 characters");
    }
}
