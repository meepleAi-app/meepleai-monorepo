using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

public class PlayerResourcesTests
{
    [Fact]
    public void Create_WithValidData_SetsProperties()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        var resources = new Dictionary<string, int> { ["wood"] = 3, ["ore"] = 2 };

        // Act
        var pr = PlayerResources.Create(participantId, resources);

        // Assert
        Assert.Equal(participantId, pr.ParticipantId);
        Assert.Equal(3, pr.Resources["wood"]);
        Assert.Equal(2, pr.Resources["ore"]);
    }

    [Fact]
    public void Create_WithEmptyParticipantId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PlayerResources.Create(Guid.Empty, new Dictionary<string, int>()));
    }

    [Fact]
    public void Create_WithNullResources_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PlayerResources.Create(Guid.NewGuid(), null!));
    }

    [Fact]
    public void Create_DefensivelyCopiesDictionary()
    {
        // Arrange
        var original = new Dictionary<string, int> { ["wood"] = 3 };
        var pr = PlayerResources.Create(Guid.NewGuid(), original);

        // Act — mutate the original dictionary
        original["wood"] = 999;

        // Assert — value object should be unaffected
        Assert.Equal(3, pr.Resources["wood"]);
    }

    [Fact]
    public void WithResource_ReturnsNewInstanceWithUpdatedValue()
    {
        // Arrange
        var pr = PlayerResources.Create(Guid.NewGuid(), new Dictionary<string, int> { ["wood"] = 3 });

        // Act
        var updated = pr.WithResource("wood", 5).WithResource("ore", 2);

        // Assert
        Assert.Equal(5, updated.Resources["wood"]);
        Assert.Equal(2, updated.Resources["ore"]);
        Assert.Equal(3, pr.Resources["wood"]); // original unchanged
        Assert.False(pr.Resources.ContainsKey("ore")); // original lacks new key
    }

    [Fact]
    public void WithResource_EmptyKey_Throws()
    {
        var pr = PlayerResources.Create(Guid.NewGuid(), new Dictionary<string, int>());
        Assert.Throws<ArgumentException>(() => pr.WithResource("", 1));
        Assert.Throws<ArgumentException>(() => pr.WithResource("  ", 1));
    }

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var pr = PlayerResources.Create(Guid.NewGuid(),
            new Dictionary<string, int> { ["wood"] = 3, ["ore"] = 1 });

        var json = pr.ToJson();

        Assert.Contains("\"wood\":3", json);
        Assert.Contains("\"ore\":1", json);
    }

    [Fact]
    public void FromJson_RoundTrips()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        var pr = PlayerResources.Create(participantId,
            new Dictionary<string, int> { ["wood"] = 3, ["ore"] = 1 });

        // Act
        var json = pr.ToJson();
        var restored = PlayerResources.FromJson(participantId, json);

        // Assert
        Assert.Equal(participantId, restored.ParticipantId);
        Assert.Equal(pr.Resources["wood"], restored.Resources["wood"]);
        Assert.Equal(pr.Resources["ore"], restored.Resources["ore"]);
    }

    [Fact]
    public void FromJson_WithEmptyJson_CreatesEmptyResources()
    {
        var pr = PlayerResources.FromJson(Guid.NewGuid(), "{}");
        Assert.Empty(pr.Resources);
    }

    [Fact]
    public void Create_WithEmptyResources_IsValid()
    {
        var pr = PlayerResources.Create(Guid.NewGuid(), new Dictionary<string, int>());
        Assert.Empty(pr.Resources);
    }
}
