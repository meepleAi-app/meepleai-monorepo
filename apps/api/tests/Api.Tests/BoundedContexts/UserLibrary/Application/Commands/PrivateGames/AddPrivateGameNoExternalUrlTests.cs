using System.Linq;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Compliance regression test: AddPrivateGameCommand must not expose external URL
/// input channels (ImageUrl/ThumbnailUrl). BGG freeze (#2123 / ADR-059) forbids
/// arbitrary user-supplied external image URLs.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AddPrivateGameNoExternalUrlTests
{
    [Fact]
    public void AddPrivateGameCommand_HasNoExternalUrlFields()
    {
        var props = typeof(AddPrivateGameCommand).GetProperties().Select(p => p.Name).ToArray();
        props.Should().NotContain("ImageUrl");
        props.Should().NotContain("ThumbnailUrl");
    }
}
