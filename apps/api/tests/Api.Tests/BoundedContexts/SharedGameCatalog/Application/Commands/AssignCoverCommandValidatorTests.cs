using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Domain.Covers;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AssignCoverCommandValidatorTests
{
    private static readonly Guid GameId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly AssignCoverCommandValidator _validator = new();

    private static AssignCoverCommand Valid() =>
        new(GameId, CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId, 0.5, 0.5);

    [Fact]
    public void Passes_ForAValidCommand()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Fails_WhenGameIdEmpty()
    {
        _validator.TestValidate(Valid() with { GameId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void Fails_WhenAdminIdEmpty()
    {
        _validator.TestValidate(Valid() with { AdminId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.AdminId);
    }

    [Theory]
    [InlineData(-0.01, 0.5)]
    [InlineData(1.01, 0.5)]
    [InlineData(0.5, -0.01)]
    [InlineData(0.5, 1.01)]
    public void Fails_WhenFocalOutOfRange(double x, double y)
    {
        var result = _validator.TestValidate(Valid() with { FocalX = x, FocalY = y });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_WhenContextUndefined()
    {
        _validator.TestValidate(Valid() with { Context = (CoverContext)99 })
            .ShouldHaveValidationErrorFor(x => x.Context);
    }

    [Fact]
    public void Fails_WhenSourceUndefined()
    {
        _validator.TestValidate(Valid() with { Source = (CoverAssignmentSource)99 })
            .ShouldHaveValidationErrorFor(x => x.Source);
    }
}
