using System.Linq;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Compliance regression test: UpdatePrivateGameCommand must not expose an external URL
/// input channel (ImageUrl). BGG freeze (#2123 / ADR-059) forbids arbitrary user-supplied
/// external image URLs on the PUT /private-games/{id} channel. The private-game cover is
/// set only via the cover-from-PDF flow (#2943).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdatePrivateGameNoExternalUrlTests
{
    [Fact]
    public void UpdatePrivateGameCommand_HasNoExternalUrlFields()
    {
        var props = typeof(UpdatePrivateGameCommand).GetProperties().Select(p => p.Name).ToArray();
        props.Should().NotContain("ImageUrl");
        props.Should().NotContain("ThumbnailUrl");
    }
}
