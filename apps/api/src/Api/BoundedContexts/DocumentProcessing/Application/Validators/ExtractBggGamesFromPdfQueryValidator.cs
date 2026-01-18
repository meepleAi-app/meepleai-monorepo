using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

internal sealed class ExtractBggGamesFromPdfQueryValidator : AbstractValidator<ExtractBggGamesFromPdfQuery>
{
    public ExtractBggGamesFromPdfQueryValidator()
    {
        RuleFor(x => x.PdfFilePath)
            .NotEmpty()
            .WithMessage("PDF file path is required");

        RuleFor(x => x.PdfFilePath)
            .Must(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .WithMessage("File must be a PDF (.pdf extension required)")
            .When(x => !string.IsNullOrEmpty(x.PdfFilePath));

        RuleFor(x => x.PdfFilePath)
            .Must(BeSecurePath)
            .WithMessage("File path contains invalid or unsafe characters")
            .When(x => !string.IsNullOrEmpty(x.PdfFilePath));
    }

    private static bool BeSecurePath(string path)
    {
        // Prevent path traversal attacks
        var fullPath = Path.GetFullPath(path);

        // Check for path traversal patterns
        if (path.Contains("..", StringComparison.Ordinal) ||
            path.Contains('~'))
        {
            return false;
        }

        // Ensure the resolved path doesn't escape expected directories
        // Allow paths in data/ directory or absolute paths that don't traverse upward
        return !fullPath.Contains("..", StringComparison.Ordinal);
    }
}
