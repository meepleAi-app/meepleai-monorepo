using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SessionBookProgressTests
{
    [Fact]
    public void Create_WithValidInputs_SetsLastLocationAndVisitedAt()
    {
        var progress = SessionBookProgress.Create(Guid.NewGuid(), Guid.NewGuid(), "§289");

        progress.LastLocation.Should().Be("§289");
        progress.LastVisitedAt.Should().NotBe(default);
        progress.HistoryJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void UpdateLocation_AppendsToHistory()
    {
        var progress = SessionBookProgress.Create(Guid.NewGuid(), Guid.NewGuid(), "§147");

        progress.UpdateLocation("§148");
        progress.UpdateLocation("§149");

        progress.LastLocation.Should().Be("§149");
        progress.HistoryJson.Should().Contain("§147");
        progress.HistoryJson.Should().Contain("§148");
        progress.HistoryJson.Should().Contain("§149");
    }

    [Fact]
    public void UpdateLocation_DuplicateLocation_DoesNotDuplicateInHistory()
    {
        var progress = SessionBookProgress.Create(Guid.NewGuid(), Guid.NewGuid(), "§147");

        progress.UpdateLocation("§147");

        var count = System.Text.RegularExpressions.Regex.Matches(progress.HistoryJson, "§147").Count;
        count.Should().Be(1);
    }
}
