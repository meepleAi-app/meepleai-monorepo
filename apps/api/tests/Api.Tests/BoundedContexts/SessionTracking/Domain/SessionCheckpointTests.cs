using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class SessionCheckpointTests
{
    private static readonly Guid ValidSessionId = Guid.NewGuid();
    private static readonly Guid ValidCreatedBy = Guid.NewGuid();

    [Fact]
    public void Create_WithValidInputs_ShouldCreateCheckpoint()
    {
        var cp = SessionCheckpoint.Create(ValidSessionId, "Test", ValidCreatedBy, "{}", 5);
        cp.Id.Should().NotBe(Guid.Empty);
        cp.SessionId.Should().Be(ValidSessionId);
        cp.Name.Should().Be("Test");
        cp.DiaryEventCount.Should().Be(5);
        cp.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithZeroEvents_ShouldSucceed()
    {
        var cp = SessionCheckpoint.Create(ValidSessionId, "Test", ValidCreatedBy, "{}", 0);
        cp.DiaryEventCount.Should().Be(0);
    }

    [Fact]
    public void Create_WithMaxName_ShouldSucceed()
    {
        var cp = SessionCheckpoint.Create(ValidSessionId, new string('A', 200), ValidCreatedBy, "{}", 0);
        cp.Name.Length.Should().Be(200);
    }

    [Fact]
    public void Create_EmptySessionId_Throws() =>
        ((Action)(() => SessionCheckpoint.Create(Guid.Empty, "T", ValidCreatedBy, "{}", 0))).Should().Throw<ArgumentException>();

    [Fact]
    public void Create_EmptyName_Throws() =>
        ((Action)(() => SessionCheckpoint.Create(ValidSessionId, "", ValidCreatedBy, "{}", 0))).Should().Throw<ArgumentException>();

    [Fact]
    public void Create_WhitespaceName_Throws() =>
        ((Action)(() => SessionCheckpoint.Create(ValidSessionId, "  ", ValidCreatedBy, "{}", 0))).Should().Throw<ArgumentException>();

    [Fact]
    public void Create_LongName_Throws() =>
        ((Action)(() => SessionCheckpoint.Create(ValidSessionId, new string('A', 201), ValidCreatedBy, "{}", 0))).Should().Throw<ArgumentException>();

    [Fact]
    public void Create_EmptyCreatedBy_Throws() =>
        ((Action)(() => SessionCheckpoint.Create(ValidSessionId, "T", Guid.Empty, "{}", 0))).Should().Throw<ArgumentException>();

    [Fact]
    public void Create_EmptySnapshot_Throws() =>
        ((Action)(() => SessionCheckpoint.Create(ValidSessionId, "T", ValidCreatedBy, "", 0))).Should().Throw<ArgumentException>();

    [Fact]
    public void Create_NegativeEvents_Throws() =>
        ((Action)(() => SessionCheckpoint.Create(ValidSessionId, "T", ValidCreatedBy, "{}", -1))).Should().Throw<ArgumentException>();

    [Fact]
    public void SoftDelete_SetsFlags()
    {
        var cp = SessionCheckpoint.Create(ValidSessionId, "Test", ValidCreatedBy, "{}", 0);
        cp.SoftDelete();
        cp.IsDeleted.Should().BeTrue();
        cp.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var c1 = SessionCheckpoint.Create(ValidSessionId, "A", ValidCreatedBy, "{}", 0);
        var c2 = SessionCheckpoint.Create(ValidSessionId, "B", ValidCreatedBy, "{}", 0);
        c2.Id.Should().NotBe(c1.Id);
    }
}
