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
    private static readonly Guid GameBookId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Create_WithValidInputs_StartsAsUploaded()
    {
        var before = DateTimeOffset.UtcNow;

        var artifact = GamebookPhotoArtifact.Create(CampaignId, GameBookId, "s3://bucket/photo.jpg");

        artifact.CampaignId.Should().Be(CampaignId);
        artifact.GameBookId.Should().Be(GameBookId);
        artifact.S3Key.Should().Be("s3://bucket/photo.jpg");
        artifact.Status.Should().Be(PhotoArtifactStatus.Uploaded);
        artifact.Segments.Should().BeEmpty();
        artifact.OcrFullText.Should().BeNull();
        artifact.FailureReason.Should().BeNull();

        // ExpiresAt should be approximately now + 24h
        var expectedExpiry = before.AddHours(24);
        artifact.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Create_WithEmptyGameBookId_Throws()
    {
        Action act = () => GamebookPhotoArtifact.Create(CampaignId, Guid.Empty, "s3://bucket/photo.jpg");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("gameBookId");
    }

    [Fact]
    public void RecordSegments_FromUploaded_TransitionsToSegmented()
    {
        var artifact = GamebookPhotoArtifact.Create(CampaignId, GameBookId, "s3://bucket/photo.jpg");
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
        var artifact = GamebookPhotoArtifact.Create(CampaignId, GameBookId, "s3://bucket/photo.jpg");
        artifact.RecordSegments(new[] { GamebookSegment.Create(1, "Text", null) }, "Text");

        Action act = () => artifact.RecordSegments(new[] { GamebookSegment.Create(2, "More text", null) }, "More text");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Uploaded*");
    }

    [Fact]
    public void MarkFailed_FromAnyState_RecordsReason()
    {
        var artifact = GamebookPhotoArtifact.Create(CampaignId, GameBookId, "s3://bucket/photo.jpg");

        artifact.MarkFailed("OCR service timeout");

        artifact.Status.Should().Be(PhotoArtifactStatus.Failed);
        artifact.FailureReason.Should().Be("OCR service timeout");
    }

    [Fact]
    public void RecordSegments_WithLangDetection_SetsDetectedSourceLangAndConfidence()
    {
        var artifact = GamebookPhotoArtifact.Create(Guid.NewGuid(), Guid.NewGuid(), "s3/key");
        var segments = new[] { GamebookSegment.Create(1, "You wake up.", null) };

        artifact.RecordSegments(segments, "You wake up.", detectedSourceLang: "EN", langDetectionConfidence: 0.92);

        artifact.DetectedSourceLang.Should().Be("EN");
        artifact.LangDetectionConfidence.Should().Be(0.92);
        artifact.Status.Should().Be(PhotoArtifactStatus.Segmented);
    }

    [Fact]
    public void RecordSegments_WithoutLangDetection_LeavesLangFieldsNull()
    {
        var artifact = GamebookPhotoArtifact.Create(Guid.NewGuid(), Guid.NewGuid(), "s3/key");
        var segments = new[] { GamebookSegment.Create(1, "Test text.", null) };

        artifact.RecordSegments(segments, "Test text.");

        artifact.DetectedSourceLang.Should().BeNull();
        artifact.LangDetectionConfidence.Should().BeNull();
    }

    [Fact]
    public void RecordSegments_WithNullLangButValidConfidence_PersistsBothFields()
    {
        var artifact = GamebookPhotoArtifact.Create(Guid.NewGuid(), Guid.NewGuid(), "s3/key");
        var segments = new[] { GamebookSegment.Create(1, "Si.", null) };

        artifact.RecordSegments(segments, "Si.", detectedSourceLang: null, langDetectionConfidence: 0.42);

        artifact.DetectedSourceLang.Should().BeNull();
        artifact.LangDetectionConfidence.Should().Be(0.42);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void RecordSegments_WithConfidenceOutOfBounds_Throws(double invalidConfidence)
    {
        var artifact = GamebookPhotoArtifact.Create(Guid.NewGuid(), Guid.NewGuid(), "s3/key");
        var segments = new[] { GamebookSegment.Create(1, "Text.", null) };

        var act = () => artifact.RecordSegments(segments, "Text.", detectedSourceLang: "EN", langDetectionConfidence: invalidConfidence);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
