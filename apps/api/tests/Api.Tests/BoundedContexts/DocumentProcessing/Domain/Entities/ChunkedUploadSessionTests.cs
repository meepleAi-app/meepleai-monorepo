using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// Unit tests for ChunkedUploadSession aggregate root.
/// Issue #2640: Comprehensive test suite for DocumentProcessing bounded context
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ChunkedUploadSessionTests
{
    private static Guid GameId => new("12345678-1234-1234-1234-123456789012");
    private static Guid UserId => new("87654321-4321-4321-4321-210987654321");
    private const string FileName = "test-document.pdf";
    private const string TempDir = "/tmp/upload_123";

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidArguments_CreatesSession()
    {
        // Arrange
        var id = Guid.NewGuid();
        var totalFileSize = 15L * 1024 * 1024; // 15 MB

        // Act
        var session = new ChunkedUploadSession(
            id,
            GameId,
            UserId,
            FileName,
            totalFileSize,
            TempDir);

        // Assert
        session.Id.Should().Be(id);
        session.GameId.Should().Be(GameId);
        session.UserId.Should().Be(UserId);
        session.FileName.Should().Be(FileName);
        session.TotalFileSize.Should().Be(totalFileSize);
        session.TotalChunks.Should().Be(2); // 15 MB / 10 MB = 2 chunks
        session.ReceivedChunks.Should().Be(0);
        session.TempDirectory.Should().Be(TempDir);
        session.Status.Should().Be("pending");
        session.ReceivedChunkIndices.Should().Be("[]");
        session.IsComplete.Should().BeFalse();
        session.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void Constructor_EmptyFileName_ThrowsArgumentException()
    {
        // Act
        Action act = () => new ChunkedUploadSession(
            Guid.NewGuid(),
            GameId,
            UserId,
            "",
            1024,
            TempDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*File name cannot be empty*");
    }

    [Fact]
    public void Constructor_WhitespaceFileName_ThrowsArgumentException()
    {
        // Act
        Action act = () => new ChunkedUploadSession(
            Guid.NewGuid(),
            GameId,
            UserId,
            "   ",
            1024,
            TempDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*File name cannot be empty*");
    }

    [Fact]
    public void Constructor_ZeroFileSize_ThrowsArgumentException()
    {
        // Act
        Action act = () => new ChunkedUploadSession(
            Guid.NewGuid(),
            GameId,
            UserId,
            FileName,
            0,
            TempDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Total file size must be positive*");
    }

    [Fact]
    public void Constructor_NegativeFileSize_ThrowsArgumentException()
    {
        // Act
        Action act = () => new ChunkedUploadSession(
            Guid.NewGuid(),
            GameId,
            UserId,
            FileName,
            -100,
            TempDir);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Total file size must be positive*");
    }

    [Fact]
    public void Constructor_EmptyTempDirectory_ThrowsArgumentException()
    {
        // Act
        Action act = () => new ChunkedUploadSession(
            Guid.NewGuid(),
            GameId,
            UserId,
            FileName,
            1024,
            "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Temp directory cannot be empty*");
    }

    #endregion

    #region TotalChunks Calculation Tests

    [Theory]
    [InlineData(5_000_000, 1)]     // 5 MB = 1 chunk
    [InlineData(10_485_760, 1)]    // Exactly 10 MB = 1 chunk
    [InlineData(10_485_761, 2)]    // 10 MB + 1 byte = 2 chunks
    [InlineData(20_000_000, 2)]    // 20 MB = 2 chunks
    [InlineData(25_000_000, 3)]    // 25 MB = 3 chunks
    [InlineData(100_000_000, 10)]  // 100 MB = 10 chunks
    public void TotalChunks_CalculatesCorrectly(long fileSize, int expectedChunks)
    {
        // Act
        var session = CreateSession(fileSize);

        // Assert
        session.TotalChunks.Should().Be(expectedChunks);
    }

    #endregion

    #region MarkChunkReceived Tests

    [Fact]
    public void MarkChunkReceived_ValidChunk_MarksAsReceived()
    {
        // Arrange
        var session = CreateSession(25_000_000); // 3 chunks

        // Act
        session.MarkChunkReceived(0);

        // Assert
        session.ReceivedChunks.Should().Be(1);
        session.HasChunk(0).Should().BeTrue();
        session.HasChunk(1).Should().BeFalse();
        session.Status.Should().Be("uploading");
        session.ReceivedChunkIndices.Should().Be("[0]");
    }

    [Fact]
    public void MarkChunkReceived_MultipleChunks_TracksAll()
    {
        // Arrange
        var session = CreateSession(35_000_000); // 4 chunks

        // Act
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(2);
        session.MarkChunkReceived(1);

        // Assert
        session.ReceivedChunks.Should().Be(3);
        session.HasChunk(0).Should().BeTrue();
        session.HasChunk(1).Should().BeTrue();
        session.HasChunk(2).Should().BeTrue();
        session.HasChunk(3).Should().BeFalse();
        session.ReceivedChunkIndices.Should().Be("[0,1,2]");
    }

    [Fact]
    public void MarkChunkReceived_DuplicateChunk_IsIdempotent()
    {
        // Arrange
        var session = CreateSession(25_000_000);

        // Act
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(0);

        // Assert
        session.ReceivedChunks.Should().Be(1);
    }

    [Fact]
    public void MarkChunkReceived_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var session = CreateSession(25_000_000);

        // Act
        Action act = () => session.MarkChunkReceived(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkChunkReceived_IndexTooHigh_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var session = CreateSession(25_000_000); // 3 chunks

        // Act
        Action act = () => session.MarkChunkReceived(3);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkChunkReceived_AfterCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSession(15_000_000); // 2 chunks
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(1);
        session.MarkAsAssembling();
        session.MarkAsCompleted();

        // Act
        Action act = () => session.MarkChunkReceived(0);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot add chunks to session*");
    }

    [Fact]
    public void MarkChunkReceived_AfterFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSession(15_000_000);
        session.MarkAsFailed("Test error");

        // Act
        Action act = () => session.MarkChunkReceived(0);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot add chunks to session*");
    }

    #endregion

    #region GetMissingChunks Tests

    [Fact]
    public void GetMissingChunks_NoChunksReceived_ReturnsAllIndices()
    {
        // Arrange
        var session = CreateSession(35_000_000); // 4 chunks

        // Act
        var missing = session.GetMissingChunks();

        // Assert
        missing.Should().BeEquivalentTo(new[] { 0, 1, 2, 3 });
    }

    [Fact]
    public void GetMissingChunks_SomeChunksReceived_ReturnsMissing()
    {
        // Arrange
        var session = CreateSession(35_000_000); // 4 chunks
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(2);

        // Act
        var missing = session.GetMissingChunks();

        // Assert
        missing.Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void GetMissingChunks_AllChunksReceived_ReturnsEmpty()
    {
        // Arrange
        var session = CreateSession(15_000_000); // 2 chunks
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(1);

        // Act
        var missing = session.GetMissingChunks();

        // Assert
        missing.Should().BeEmpty();
    }

    #endregion

    #region IsComplete Tests

    [Fact]
    public void IsComplete_AllChunksReceived_ReturnsTrue()
    {
        // Arrange
        var session = CreateSession(15_000_000); // 2 chunks
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(1);

        // Assert
        session.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void IsComplete_NotAllChunksReceived_ReturnsFalse()
    {
        // Arrange
        var session = CreateSession(15_000_000); // 2 chunks
        session.MarkChunkReceived(0);

        // Assert
        session.IsComplete.Should().BeFalse();
    }

    #endregion

    #region Status Transition Tests

    [Fact]
    public void MarkAsAssembling_AllChunksReceived_TransitionsToAssembling()
    {
        // Arrange
        var session = CreateSession(15_000_000); // 2 chunks
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(1);

        // Act
        session.MarkAsAssembling();

        // Assert
        session.Status.Should().Be("assembling");
    }

    [Fact]
    public void MarkAsAssembling_NotAllChunksReceived_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSession(15_000_000); // 2 chunks
        session.MarkChunkReceived(0);

        // Act
        Action act = () => session.MarkAsAssembling();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot assemble until all chunks are received*");
    }

    [Fact]
    public void MarkAsCompleted_SetsStatusAndCompletedAt()
    {
        // Arrange
        var session = CreateSession(15_000_000);
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(1);
        session.MarkAsAssembling();

        // Act
        session.MarkAsCompleted();

        // Assert
        session.Status.Should().Be("completed");
        session.CompletedAt.Should().NotBeNull();
        session.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkAsFailed_SetsStatusAndErrorMessage()
    {
        // Arrange
        var session = CreateSession(15_000_000);
        const string errorMessage = "File assembly failed: checksum mismatch";

        // Act
        session.MarkAsFailed(errorMessage);

        // Assert
        session.Status.Should().Be("failed");
        session.ErrorMessage.Should().Be(errorMessage);
        session.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsExpired_SetsStatusAndCompletedAt()
    {
        // Arrange
        var session = CreateSession(15_000_000);

        // Act
        session.MarkAsExpired();

        // Assert
        session.Status.Should().Be("expired");
        session.CompletedAt.Should().NotBeNull();
    }

    #endregion

    #region GetChunkFilePath Tests

    [Theory]
    [InlineData(0, "chunk_0000.bin")]
    [InlineData(1, "chunk_0001.bin")]
    [InlineData(99, "chunk_0099.bin")]
    [InlineData(9999, "chunk_9999.bin")]
    public void GetChunkFilePath_ReturnsFormattedPath(int chunkIndex, string expectedFileName)
    {
        // Arrange
        var session = CreateSession(1L * 1024 * 1024 * 1024); // Large file to allow high indices

        // Act
        var path = session.GetChunkFilePath(chunkIndex);

        // Assert - Check the path ends with expected filename (cross-platform)
        path.Should().EndWith(expectedFileName);
        path.Should().StartWith(TempDir);
    }

    [Fact]
    public void GetChunkFilePath_CombinesTempDirectoryAndChunkFile()
    {
        // Arrange
        var session = CreateSession(15_000_000);

        // Act
        var path = session.GetChunkFilePath(0);

        // Assert - Use Path.Combine behavior
        var expectedPath = Path.Combine(TempDir, "chunk_0000.bin");
        path.Should().Be(expectedPath);
    }

    #endregion

    #region ProgressPercentage Tests

    [Fact]
    public void ProgressPercentage_NoChunksReceived_ReturnsZero()
    {
        // Arrange
        var session = CreateSession(25_000_000); // 3 chunks

        // Assert
        session.ProgressPercentage.Should().Be(0);
    }

    [Fact]
    public void ProgressPercentage_HalfChunksReceived_ReturnsFifty()
    {
        // Arrange
        var session = CreateSession(20_000_000); // 2 chunks
        session.MarkChunkReceived(0);

        // Assert
        session.ProgressPercentage.Should().Be(50);
    }

    [Fact]
    public void ProgressPercentage_AllChunksReceived_ReturnsHundred()
    {
        // Arrange
        var session = CreateSession(15_000_000); // 2 chunks
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(1);

        // Assert
        session.ProgressPercentage.Should().Be(100);
    }

    [Fact]
    public void ProgressPercentage_ThreeOfFourChunks_ReturnsSeventyFive()
    {
        // Arrange
        var session = CreateSession(35_000_000); // 4 chunks
        session.MarkChunkReceived(0);
        session.MarkChunkReceived(1);
        session.MarkChunkReceived(2);

        // Assert
        session.ProgressPercentage.Should().Be(75);
    }

    #endregion

    #region Expiration Tests

    [Fact]
    public void ExpiresAt_Is24HoursAfterCreation()
    {
        // Arrange & Act
        var session = CreateSession(15_000_000);

        // Assert
        var expectedExpiry = session.CreatedAt.AddHours(ChunkedUploadSession.SessionExpirationHours);
        session.ExpiresAt.Should().Be(expectedExpiry);
    }

    [Fact]
    public void MaxChunkSizeBytes_Is10MB()
    {
        // Assert
        ChunkedUploadSession.MaxChunkSizeBytes.Should().Be(10 * 1024 * 1024);
    }

    [Fact]
    public void SessionExpirationHours_Is24()
    {
        // Assert
        ChunkedUploadSession.SessionExpirationHours.Should().Be(24);
    }

    #endregion

    #region Helper Methods

    private static ChunkedUploadSession CreateSession(long totalFileSize)
    {
        return new ChunkedUploadSession(
            Guid.NewGuid(),
            GameId,
            UserId,
            FileName,
            totalFileSize,
            TempDir);
    }

    #endregion
}
