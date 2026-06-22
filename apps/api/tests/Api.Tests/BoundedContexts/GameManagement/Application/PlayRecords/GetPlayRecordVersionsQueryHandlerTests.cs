using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for <see cref="GetPlayRecordVersionsQueryHandler"/>.
/// Issue #2437-3: version history query (creator-only).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2437")]
public class GetPlayRecordVersionsQueryHandlerTests
{
    private static readonly Guid CreatorId = Guid.NewGuid();
    private static readonly Guid RecordId = Guid.NewGuid();

    private static GetPlayRecordVersionsQueryHandler CreateSut(
        Mock<IPlayRecordVersionRepository> versionRepo,
        Mock<IPlayRecordRepository> recordRepo)
    {
        return new GetPlayRecordVersionsQueryHandler(
            versionRepo.Object,
            new PlayRecordPermissionChecker(recordRepo.Object));
    }

    private static PlayRecordVersion MakeVersion(int number, string? notes = null, string? location = null)
    {
        return new PlayRecordVersion(
            Guid.NewGuid(),
            RecordId,
            number,
            new DateTime(2026, 1, number, 0, 0, 0, DateTimeKind.Utc),
            notes,
            location,
            new DateTime(2026, 6, number, 0, 0, 0, DateTimeKind.Utc),
            CreatorId);
    }

    // -------------------------------------------------------------------------
    // Auth / permission tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_NonCreator_ThrowsForbidden()
    {
        var otherUser = Guid.NewGuid();
        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        var recordRepo = new Mock<IPlayRecordRepository>();
        recordRepo
            .Setup(r => r.CanUserEditAsync(otherUser, RecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => CreateSut(versionRepo, recordRepo).Handle(
            new GetPlayRecordVersionsQuery(RecordId, otherUser),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        versionRepo.Verify(v => v.GetRecentAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Happy-path tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_Creator_ReturnsMappedVersionsDescending()
    {
        var versions = new List<PlayRecordVersion>
        {
            MakeVersion(3, notes: "Third",  location: "Library"),
            MakeVersion(2, notes: "Second", location: null),
            MakeVersion(1, notes: "First",  location: "Home"),
        };

        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        versionRepo
            .Setup(v => v.GetRecentAsync(RecordId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        var recordRepo = new Mock<IPlayRecordRepository>();
        recordRepo
            .Setup(r => r.CanUserEditAsync(CreatorId, RecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut(versionRepo, recordRepo).Handle(
            new GetPlayRecordVersionsQuery(RecordId, CreatorId),
            CancellationToken.None);

        result.Should().HaveCount(3);
        result[0].VersionNumber.Should().Be(3);
        result[0].Notes.Should().Be("Third");
        result[0].Location.Should().Be("Library");
        result[0].CreatedByUserId.Should().Be(CreatorId);
        result[1].VersionNumber.Should().Be(2);
        result[2].VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Creator_EmptyHistory_ReturnsEmptyList()
    {
        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        versionRepo
            .Setup(v => v.GetRecentAsync(RecordId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayRecordVersion>());

        var recordRepo = new Mock<IPlayRecordRepository>();
        recordRepo
            .Setup(r => r.CanUserEditAsync(CreatorId, RecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut(versionRepo, recordRepo).Handle(
            new GetPlayRecordVersionsQuery(RecordId, CreatorId),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Creator_RequestsLimitOf5()
    {
        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        versionRepo
            .Setup(v => v.GetRecentAsync(RecordId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayRecordVersion>());

        var recordRepo = new Mock<IPlayRecordRepository>();
        recordRepo
            .Setup(r => r.CanUserEditAsync(CreatorId, RecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateSut(versionRepo, recordRepo).Handle(
            new GetPlayRecordVersionsQuery(RecordId, CreatorId),
            CancellationToken.None);

        versionRepo.Verify(v => v.GetRecentAsync(RecordId, 5, It.IsAny<CancellationToken>()), Times.Once);
    }
}
