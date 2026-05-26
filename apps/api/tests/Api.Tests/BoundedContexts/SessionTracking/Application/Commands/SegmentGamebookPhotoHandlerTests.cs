using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SegmentGamebookPhotoHandlerTests
{
    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeCampaignRepo : IGamebookCampaignSessionRepository
    {
        public List<GamebookCampaignSession> Store { get; } = new();

        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid ownerUserId, Guid? gameId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(Store);

        public Task AddAsync(GamebookCampaignSession session, CancellationToken ct = default) { Store.Add(session); return Task.CompletedTask; }
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
        public Task<string> UploadAsync(Stream photoStream, string contentType, Guid campaignId, Guid photoId, CancellationToken cancellationToken)
            => Task.FromResult($"gamebook-photos/{campaignId}/{photoId}");

        public Task<Stream> RetrieveAsync(string storageKey, CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream(new byte[] { 0x89, 0x50 })); // fake PNG bytes

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeOcrService : IOcrService
    {
        public int CallCount { get; private set; }
        private readonly OcrResult _result;

        public FakeOcrService(OcrResult? result = null)
        {
            _result = result ?? new OcrResult(
                FullText: "The Hive awakens. The Queen stirs.",
                Paragraphs: new[]
                {
                    new OcrParagraph(1, "The Hive awakens.", null),
                    new OcrParagraph(2, "The Queen stirs.", null),
                },
                AverageConfidence: 0.92);
        }

        public Task<OcrResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingOcrService : IOcrService
    {
        public Task<OcrResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken)
            => throw new InvalidOperationException("OCR engine failure");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (SegmentGamebookPhotoCommandHandler handler, FakeCampaignRepo campaignRepo, FakeArtifactRepo artifactRepo, FakeOcrService ocr)
        BuildSut()
    {
        var campaignRepo = new FakeCampaignRepo();
        var artifactRepo = new FakeArtifactRepo();
        var storage = new FakeStorage();
        var ocr = new FakeOcrService();
        var handler = new SegmentGamebookPhotoCommandHandler(campaignRepo, artifactRepo, storage, ocr);
        return (handler, campaignRepo, artifactRepo, ocr);
    }

    private static GamebookCampaignSession BuildCampaign(Guid ownerId) =>
        GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "Test Campaign");

    private static GamebookPhotoArtifact BuildArtifact(Guid campaignId) =>
        GamebookPhotoArtifact.Create(campaignId, Guid.NewGuid(), $"gamebook-photos/{campaignId}/test");

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_ArtifactBecomesSegmentedWithTwoSegments()
    {
        // Arrange
        var (handler, campaignRepo, artifactRepo, _) = BuildSut();
        var ownerId = Guid.NewGuid();
        var campaign = BuildCampaign(ownerId);
        campaignRepo.Store.Add(campaign);

        var artifact = BuildArtifact(campaign.Id);
        artifactRepo.Store.Add(artifact);

        var cmd = new SegmentGamebookPhotoCommand(campaign.Id, artifact.Id, ownerId);

        // Act
        var dto = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        dto.Status.Should().Be("Segmented");
        dto.Segments.Should().HaveCount(2);
        dto.Segments[0].ParagraphNumber.Should().Be(1);
        dto.Segments[0].SourceText.Should().Be("The Hive awakens.");
        dto.Segments[1].ParagraphNumber.Should().Be(2);
        dto.OcrFullText.Should().Contain("Hive");

        // Ensure the in-memory entity was mutated (no separate persistence copy here)
        artifact.Status.Should().Be(PhotoArtifactStatus.Segmented);
        artifact.Segments.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_OcrThrows_ArtifactMarkedFailedWithOcrPrefix()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var campaign = BuildCampaign(ownerId);
        var artifactRepo = new FakeArtifactRepo();
        var campaignRepo = new FakeCampaignRepo();
        campaignRepo.Store.Add(campaign);

        var artifact = BuildArtifact(campaign.Id);
        artifactRepo.Store.Add(artifact);

        var throwingOcr = new ThrowingOcrService();
        var handler = new SegmentGamebookPhotoCommandHandler(
            campaignRepo, artifactRepo, new FakeStorage(), throwingOcr);

        var cmd = new SegmentGamebookPhotoCommand(campaign.Id, artifact.Id, ownerId);

        // Act
        var dto = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        dto.Status.Should().Be("Failed");
        dto.FailureReason.Should().StartWith("OCR:");
        artifact.Status.Should().Be(PhotoArtifactStatus.Failed);
    }

    [Fact]
    public async Task Handle_ArtifactAlreadySegmented_ReturnsCurrentDtoWithoutCallingOcr()
    {
        // Arrange
        var (handler, campaignRepo, artifactRepo, ocr) = BuildSut();
        var ownerId = Guid.NewGuid();
        var campaign = BuildCampaign(ownerId);
        campaignRepo.Store.Add(campaign);

        var artifact = BuildArtifact(campaign.Id);
        // Pre-transition to Segmented
        artifact.RecordSegments(
            new[] { GamebookSegment.Create(1, "Already segmented.", null) },
            "Already segmented.");
        artifactRepo.Store.Add(artifact);

        var cmd = new SegmentGamebookPhotoCommand(campaign.Id, artifact.Id, ownerId);

        // Act
        var dto = await handler.Handle(cmd, CancellationToken.None);

        // Assert — idempotent: OCR was never called
        ocr.CallCount.Should().Be(0);
        dto.Status.Should().Be("Segmented");
        dto.Segments.Should().HaveCount(1);
    }
}
