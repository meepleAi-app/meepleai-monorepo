using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch;
using Api.BoundedContexts.SharedGameCatalog.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class EnrichCatalogCoverBatchCommandValidatorTests
{
    private readonly EnrichCatalogCoverBatchCommandValidator _sut = new();

    [Fact]
    public void Validate_NullList_FailsOnNotNull()
    {
        var cmd = new EnrichCatalogCoverBatchCommand(null!);
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.GameIds);
    }

    [Fact]
    public void Validate_EmptyList_FailsOnNotEmpty()
    {
        var cmd = new EnrichCatalogCoverBatchCommand(Array.Empty<Guid>());
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.GameIds);
    }

    [Fact]
    public void Validate_TooLarge_FailsOnMaxBatchSize()
    {
        var ids = Enumerable.Range(0, EnrichCatalogCoverBatchCommandValidator.MaxBatchSize + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();
        var cmd = new EnrichCatalogCoverBatchCommand(ids);

        var result = _sut.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.GameIds)
            .WithErrorMessage($"GameIds must not exceed {EnrichCatalogCoverBatchCommandValidator.MaxBatchSize} entries per batch.");
    }

    [Fact]
    public void Validate_ContainsGuidEmpty_Fails()
    {
        var cmd = new EnrichCatalogCoverBatchCommand(new[] { Guid.NewGuid(), Guid.Empty });
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.GameIds);
    }

    [Fact]
    public void Validate_ContainsDuplicates_Fails()
    {
        var id = Guid.NewGuid();
        var cmd = new EnrichCatalogCoverBatchCommand(new[] { id, id });
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.GameIds);
    }

    [Fact]
    public void Validate_ValidBatch_Passes()
    {
        var cmd = new EnrichCatalogCoverBatchCommand(new[] { Guid.NewGuid(), Guid.NewGuid() });
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MaxBatchSize_IsExactly200()
    {
        // Regression guard: the spec locks 200 as the agreed cap. Bumping
        // requires a coordinated review with ops because the Wikimedia
        // 1 req/sec SPARQL rate-limit makes a single batch take ~MaxBatchSize
        // seconds at worst.
        EnrichCatalogCoverBatchCommandValidator.MaxBatchSize.Should().Be(200);
    }
}
