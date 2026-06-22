using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the SharedGameTranslation aggregate root.
/// Issue #2339 — sub-PR 1/3 Wave 1 (Task 3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameTranslationTests
{
    private static readonly Guid SampleGameId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_HappyPath_AssignsAllFields()
    {
        var locale = Locale.Create("it");
        var actor = Guid.NewGuid();

        var t = SharedGameTranslation.Create(
            sharedGameId: SampleGameId,
            locale: locale,
            title: "I Coloni di Catan",
            description: "Costruisci e scambia sull'isola di Catan",
            source: TranslationSource.Manual,
            createdBy: actor,
            now: Now);

        t.Id.Should().NotBe(Guid.Empty);
        t.SharedGameId.Should().Be(SampleGameId);
        t.Locale.Should().Be(locale);
        t.Title.Should().Be("I Coloni di Catan");
        t.Description.Should().Be("Costruisci e scambia sull'isola di Catan");
        t.Source.Should().Be(TranslationSource.Manual);
        t.CreatedAt.Should().Be(Now);
        t.CreatedBy.Should().Be(actor);
        t.IsDeleted.Should().BeFalse();
        t.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_TitleTrimmed()
    {
        var t = SharedGameTranslation.Create(
            SampleGameId, Locale.Create("it"),
            "  Catan  ", null, TranslationSource.Manual, null, Now);
        t.Title.Should().Be("Catan");
    }

    [Fact]
    public void Create_EmptyGameId_Throws()
    {
        var act = () => SharedGameTranslation.Create(
            Guid.Empty, Locale.Create("it"),
            "title", null, TranslationSource.Manual, null, Now);
        act.Should().Throw<ArgumentException>().WithMessage("*SharedGameId*");
    }

    [Fact]
    public void Create_CanonicalEnLocale_Throws()
    {
        var act = () => SharedGameTranslation.Create(
            SampleGameId, Locale.CanonicalEn,
            "Catan", null, TranslationSource.Manual, null, Now);
        act.Should().Throw<InvalidLocaleException>()
            .WithMessage("*Canonical EN*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyTitle_Throws(string? title)
    {
        var act = () => SharedGameTranslation.Create(
            SampleGameId, Locale.Create("it"),
            title!, null, TranslationSource.Manual, null, Now);
        act.Should().Throw<ArgumentException>().WithMessage("*Title*");
    }

    [Fact]
    public void Create_TitleTooLong_Throws()
    {
        var act = () => SharedGameTranslation.Create(
            SampleGameId, Locale.Create("it"),
            new string('x', 501), null, TranslationSource.Manual, null, Now);
        act.Should().Throw<ArgumentException>().WithMessage("*500*");
    }

    [Fact]
    public void UpdateTitle_Active_MutatesAndStampsUpdated()
    {
        var t = NewActiveTranslation();
        var actor = Guid.NewGuid();
        var later = Now.AddHours(1);

        t.UpdateTitle("Nuovo titolo", actor, later);

        t.Title.Should().Be("Nuovo titolo");
        t.UpdatedAt.Should().Be(later);
        t.UpdatedBy.Should().Be(actor);
    }

    [Fact]
    public void UpdateTitle_SoftDeleted_Throws()
    {
        var t = NewActiveTranslation();
        t.SoftDelete(null, Now);
        var act = () => t.UpdateTitle("any", null, Now.AddHours(1));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SoftDelete_Idempotent()
    {
        var t = NewActiveTranslation();
        t.SoftDelete(null, Now);
        var firstDeletedAt = t.DeletedAt;
        t.SoftDelete(null, Now.AddHours(1)); // second call no-op
        t.DeletedAt.Should().Be(firstDeletedAt);
    }

    [Fact]
    public void Restore_ResurrectsAndStampsUpdated()
    {
        var t = NewActiveTranslation();
        t.SoftDelete(null, Now);
        var actor = Guid.NewGuid();
        var later = Now.AddDays(1);

        t.Restore(actor, later);

        t.IsDeleted.Should().BeFalse();
        t.DeletedAt.Should().BeNull();
        t.UpdatedAt.Should().Be(later);
        t.UpdatedBy.Should().Be(actor);
    }

    [Fact]
    public void SetXmin_AssignsXmin()
    {
        var t = NewActiveTranslation();
        t.SetXmin(42u);
        t.Xmin.Should().Be(42u);
    }

    private static SharedGameTranslation NewActiveTranslation() =>
        SharedGameTranslation.Create(
            SampleGameId, Locale.Create("it"),
            "Catan", null, TranslationSource.Manual, null, Now);
}
