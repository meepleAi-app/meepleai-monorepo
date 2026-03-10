using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Scheduling;

/// <summary>
/// Unit tests for LlmRequestLogPseudonymizationOptions.
/// Issue #5511: GDPR pseudonymization configuration.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class LlmRequestLogPseudonymizationOptionsTests
{
    [Fact]
    public void Defaults_SaltIsNotEmpty()
    {
        var options = new LlmRequestLogPseudonymizationOptions();

        options.Salt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        LlmRequestLogPseudonymizationOptions.SectionName.Should().Be("Gdpr:LogPseudonymization");
    }

    [Fact]
    public void Salt_CanBeOverridden()
    {
        var options = new LlmRequestLogPseudonymizationOptions { Salt = "custom-salt" };

        options.Salt.Should().Be("custom-salt");
    }
}
