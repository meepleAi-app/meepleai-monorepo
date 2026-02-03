using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the SharedGameDocumentType enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 12
/// </summary>
[Trait("Category", "Unit")]
public sealed class SharedGameDocumentTypeTests
{
    #region Enum Value Tests

    [Fact]
    public void Rulebook_HasCorrectValue()
    {
        ((int)SharedGameDocumentType.Rulebook).Should().Be(0);
    }

    [Fact]
    public void Errata_HasCorrectValue()
    {
        ((int)SharedGameDocumentType.Errata).Should().Be(1);
    }

    [Fact]
    public void Homerule_HasCorrectValue()
    {
        ((int)SharedGameDocumentType.Homerule).Should().Be(2);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void SharedGameDocumentType_HasThreeValues()
    {
        var values = Enum.GetValues<SharedGameDocumentType>();
        values.Should().HaveCount(3);
    }

    [Fact]
    public void SharedGameDocumentType_AllValuesCanBeParsed()
    {
        var names = new[] { "Rulebook", "Errata", "Homerule" };

        foreach (var name in names)
        {
            var parsed = Enum.Parse<SharedGameDocumentType>(name);
            parsed.Should().BeOneOf(Enum.GetValues<SharedGameDocumentType>());
        }
    }

    [Fact]
    public void SharedGameDocumentType_ToString_ReturnsExpectedNames()
    {
        SharedGameDocumentType.Rulebook.ToString().Should().Be("Rulebook");
        SharedGameDocumentType.Errata.ToString().Should().Be("Errata");
        SharedGameDocumentType.Homerule.ToString().Should().Be("Homerule");
    }

    #endregion

    #region Semantic Tests

    [Fact]
    public void Rulebook_IsDefaultType()
    {
        // Rulebook = 0 is the most common document type
        var defaultType = default(SharedGameDocumentType);
        defaultType.Should().Be(SharedGameDocumentType.Rulebook);
    }

    [Fact]
    public void TypeValues_AreContiguousAndSequential()
    {
        var values = Enum.GetValues<SharedGameDocumentType>()
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
    [InlineData(0, SharedGameDocumentType.Rulebook)]
    [InlineData(1, SharedGameDocumentType.Errata)]
    [InlineData(2, SharedGameDocumentType.Homerule)]
    public void SharedGameDocumentType_CastFromInt_ReturnsCorrectValue(int value, SharedGameDocumentType expected)
    {
        ((SharedGameDocumentType)value).Should().Be(expected);
    }

    [Fact]
    public void SharedGameDocumentType_IsDefined_ReturnsTrueForValidValues()
    {
        for (int i = 0; i <= 2; i++)
        {
            Enum.IsDefined(typeof(SharedGameDocumentType), i).Should().BeTrue();
        }
    }

    [Fact]
    public void SharedGameDocumentType_IsDefined_ReturnsFalseForInvalidValues()
    {
        Enum.IsDefined(typeof(SharedGameDocumentType), 3).Should().BeFalse();
        Enum.IsDefined(typeof(SharedGameDocumentType), -1).Should().BeFalse();
    }

    #endregion

    #region Semantic Category Tests

    [Fact]
    public void Rulebook_IsOfficialDocumentType()
    {
        // Rulebook represents official rules from publisher
        var rulebook = SharedGameDocumentType.Rulebook;
        rulebook.Should().Be(SharedGameDocumentType.Rulebook);
        ((int)rulebook).Should().Be(0);
    }

    [Fact]
    public void Errata_IsCorrectionsType()
    {
        // Errata represents corrections and clarifications
        var errata = SharedGameDocumentType.Errata;
        errata.Should().Be(SharedGameDocumentType.Errata);
        ((int)errata).Should().Be(1);
    }

    [Fact]
    public void Homerule_IsUserCreatedType()
    {
        // Homerule represents user-created variants
        var homerule = SharedGameDocumentType.Homerule;
        homerule.Should().Be(SharedGameDocumentType.Homerule);
        ((int)homerule).Should().Be(2);
    }

    [Fact]
    public void OfficialTypes_HaveLowerValues()
    {
        // Official types (Rulebook, Errata) have lower values than user-created (Homerule)
        ((int)SharedGameDocumentType.Rulebook).Should().BeLessThan((int)SharedGameDocumentType.Homerule);
        ((int)SharedGameDocumentType.Errata).Should().BeLessThan((int)SharedGameDocumentType.Homerule);
    }

    #endregion
}
