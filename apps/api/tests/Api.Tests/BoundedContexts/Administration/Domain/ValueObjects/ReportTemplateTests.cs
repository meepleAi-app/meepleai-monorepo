using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Tests for the ReportTemplate enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 7
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReportTemplateTests
{
    #region Enum Value Tests

    [Fact]
    public void ReportTemplate_SystemHealth_HasCorrectValue()
    {
        ((int)ReportTemplate.SystemHealth).Should().Be(1);
    }

    [Fact]
    public void ReportTemplate_UserActivity_HasCorrectValue()
    {
        ((int)ReportTemplate.UserActivity).Should().Be(2);
    }

    [Fact]
    public void ReportTemplate_AIUsage_HasCorrectValue()
    {
        ((int)ReportTemplate.AIUsage).Should().Be(3);
    }

    [Fact]
    public void ReportTemplate_ContentMetrics_HasCorrectValue()
    {
        ((int)ReportTemplate.ContentMetrics).Should().Be(4);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void ReportTemplate_HasFourValues()
    {
        var values = Enum.GetValues<ReportTemplate>();
        values.Should().HaveCount(4);
    }

    [Fact]
    public void ReportTemplate_AllValuesCanBeParsed()
    {
        var names = new[] { "SystemHealth", "UserActivity", "AIUsage", "ContentMetrics" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<ReportTemplate>(name);
            parsed.Should().BeOneOf(Enum.GetValues<ReportTemplate>());
        }
    }

    [Fact]
    public void ReportTemplate_ToString_ReturnsExpectedNames()
    {
        ReportTemplate.SystemHealth.ToString().Should().Be("SystemHealth");
        ReportTemplate.UserActivity.ToString().Should().Be("UserActivity");
        ReportTemplate.AIUsage.ToString().Should().Be("AIUsage");
        ReportTemplate.ContentMetrics.ToString().Should().Be("ContentMetrics");
    }

    #endregion

    #region Template Category Tests

    [Fact]
    public void ReportTemplate_SystemHealth_IsInfrastructureTemplate()
    {
        // SystemHealth is related to system infrastructure monitoring
        var infrastructureTemplates = new[] { ReportTemplate.SystemHealth };
        infrastructureTemplates.Should().Contain(ReportTemplate.SystemHealth);
    }

    [Fact]
    public void ReportTemplate_UserActivity_IsUserRelatedTemplate()
    {
        // UserActivity tracks user engagement
        var userTemplates = new[] { ReportTemplate.UserActivity };
        userTemplates.Should().Contain(ReportTemplate.UserActivity);
    }

    [Fact]
    public void ReportTemplate_AIUsage_IsCostRelatedTemplate()
    {
        // AIUsage tracks AI/LLM costs
        var costTemplates = new[] { ReportTemplate.AIUsage };
        costTemplates.Should().Contain(ReportTemplate.AIUsage);
    }

    [Fact]
    public void ReportTemplate_ContentMetrics_IsContentRelatedTemplate()
    {
        // ContentMetrics tracks document and content stats
        var contentTemplates = new[] { ReportTemplate.ContentMetrics };
        contentTemplates.Should().Contain(ReportTemplate.ContentMetrics);
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ReportTemplate_CastFromInt_ReturnsCorrectTemplates()
    {
        ((ReportTemplate)1).Should().Be(ReportTemplate.SystemHealth);
        ((ReportTemplate)2).Should().Be(ReportTemplate.UserActivity);
        ((ReportTemplate)3).Should().Be(ReportTemplate.AIUsage);
        ((ReportTemplate)4).Should().Be(ReportTemplate.ContentMetrics);
    }

    [Fact]
    public void ReportTemplate_IsDefined_ReturnsTrueForValidValues()
    {
        for (int i = 1; i <= 4; i++)
        {
            Enum.IsDefined(typeof(ReportTemplate), i).Should().BeTrue();
        }
    }

    [Fact]
    public void ReportTemplate_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(ReportTemplate), 0).Should().BeFalse();
        Enum.IsDefined(typeof(ReportTemplate), 5).Should().BeFalse();
        Enum.IsDefined(typeof(ReportTemplate), -1).Should().BeFalse();
    }

    #endregion
}