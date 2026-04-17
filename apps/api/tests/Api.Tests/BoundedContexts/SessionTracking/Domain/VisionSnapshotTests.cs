using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Unit tests for VisionSnapshot aggregate.
/// Session Vision AI feature.
/// </summary>
public class VisionSnapshotTests
{
    private static readonly Guid ValidSessionId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();

    // ─── Create ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Create_SetsRequiredProperties()
    {
        var snapshot = VisionSnapshot.Create(ValidSessionId, ValidUserId, 3, "Turn 3 state");

        snapshot.Id.Should().NotBeEmpty();
        snapshot.SessionId.Should().Be(ValidSessionId);
        snapshot.UserId.Should().Be(ValidUserId);
        snapshot.TurnNumber.Should().Be(3);
        snapshot.Caption.Should().Be("Turn 3 state");
        snapshot.IsDeleted.Should().BeFalse();
        snapshot.Images.Should().BeEmpty();
        snapshot.ExtractedGameState.Should().BeNull();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Create_WithEmptySessionId_Throws()
    {
        var act = () => VisionSnapshot.Create(Guid.Empty, ValidUserId, 1, null);

        act.Should().Throw<ArgumentException>().WithParameterName("sessionId");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Create_WithEmptyUserId_Throws()
    {
        var act = () => VisionSnapshot.Create(ValidSessionId, Guid.Empty, 1, null);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Create_WithNegativeTurnNumber_Throws()
    {
        var act = () => VisionSnapshot.Create(ValidSessionId, ValidUserId, -1, null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Create_WithTooLongCaption_Throws()
    {
        var longCaption = new string('x', 201);

        var act = () => VisionSnapshot.Create(ValidSessionId, ValidUserId, 1, longCaption);

        act.Should().Throw<ArgumentException>().WithParameterName("caption");
    }

    // ─── AddImage ───────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void AddImage_AddsWithCorrectOrderIndex()
    {
        var snapshot = VisionSnapshot.Create(ValidSessionId, ValidUserId, 1, null);

        snapshot.AddImage("storage/img1.jpg", "image/jpeg", 800, 600);
        snapshot.AddImage("storage/img2.png", "image/png", 1024, 768);

        snapshot.Images.Should().HaveCount(2);
        snapshot.Images[0].StorageKey.Should().Be("storage/img1.jpg");
        snapshot.Images[0].OrderIndex.Should().Be(0);
        snapshot.Images[1].StorageKey.Should().Be("storage/img2.png");
        snapshot.Images[1].OrderIndex.Should().Be(1);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void AddImage_WithEmptyStorageKey_Throws()
    {
        var snapshot = VisionSnapshot.Create(ValidSessionId, ValidUserId, 1, null);

        var act = () => snapshot.AddImage("", "image/jpeg", 800, 600);

        act.Should().Throw<ArgumentException>().WithParameterName("storageKey");
    }

    // ─── UpdateGameState ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void UpdateGameState_SetsJsonAndTimestamp()
    {
        var snapshot = VisionSnapshot.Create(ValidSessionId, ValidUserId, 1, null);
        var json = """{"score": {"player1": 10}}""";

        snapshot.UpdateGameState(json);

        snapshot.ExtractedGameState.Should().Be(json);
        snapshot.UpdatedAt.Should().NotBeNull();
        snapshot.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void UpdateGameState_WithNull_Throws()
    {
        var snapshot = VisionSnapshot.Create(ValidSessionId, ValidUserId, 1, null);

        var act = () => snapshot.UpdateGameState(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ─── SoftDelete ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void SoftDelete_MarksDeleted()
    {
        var snapshot = VisionSnapshot.Create(ValidSessionId, ValidUserId, 1, null);

        snapshot.SoftDelete();

        snapshot.IsDeleted.Should().BeTrue();
        snapshot.DeletedAt.Should().NotBeNull();
        snapshot.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}
