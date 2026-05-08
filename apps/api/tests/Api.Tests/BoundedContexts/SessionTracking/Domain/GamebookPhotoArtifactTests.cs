using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class GamebookPhotoArtifactTests
{
    private static readonly Guid CampaignId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Create_WithValidInputs_StartsAsUploaded()
    {
        var before = DateTimeOffset.UtcNow;

        var artifact = GamebookPhotoArtifact.Create(CampaignId, "s3://bucket/photo.jpg", GamebookPageType.Storybook);

        artifact.CampaignId.Should().Be(CampaignId);
        artifact.S3Key.Should().Be("s3://bucket/photo.jpg");
        artifact.PageType.Should().Be(GamebookPageType.Storybook);
        artifact.Status.Should().Be(PhotoArtifactStatus.Uploaded);
        artifact.Segments.Should().BeEmpty();
        artifact.OcrFullText.Should().BeNull();
        artifact.FailureReason.Should().BeNull();

        // ExpiresAt should be approximately now + 24h
        var expectedExpiry = before.AddHours(24);
        artifact.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void RecordSegments_FromUploaded_TransitionsToSegmented()
    {
        var artifact = GamebookPhotoArtifact.Create(CampaignId, "s3://bucket/photo.jpg", GamebookPageType.Encounter);
        var segments = new[]
        {
            GamebookSegment.Create(1, "You enter the dark cave.", null),
            GamebookSegment.Create(2, "A goblin stands before you.", "{\"x\":10,\"y\":20,\"w\":100,\"h\":50}"),
        };

        artifact.RecordSegments(segments, "You enter the dark cave. A goblin stands before you.");

        artifact.Status.Should().Be(PhotoArtifactStatus.Segmented);
        artifact.Segments.Should().HaveCount(2);
        artifact.OcrFullText.Should().Be("You enter the dark cave. A goblin stands before you.");
    }

    [Fact]
    public void RecordSegments_FromSegmented_Throws()
    {
        var artifact = GamebookPhotoArtifact.Create(CampaignId, "s3://bucket/photo.jpg", GamebookPageType.Storybook);
        artifact.RecordSegments(new[] { GamebookSegment.Create(1, "Text", null) }, "Text");

        Action act = () => artifact.RecordSegments(new[] { GamebookSegment.Create(2, "More text", null) }, "More text");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Uploaded*");
    }

    [Fact]
    public void MarkFailed_FromAnyState_RecordsReason()
    {
        var artifact = GamebookPhotoArtifact.Create(CampaignId, "s3://bucket/photo.jpg", GamebookPageType.Storybook);

        artifact.MarkFailed("OCR service timeout");

        artifact.Status.Should().Be(PhotoArtifactStatus.Failed);
        artifact.FailureReason.Should().Be("OCR service timeout");
    }
}
