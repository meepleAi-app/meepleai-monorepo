using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Unit tests for the <see cref="StagingAllowlistEntry"/> domain entity (#845).
/// Covers factory invariants, normalization rules, soft-delete state, and domain event emission.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class StagingAllowlistEntryTests
{
    [Fact]
    public void Create_AssignsIdAndAddedAt()
    {
        var entry = StagingAllowlistEntry.Create("badsworm@gmail.com", addedByUserId: Guid.NewGuid(), note: "bootstrap");

        entry.Id.Should().NotBeEmpty();
        entry.AddedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        entry.IsDeleted.Should().BeFalse();
        entry.DeletedAt.Should().BeNull();
        entry.DeletedByUserId.Should().BeNull();
    }

    [Theory]
    [InlineData("Badsworm@Gmail.com", "badsworm@gmail.com")]
    [InlineData("  user@example.com  ", "user@example.com")]
    [InlineData("USER+tag@EXAMPLE.COM", "user+tag@example.com")]
    public void NormalizeEmail_TrimsAndLowercases(string input, string expected)
    {
        StagingAllowlistEntry.NormalizeEmail(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ThrowsOnMissingEmail(string? email)
    {
        var act = () => StagingAllowlistEntry.Create(email!, addedByUserId: null, note: null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NormalizesEmail()
    {
        var entry = StagingAllowlistEntry.Create("  BADsworm@Gmail.com  ", addedByUserId: null, note: null);

        entry.Email.Should().Be("badsworm@gmail.com");
    }

    [Fact]
    public void Create_EmitsAddedDomainEvent()
    {
        var userId = Guid.NewGuid();

        var entry = StagingAllowlistEntry.Create("user@example.com", userId, note: null);

        entry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<StagingAllowlistEntryAddedEvent>()
            .Which.Email.Should().Be("user@example.com");
        var evt = (StagingAllowlistEntryAddedEvent)entry.DomainEvents.Single();
        evt.AddedByUserId.Should().Be(userId);
        evt.EntryId.Should().Be(entry.Id);
    }

    [Fact]
    public void SoftDelete_MarksAsDeletedAndEmitsRemovedEvent()
    {
        var entry = StagingAllowlistEntry.Create("user@example.com", addedByUserId: null, note: null);
        entry.ClearDomainEvents();
        var removerId = Guid.NewGuid();

        entry.SoftDelete(removerId);

        entry.IsDeleted.Should().BeTrue();
        entry.DeletedAt.Should().NotBeNull();
        entry.DeletedByUserId.Should().Be(removerId);

        entry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<StagingAllowlistEntryRemovedEvent>()
            .Which.RemovedByUserId.Should().Be(removerId);
    }

    [Fact]
    public void SoftDelete_IsIdempotent()
    {
        var entry = StagingAllowlistEntry.Create("user@example.com", addedByUserId: null, note: null);
        entry.ClearDomainEvents();

        entry.SoftDelete(Guid.NewGuid());
        var firstDeletedAt = entry.DeletedAt;
        entry.ClearDomainEvents();
        entry.SoftDelete(Guid.NewGuid());

        entry.DeletedAt.Should().Be(firstDeletedAt, "second SoftDelete must be a no-op");
        entry.DomainEvents.Should().BeEmpty("second SoftDelete must not re-emit the removed event");
    }
}
