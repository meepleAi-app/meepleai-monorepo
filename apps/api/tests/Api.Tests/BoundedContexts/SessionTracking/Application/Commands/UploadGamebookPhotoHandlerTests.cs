using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class UploadGamebookPhotoHandlerTests
{
    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeCampaignRepo : IGamebookCampaignSessionRepository
    {
        public List<GamebookCampaignSession> Store { get; } = new();

        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid o, Guid? g, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(Store);

        public Task AddAsync(GamebookCampaignSession s, CancellationToken ct = default) { Store.Add(s); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeArtifactRepo : IGamebookPhotoArtifactRepository
    {
        public List<GamebookPhotoArtifact> Store { get; } = new();

        public Task<GamebookPhotoArtifact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookPhotoArtifact>> ListExpiredAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GamebookPhotoArtifact>>(new List<GamebookPhotoArtifact>());

        public Task AddAsync(GamebookPhotoArtifact artifact, CancellationToken cancellationToken = default) { Store.Add(artifact); return Task.CompletedTask; }
        public Task RemoveAsync(GamebookPhotoArtifact artifact, CancellationToken cancellationToken = default) { Store.Remove(artifact); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStorage : IGamebookPhotoStorage
    {
        public List<(Guid CampaignId, Guid PhotoId)> Uploads { get; } = new();

        public Task<string> UploadAsync(Stream photoStream, string contentType, Guid campaignId, Guid photoId, CancellationToken cancellationToken)
        {
            Uploads.Add((campaignId, photoId));
            return Task.FromResult($"gamebook-photos/{campaignId}/{photoId}");
        }

        public Task<Stream> RetrieveAsync(string storageKey, CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (UploadGamebookPhotoCommandHandler handler, FakeCampaignRepo campaignRepo, FakeArtifactRepo artifactRepo, FakeStorage storage)
        BuildSut()
    {
        var campaignRepo = new FakeCampaignRepo();
        var artifactRepo = new FakeArtifactRepo();
        var storage = new FakeStorage();
        var handler = new UploadGamebookPhotoCommandHandler(campaignRepo, artifactRepo, storage);
        return (handler, campaignRepo, artifactRepo, storage);
    }

    private static GamebookCampaignSession BuildCampaign(Guid ownerId) =>
        GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "Test Campaign");

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_UploadsPersistsAndReturnsDto()
    {
        // Arrange
        var (handler, campaignRepo, artifactRepo, storage) = BuildSut();
        var ownerId = Guid.NewGuid();
        var campaign = BuildCampaign(ownerId);
        campaignRepo.Store.Add(campaign);

        var cmd = new UploadGamebookPhotoCommand(
            campaign.Id,
            Guid.NewGuid(),
            ownerId,
            new MemoryStream(new byte[] { 0xFF, 0xD8 }), // fake JPEG bytes
            "image/jpeg");

        // Act
        var dto = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        storage.Uploads.Should().HaveCount(1);
        storage.Uploads[0].CampaignId.Should().Be(campaign.Id);

        artifactRepo.Store.Should().HaveCount(1);
        var artifact = artifactRepo.Store[0];

        dto.Id.Should().Be(artifact.Id);
        dto.CampaignId.Should().Be(campaign.Id);
        dto.Status.Should().Be("Uploaded");
        dto.Segments.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CampaignNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var (handler, _, _, _) = BuildSut();
        var cmd = new UploadGamebookPhotoCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new MemoryStream(),
            "image/jpeg");

        // Act
        Func<Task> act = () => handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CallerNotOwner_ThrowsConflictException()
    {
        // Arrange
        var (handler, campaignRepo, _, _) = BuildSut();
        var ownerId = Guid.NewGuid();
        var differentCallerId = Guid.NewGuid();
        var campaign = BuildCampaign(ownerId);
        campaignRepo.Store.Add(campaign);

        var cmd = new UploadGamebookPhotoCommand(
            campaign.Id,
            Guid.NewGuid(),
            differentCallerId,
            new MemoryStream(),
            "image/jpeg");

        // Act
        Func<Task> act = () => handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }
}
