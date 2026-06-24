using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BulkRejectMechanicClaimsCommandValidatorTests
{
    private readonly BulkRejectMechanicClaimsCommandValidator _validator = new();

    private static BulkRejectMechanicClaimsCommand Valid(
        IReadOnlyList<Guid>? claimIds = null, string reason = "verbatim copy") =>
        new(Guid.NewGuid(), claimIds ?? new[] { Guid.NewGuid() }, reason, Guid.NewGuid());

    [Fact]
    public void Valid_command_passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_analysisId_fails()
    {
        var cmd = Valid() with { AnalysisId = Guid.Empty };
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_reviewerId_fails()
    {
        var cmd = Valid() with { ReviewerId = Guid.Empty };
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_claimIds_list_fails()
    {
        var result = _validator.Validate(Valid(claimIds: Array.Empty<Guid>()));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BulkRejectMechanicClaimsCommand.ClaimIds));
    }

    [Fact]
    public void ClaimIds_containing_empty_guid_fails()
    {
        _validator.Validate(Valid(claimIds: new[] { Guid.NewGuid(), Guid.Empty })).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_reason_fails()
    {
        _validator.Validate(Valid(reason: "")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Whitespace_reason_fails()
    {
        _validator.Validate(Valid(reason: "   ")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Reason_over_500_chars_fails()
    {
        _validator.Validate(Valid(reason: new string('x', 501))).IsValid.Should().BeFalse();
    }
}
