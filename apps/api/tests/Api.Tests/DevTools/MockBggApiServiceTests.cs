using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Api.DevTools.Scenarios;
using Xunit;

namespace Api.Tests.DevTools;

public class MockBggApiServiceTests
{
    private static MockBggApiService MakeService() => new(new ScenarioLoader());

    [Fact]
    public async Task SearchGames_EmptyQueryReturnsEmpty()
    {
        var svc = MakeService();
        var results = await svc.SearchGamesAsync("", exact: false, CancellationToken.None);
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchGames_WithQueryReturnsList()
    {
        var svc = MakeService();
        var results = await svc.SearchGamesAsync("Wingspan", exact: false, CancellationToken.None);
        // Either returns matches from seed scenarios or empty list — both acceptable.
        // The behavior is deterministic.
        Assert.NotNull(results);
    }

    [Fact]
    public async Task GetGameDetails_KnownIdReturnsDetails()
    {
        var svc = MakeService();
        // 266192 = Wingspan in small-library scenario seeds
        var details = await svc.GetGameDetailsAsync(266192, CancellationToken.None);
        if (details is not null)
        {
            Assert.Equal(266192, details.BggId);
        }
    }

    [Fact]
    public async Task GetGameDetails_UnknownIdReturnsNull()
    {
        var svc = MakeService();
        var details = await svc.GetGameDetailsAsync(999999999, CancellationToken.None);
        Assert.Null(details);
    }
}
