using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Xunit;

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
        Assert.NotEqual(Guid.Empty, cp.Id);
        Assert.Equal(ValidSessionId, cp.SessionId);
        Assert.Equal("Test", cp.Name);
        Assert.Equal(5, cp.DiaryEventCount);
        Assert.False(cp.IsDeleted);
    }

    [Fact]
    public void Create_WithZeroEvents_ShouldSucceed()
    {
        var cp = SessionCheckpoint.Create(ValidSessionId, "Test", ValidCreatedBy, "{}", 0);
        Assert.Equal(0, cp.DiaryEventCount);
    }

    [Fact]
    public void Create_WithMaxName_ShouldSucceed()
    {
        var cp = SessionCheckpoint.Create(ValidSessionId, new string('A', 200), ValidCreatedBy, "{}", 0);
        Assert.Equal(200, cp.Name.Length);
    }

    [Fact]
    public void Create_EmptySessionId_Throws() =>
        Assert.Throws<ArgumentException>(() => SessionCheckpoint.Create(Guid.Empty, "T", ValidCreatedBy, "{}", 0));

    [Fact]
    public void Create_EmptyName_Throws() =>
        Assert.Throws<ArgumentException>(() => SessionCheckpoint.Create(ValidSessionId, "", ValidCreatedBy, "{}", 0));

    [Fact]
    public void Create_WhitespaceName_Throws() =>
        Assert.Throws<ArgumentException>(() => SessionCheckpoint.Create(ValidSessionId, "  ", ValidCreatedBy, "{}", 0));

    [Fact]
    public void Create_LongName_Throws() =>
        Assert.Throws<ArgumentException>(() => SessionCheckpoint.Create(ValidSessionId, new string('A', 201), ValidCreatedBy, "{}", 0));

    [Fact]
    public void Create_EmptyCreatedBy_Throws() =>
        Assert.Throws<ArgumentException>(() => SessionCheckpoint.Create(ValidSessionId, "T", Guid.Empty, "{}", 0));

    [Fact]
    public void Create_EmptySnapshot_Throws() =>
        Assert.Throws<ArgumentException>(() => SessionCheckpoint.Create(ValidSessionId, "T", ValidCreatedBy, "", 0));

    [Fact]
    public void Create_NegativeEvents_Throws() =>
        Assert.Throws<ArgumentException>(() => SessionCheckpoint.Create(ValidSessionId, "T", ValidCreatedBy, "{}", -1));

    [Fact]
    public void SoftDelete_SetsFlags()
    {
        var cp = SessionCheckpoint.Create(ValidSessionId, "Test", ValidCreatedBy, "{}", 0);
        cp.SoftDelete();
        Assert.True(cp.IsDeleted);
        Assert.NotNull(cp.DeletedAt);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var c1 = SessionCheckpoint.Create(ValidSessionId, "A", ValidCreatedBy, "{}", 0);
        var c2 = SessionCheckpoint.Create(ValidSessionId, "B", ValidCreatedBy, "{}", 0);
        Assert.NotEqual(c1.Id, c2.Id);
    }
}
