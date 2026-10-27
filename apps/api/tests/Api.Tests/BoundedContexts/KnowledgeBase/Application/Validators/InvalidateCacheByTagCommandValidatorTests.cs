using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Tests for <see cref="InvalidateCacheByTagCommandValidator"/>.
/// #4561: <c>IHybridCacheService.RemoveByTagAsync</c> now also invalidates the tag on the Microsoft
/// <c>HybridCache</c>, where <c>*</c> is the wildcard that drops every entry. Before, a tag <c>*</c> on
/// <c>DELETE /admin/cache/tags/*</c> was a no-op (the wrapper's set <c>cache:tag:*</c> is empty); the
/// validator must keep it from turning into a flush of the whole cache.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class InvalidateCacheByTagCommandValidatorTests
{
    private readonly InvalidateCacheByTagCommandValidator _validator = new();

    [Theory]
    [InlineData("search-games")]
    [InlineData("shared-game:3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    [InlineData("user:123")]
    public void Validate_WithAnOrdinaryTag_HasNoError(string tag)
    {
        var result = _validator.TestValidate(new InvalidateCacheByTagCommand(tag));

        result.ShouldNotHaveValidationErrorFor(x => x.Tag);
    }

    [Theory]
    [InlineData("*")]
    public void Validate_WithTheHybridCacheWildcard_HasTagError(string tag)
    {
        var result = _validator.TestValidate(new InvalidateCacheByTagCommand(tag));

        result.ShouldHaveValidationErrorFor(x => x.Tag);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithAnEmptyTag_HasTagError(string tag)
    {
        var result = _validator.TestValidate(new InvalidateCacheByTagCommand(tag));

        result.ShouldHaveValidationErrorFor(x => x.Tag);
    }

    [Fact]
    public void Validate_WithATagLongerThan200Characters_HasTagError()
    {
        var result = _validator.TestValidate(new InvalidateCacheByTagCommand(new string('a', 201)));

        result.ShouldHaveValidationErrorFor(x => x.Tag);
    }
}
