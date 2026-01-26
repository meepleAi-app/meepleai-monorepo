using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the ContributionRecordType enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 10
/// </summary>
[Trait("Category", "Unit")]
public sealed class ContributionRecordTypeTests
{
    #region Enum Value Tests

    [Fact]
    public void InitialSubmission_HasCorrectValue()
    {
        ((int)ContributionRecordType.InitialSubmission).Should().Be(0);
    }

    [Fact]
    public void DocumentAddition_HasCorrectValue()
    {
        ((int)ContributionRecordType.DocumentAddition).Should().Be(1);
    }

    [Fact]
    public void MetadataUpdate_HasCorrectValue()
    {
        ((int)ContributionRecordType.MetadataUpdate).Should().Be(2);
    }

    [Fact]
    public void ContentEnhancement_HasCorrectValue()
    {
        ((int)ContributionRecordType.ContentEnhancement).Should().Be(3);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void ContributionRecordType_HasFourValues()
    {
        var values = Enum.GetValues<ContributionRecordType>();
        values.Should().HaveCount(4);
    }

    [Fact]
    public void ContributionRecordType_AllValuesCanBeParsed()
    {
        var names = new[] { "InitialSubmission", "DocumentAddition", "MetadataUpdate", "ContentEnhancement" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<ContributionRecordType>(name);
            parsed.Should().BeOneOf(Enum.GetValues<ContributionRecordType>());
        }
    }

    [Fact]
    public void ContributionRecordType_ToString_ReturnsExpectedNames()
    {
        ContributionRecordType.InitialSubmission.ToString().Should().Be("InitialSubmission");
        ContributionRecordType.DocumentAddition.ToString().Should().Be("DocumentAddition");
        ContributionRecordType.MetadataUpdate.ToString().Should().Be("MetadataUpdate");
        ContributionRecordType.ContentEnhancement.ToString().Should().Be("ContentEnhancement");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void InitialSubmission_IsDefaultType()
    {
        // InitialSubmission = 0 is the type for the first game contribution
        var defaultType = default(ContributionRecordType);
        defaultType.Should().Be(ContributionRecordType.InitialSubmission);
    }

    [Fact]
    public void TypeValues_AreContiguousAndSequential()
    {
        var values = Enum.GetValues<ContributionRecordType>()
            .Cast<int>()
            .OrderBy(x => x)
            .ToArray();

        for (int i = 0; i < values.Length; i++)
        {
            values[i].Should().Be(i, $"Expected type at position {i} to have value {i}");
        }
    }

    #endregion

    #region Conversion Tests

    [Theory]
    [InlineData(0, ContributionRecordType.InitialSubmission)]
    [InlineData(1, ContributionRecordType.DocumentAddition)]
    [InlineData(2, ContributionRecordType.MetadataUpdate)]
    [InlineData(3, ContributionRecordType.ContentEnhancement)]
    public void ContributionRecordType_CastFromInt_ReturnsCorrectValue(int value, ContributionRecordType expected)
    {
        ((ContributionRecordType)value).Should().Be(expected);
    }

    [Fact]
    public void ContributionRecordType_IsDefined_ReturnsTrueForValidValues()
    {
        for (int i = 0; i <= 3; i++)
        {
            Enum.IsDefined(typeof(ContributionRecordType), i).Should().BeTrue();
        }
    }

    [Fact]
    public void ContributionRecordType_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(ContributionRecordType), 4).Should().BeFalse();
        Enum.IsDefined(typeof(ContributionRecordType), -1).Should().BeFalse();
    }

    #endregion
}
