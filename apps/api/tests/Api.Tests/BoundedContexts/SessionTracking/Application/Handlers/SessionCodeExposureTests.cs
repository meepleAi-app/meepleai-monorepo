using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// #4883: the session code is the credential that lets a stranger join (POST /game-sessions/code/{code}/join).
/// What is answered without authentication — the share summary, the invite preview — or written into a link
/// made to be pasted into social media must therefore never carry it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SessionCodeExposureTests
{
    [Theory]
    [InlineData(typeof(ShareableSessionResponse))]
    [InlineData(typeof(SessionInviteResponse))]
    public void AnonymousResponses_HaveNoSessionCodeMember(Type anonymousResponse)
    {
        // GET /game-sessions/{id}/share and GET /game-sessions/invite/{token} are AllowAnonymous.
        anonymousResponse.GetProperties().Select(p => p.Name)
            .Should().NotContain(
                name => name.Contains("SessionCode", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("Code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ShareLink_OpenGraphMetadata_DoesNotCarryTheSessionCode()
    {
        // The title ends up in the og:title of the shared page, which any crawler reads.
        var ownerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.GameSpecific);
        var repo = new Mock<ISessionRepository>();
        repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var scores = new Mock<IScoreEntryRepository>();
        scores.Setup(r => r.GetBySessionIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<ScoreEntry>());
        var handler = new GenerateSessionShareLinkQueryHandler(
            repo.Object,
            scores.Object,
            PublicBaseUrlTestSupport.LocalProvider(),
            NullLogger<GenerateSessionShareLinkQueryHandler>.Instance);

        var response = await handler.Handle(
            new GenerateSessionShareLinkQuery(session.Id, ownerId), CancellationToken.None);

        var everythingPublic = $"{response.ShareUrl} {response.OgMetadata.Title} {response.OgMetadata.Description} "
                               + $"{response.OgMetadata.ImageUrl} {response.OgMetadata.Url}";
        everythingPublic.Should().NotContainEquivalentOf(session.SessionCode);
    }
}
