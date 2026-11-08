using Api.BoundedContexts.KnowledgeBase.Application.Commands.RagEnhancements;
using Api.BoundedContexts.KnowledgeBase.Application.Validators.RagEnhancements;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators.RagEnhancements;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4594")]
public sealed class ToggleRagEnhancementCommandValidatorTests
{
    private readonly ToggleRagEnhancementCommandValidator _validator = new();

    public static TheoryData<string> EveryRagEnhancementKey() => new(FeatureFlagConstants.RagEnhancements);

    [Theory]
    [MemberData(nameof(EveryRagEnhancementKey))]
    public void Validate_WithAKnownKeyAndAnAdmin_Passes(string key)
    {
        _validator.TestValidate(new ToggleRagEnhancementCommand(key, Guid.NewGuid()))
            .ShouldNotHaveAnyValidationErrors();
        _validator.TestValidate(new ToggleRagEnhancementCommand(key, Guid.NewGuid(), UserTier.Premium))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutTheAdminId_Fails()
    {
        // Guid.Empty is what FeatureFlagService used before #4594 when the route passed no id.
        var result = _validator.TestValidate(
            new ToggleRagEnhancementCommand("rag.enhancement.crag-evaluation", Guid.Empty));

        result.ShouldHaveValidationErrorFor(c => c.AdminId);
    }

    [Theory]
    [InlineData("rag.enhancement.not-a-real-one")]
    [InlineData("basic_chat")]
    [InlineData("RAG.ENHANCEMENT.CRAG-EVALUATION")]
    public void Validate_WithAKeyThatIsNotARagEnhancement_Fails(string key)
    {
        var result = _validator.TestValidate(new ToggleRagEnhancementCommand(key, Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(c => c.FeatureKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithAnEmptyKey_Fails_WithASingleError(string key)
    {
        var result = _validator.TestValidate(new ToggleRagEnhancementCommand(key, Guid.NewGuid()));

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ToggleRagEnhancementCommand.FeatureKey));
    }
}
