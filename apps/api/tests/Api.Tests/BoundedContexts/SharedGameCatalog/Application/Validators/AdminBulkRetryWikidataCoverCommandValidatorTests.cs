using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class AdminBulkRetryWikidataCoverCommandValidatorTests
{
    private readonly AdminBulkRetryWikidataCoverCommandValidator _sut = new();

    [Fact]
    public async Task ValidCommand_PassesValidation()
    {
        var cmd = new AdminBulkRetryWikidataCoverCommand(
            AttemptIds: new[] { Guid.NewGuid(), Guid.NewGuid() },
            TriggeredByUserId: Guid.NewGuid());

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task EmptyAttemptIds_Fails()
    {
        var cmd = new AdminBulkRetryWikidataCoverCommand(
            AttemptIds: Array.Empty<Guid>(),
            TriggeredByUserId: Guid.NewGuid());

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.AttemptIds);
    }

    [Fact]
    public async Task OverMaxBatchSize_Fails()
    {
        var ids = Enumerable.Range(0, AdminBulkRetryWikidataCoverCommandValidator.MaxBatchSize + 1)
            .Select(_ => Guid.NewGuid()).ToArray();
        var cmd = new AdminBulkRetryWikidataCoverCommand(ids, Guid.NewGuid());

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.AttemptIds)
            .WithErrorMessage($"AttemptIds cannot exceed {AdminBulkRetryWikidataCoverCommandValidator.MaxBatchSize} per request.");
    }

    [Fact]
    public async Task ContainsGuidEmpty_Fails()
    {
        var cmd = new AdminBulkRetryWikidataCoverCommand(
            AttemptIds: new[] { Guid.NewGuid(), Guid.Empty },
            TriggeredByUserId: Guid.NewGuid());

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.AttemptIds);
    }

    [Fact]
    public async Task ContainsDuplicates_Fails()
    {
        var dup = Guid.NewGuid();
        var cmd = new AdminBulkRetryWikidataCoverCommand(
            AttemptIds: new[] { dup, dup },
            TriggeredByUserId: Guid.NewGuid());

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.AttemptIds);
    }

    [Fact]
    public async Task EmptyTriggeredBy_Fails()
    {
        var cmd = new AdminBulkRetryWikidataCoverCommand(
            AttemptIds: new[] { Guid.NewGuid() },
            TriggeredByUserId: Guid.Empty);

        var r = await _sut.TestValidateAsync(cmd);
        r.ShouldHaveValidationErrorFor(c => c.TriggeredByUserId);
    }

    [Fact]
    public void MaxBatchSize_Is50()
    {
        // ADR-driven cap; if this changes the operations runbook + the FE
        // multi-select limit MUST update too.
        AdminBulkRetryWikidataCoverCommandValidator.MaxBatchSize
            .Should().Be(50, "DEC-3e rate-limiter budget caps each bulk click at 50 enrichments");
    }
}
