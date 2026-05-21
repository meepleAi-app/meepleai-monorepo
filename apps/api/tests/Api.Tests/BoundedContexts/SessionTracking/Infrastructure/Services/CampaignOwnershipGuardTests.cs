using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class CampaignOwnershipGuardTests
{
    private readonly Mock<IGamebookCampaignSessionRepository> _campaignsMock = new(MockBehavior.Strict);
    private readonly DefaultHttpContext _httpContext = new();
    private readonly Mock<IHttpContextAccessor> _accessorMock = new();

    public CampaignOwnershipGuardTests()
    {
        _accessorMock.Setup(a => a.HttpContext).Returns(_httpContext);
    }

    private CampaignOwnershipGuard CreateGuard() =>
        new(_campaignsMock.Object, _accessorMock.Object);

    private static GamebookCampaignSession CreateCampaign(Guid ownerUserId) =>
        GamebookCampaignSession.Create(
            gameRef: GameRef.Shared(Guid.NewGuid()),
            ownerUserId: ownerUserId,
            title: "Test Campaign");

    [Fact]
    public async Task AssertOwnedByAsync_OwnerMatches_DoesNotThrow()
    {
        var userId = Guid.NewGuid();
        var campaign = CreateCampaign(userId);
        _campaignsMock.Setup(c => c.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        await CreateGuard().AssertOwnedByAsync(campaign.Id, userId, CancellationToken.None);

        _campaignsMock.VerifyAll();
    }

    [Fact]
    public async Task AssertOwnedByAsync_OwnerMismatch_ThrowsForbiddenException()
    {
        var actualOwner = Guid.NewGuid();
        var caller = Guid.NewGuid();
        var campaign = CreateCampaign(actualOwner);
        _campaignsMock.Setup(c => c.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateGuard().AssertOwnedByAsync(campaign.Id, caller, CancellationToken.None));
    }

    [Fact]
    public async Task AssertOwnedByAsync_CampaignMissing_ThrowsNotFoundException()
    {
        var campaignId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _campaignsMock.Setup(c => c.GetByIdAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GamebookCampaignSession?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateGuard().AssertOwnedByAsync(campaignId, userId, CancellationToken.None));
    }

    [Fact]
    public async Task AssertOwnedByAsync_SecondCallSameRequest_HitsCache()
    {
        var userId = Guid.NewGuid();
        var campaign = CreateCampaign(userId);
        _campaignsMock.Setup(c => c.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        var guard = CreateGuard();
        await guard.AssertOwnedByAsync(campaign.Id, userId, CancellationToken.None);
        await guard.AssertOwnedByAsync(campaign.Id, userId, CancellationToken.None);

        _campaignsMock.Verify(c => c.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>()),
            Times.Once); // only 1 DB call despite 2 assertions
    }

    [Fact]
    public async Task AssertOwnedByAsync_DifferentUserSameRequest_RecomputesAndCaches()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var campaign = CreateCampaign(user1);
        _campaignsMock.Setup(c => c.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaign);

        var guard = CreateGuard();
        await guard.AssertOwnedByAsync(campaign.Id, user1, CancellationToken.None);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            guard.AssertOwnedByAsync(campaign.Id, user2, CancellationToken.None));

        // 2 DB calls because cache key is (campaignId, userId) tuple and only positive
        // outcomes get cached — the user2 mismatch is NOT cached.
        _campaignsMock.Verify(c => c.GetByIdAsync(campaign.Id, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
