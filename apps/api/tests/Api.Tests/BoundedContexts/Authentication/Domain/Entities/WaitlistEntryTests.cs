using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Domain unit tests for the <see cref="WaitlistEntry"/> aggregate.
/// Spec §3.5 (2026-04-27-v2-migration-wave-a-2-join.md) — RED phase.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class WaitlistEntryTests
{
    private const string ValidEmail = "alice@example.com";
    private const string ValidGameId = "g-azul";

    [Fact]
    public void Create_WithValidArguments_ReturnsEntry()
    {
        var entry = WaitlistEntry.Create(
            email: ValidEmail,
            name: "Alice",
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);

        entry.Id.Should().NotBe(Guid.Empty);
        entry.Email.Should().Be(ValidEmail);
        entry.Name.Should().Be("Alice");
        entry.GamePreferenceId.Should().Be(ValidGameId);
        entry.GamePreferenceOther.Should().BeNull();
        entry.NewsletterOptIn.Should().BeFalse();
        entry.Position.Should().Be(1);
        entry.ContactedAt.Should().BeNull();
        (entry.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
        (entry.CreatedAt > DateTime.UtcNow.AddMinutes(-1)).Should().BeTrue();
    }

    [Fact]
    public void Create_NormalizesEmailToLowercase()
    {
        var entry = WaitlistEntry.Create(
            email: "Alice@EXAMPLE.COM",
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);

        entry.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public void Create_TrimsWhitespaceFromEmail()
    {
        var entry = WaitlistEntry.Create(
            email: "   alice@example.com  ",
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);

        entry.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public void Create_WithNullEmail_ThrowsArgumentException()
    {
        var act = () => WaitlistEntry.Create(
            email: null!,
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyEmail_ThrowsArgumentException()
    {
        var act = () => WaitlistEntry.Create(
            email: "",
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithWhitespaceEmail_ThrowsArgumentException()
    {
        var act = () => WaitlistEntry.Create(
            email: "   ",
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyGamePreferenceId_ThrowsArgumentException()
    {
        var act = () => WaitlistEntry.Create(
            email: ValidEmail,
            name: null,
            gamePreferenceId: "",
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithGOtherIdAndPopulatedOther_StoresFreeText()
    {
        var entry = WaitlistEntry.Create(
            email: ValidEmail,
            name: null,
            gamePreferenceId: "g-other",
            gamePreferenceOther: "Terraforming Mars",
            newsletterOptIn: false,
            position: 1);

        entry.GamePreferenceId.Should().Be("g-other");
        entry.GamePreferenceOther.Should().Be("Terraforming Mars");
    }

    [Fact]
    public void Create_WithZeroOrNegativePosition_ThrowsArgumentOutOfRange()
    {
        var act = () => WaitlistEntry.Create(
            email: ValidEmail,
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithNewsletterOptInTrue_PreservesOptIn()
    {
        var entry = WaitlistEntry.Create(
            email: ValidEmail,
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: true,
            position: 1);

        entry.NewsletterOptIn.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullName_StoresNullName()
    {
        var entry = WaitlistEntry.Create(
            email: ValidEmail,
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);

        entry.Name.Should().BeNull();
    }

    [Fact]
    public void MarkContacted_FromUncontacted_SetsContactedAt()
    {
        var entry = WaitlistEntry.Create(
            email: ValidEmail,
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);

        entry.MarkContacted();

        entry.ContactedAt.Should().NotBeNull();
        (entry.ContactedAt!.Value <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void MarkContacted_WhenAlreadyContacted_ThrowsInvalidOperation()
    {
        var entry = WaitlistEntry.Create(
            email: ValidEmail,
            name: null,
            gamePreferenceId: ValidGameId,
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 1);
        entry.MarkContacted();

        var act = () => entry.MarkContacted();

        act.Should().Throw<InvalidOperationException>();
    }
}
