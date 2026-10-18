using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// #4443 R2: the chunked upload accepts the same priorities as <c>/ingest/pdf</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4443")]
public sealed class CompleteChunkedUploadCommandValidatorTests
{
    private readonly CompleteChunkedUploadCommandValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("normal")]
    [InlineData("urgent")]
    [InlineData("Urgent")]
    public void Validate_WithAKnownOrNoPriority_Passes(string? priority)
    {
        var result = _validator.TestValidate(new CompleteChunkedUploadCommand(Guid.NewGuid(), Guid.NewGuid(), priority));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("high")]
    [InlineData("")]
    public void Validate_WithAnUnknownPriority_Fails(string priority)
    {
        var result = _validator.TestValidate(new CompleteChunkedUploadCommand(Guid.NewGuid(), Guid.NewGuid(), priority));

        result.ShouldHaveValidationErrorFor(c => c.Priority);
    }
}
