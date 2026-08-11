using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Validators.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Validators.Queue;

/// <summary>
/// Unit tests for <see cref="BulkReindexReadyCommandValidator"/>. Issue #3269 — mirrors
/// <c>ReindexDocumentCommandValidatorTests</c> for the optional TargetVersion selector.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class BulkReindexReadyCommandValidatorTests
{
    private readonly BulkReindexReadyCommandValidator _validator = new();

    [Fact]
    public void TargetVersion_Null_Passes()
    {
        var result = _validator.TestValidate(new BulkReindexReadyCommand(Guid.NewGuid()));
        result.ShouldNotHaveValidationErrorFor(c => c.TargetVersion);
    }

    [Fact]
    public void TargetVersion_Current_Passes()
    {
        var current = IndexerVersionRegistry.Current.Version;
        var result = _validator.TestValidate(new BulkReindexReadyCommand(Guid.NewGuid(), current));
        result.ShouldNotHaveValidationErrorFor(c => c.TargetVersion);
    }

    [Fact]
    public void TargetVersion_LegacyV0_FailsAsNotSelectable()
    {
        var legacy = IndexerVersionRegistry.Legacy.Version;
        var result = _validator.TestValidate(new BulkReindexReadyCommand(Guid.NewGuid(), legacy));
        result.ShouldHaveValidationErrorFor(c => c.TargetVersion)
            .WithErrorMessage($"Indexer version '{legacy}' is not selectable (legacy marker).");
    }

    [Fact]
    public void TargetVersion_Unknown_FailsAsUnknown()
    {
        var result = _validator.TestValidate(new BulkReindexReadyCommand(Guid.NewGuid(), "v99"));
        result.ShouldHaveValidationErrorFor(c => c.TargetVersion)
            .WithErrorMessage("Unknown indexer version 'v99'.");
    }

    [Fact]
    public void RequestedBy_Empty_Fails()
    {
        var result = _validator.TestValidate(new BulkReindexReadyCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(c => c.RequestedBy);
    }
}
