using Api.BoundedContexts.Authentication.Application.Commands.RevokeShareLink;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// Unit tests for RevokeShareLinkCommandHandler (#4573).
/// <c>ShareLinkRepository.UpdateAsync</c> scrive solo su un link non ancora revocato: se un'altra richiesta l'ha
/// revocato dopo la lettura, l'UPDATE tocca 0 righe e il repository lancia <see cref="DbUpdateConcurrencyException"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4573")]
public class RevokeShareLinkCommandHandlerTests
{
    private readonly Mock<IShareLinkRepository> _repository = new();
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private RevokeShareLinkCommandHandler CreateHandler() => new(_repository.Object, _cache);

    [Fact]
    public async Task Handle_LinkRevokedByAnotherRequestFirst_AnswersTrueAndBlacklistsTheLink()
    {
        var creatorId = Guid.NewGuid();
        var link = new ShareLinkBuilder().WithCreatorId(creatorId).Build();
        // La prima lettura trova il link attivo; la seconda, dopo il conflitto, lo trova revocato dall'altra
        // richiesta. Qui è la stessa istanza, che l'handler ha già revocato in memoria.
        _repository
            .Setup(r => r.GetByIdAsync(link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);
        _repository
            .Setup(r => r.UpdateAsync(link, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("0 rows: the link was revoked after the read"));

        var revoked = await CreateHandler().Handle(new RevokeShareLinkCommand(link.Id, creatorId), CancellationToken.None);

        revoked.Should().BeTrue("the link is revoked, which is what the request asked for");
        (await _cache.GetStringAsync($"revoked_share_link:{link.Id}")).Should().NotBeNull(
            "the blacklist is written anyway: if the other request fell between the database and Redis, the link would stay valid");
    }

    [Fact]
    public async Task Handle_ConflictButTheLinkIsNotRevoked_RethrowsAndDoesNotBlacklist()
    {
        var creatorId = Guid.NewGuid();
        var link = new ShareLinkBuilder().WithCreatorId(creatorId).Build();
        var stillActive = new ShareLinkBuilder().WithCreatorId(creatorId).Build();
        _repository
            .SetupSequence(r => r.GetByIdAsync(link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link)
            .ReturnsAsync(stillActive);
        _repository
            .Setup(r => r.UpdateAsync(link, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("conflict for another reason"));

        var act = () => CreateHandler().Handle(new RevokeShareLinkCommand(link.Id, creatorId), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "a conflict that is not 'already revoked' is a real conflict, not a repeated revocation");
        (await _cache.GetStringAsync($"revoked_share_link:{link.Id}")).Should().BeNull();
    }

    [Fact]
    public async Task Handle_ActiveLink_RevokesAndBlacklists()
    {
        var creatorId = Guid.NewGuid();
        var link = new ShareLinkBuilder().WithCreatorId(creatorId).Build();
        _repository
            .Setup(r => r.GetByIdAsync(link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var revoked = await CreateHandler().Handle(new RevokeShareLinkCommand(link.Id, creatorId), CancellationToken.None);

        revoked.Should().BeTrue();
        link.IsRevoked.Should().BeTrue();
        _repository.Verify(r => r.UpdateAsync(link, It.IsAny<CancellationToken>()), Times.Once());
        (await _cache.GetStringAsync($"revoked_share_link:{link.Id}")).Should().NotBeNull();
    }
}
