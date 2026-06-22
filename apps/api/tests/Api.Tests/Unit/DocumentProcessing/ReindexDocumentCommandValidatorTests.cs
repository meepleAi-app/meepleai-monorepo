using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class ReindexDocumentCommandValidatorTests
{
    private readonly ReindexDocumentCommandValidator _validator = new();

    [Fact]
    public void PdfId_Empty_FailsWithNotEmpty()
    {
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(c => c.PdfId);
    }

    [Fact]
    public void IndexerVersion_Null_Passes()
    {
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.NewGuid()));
        result.ShouldNotHaveValidationErrorFor(c => c.IndexerVersion);
    }

    [Fact]
    public void IndexerVersion_Current_Passes()
    {
        var current = IndexerVersionRegistry.Current.Version;
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.NewGuid(), current));
        result.ShouldNotHaveValidationErrorFor(c => c.IndexerVersion);
    }

    [Fact]
    public void IndexerVersion_LegacyV0_FailsAsNotSelectable()
    {
        var legacy = IndexerVersionRegistry.Legacy.Version;
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.NewGuid(), legacy));
        result.ShouldHaveValidationErrorFor(c => c.IndexerVersion)
            .WithErrorMessage($"Indexer version '{legacy}' is not selectable (legacy marker).");
    }

    [Fact]
    public void IndexerVersion_Unknown_FailsAsUnknown()
    {
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.NewGuid(), "v99"));
        result.ShouldHaveValidationErrorFor(c => c.IndexerVersion)
            .WithErrorMessage("Unknown indexer version 'v99'.");
    }
}
