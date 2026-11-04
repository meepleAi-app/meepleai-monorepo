using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for <see cref="RestorePlayRecordVersionCommandHandler"/>.
/// #4843: a version stores the state BEFORE an update, with <c>null</c> for a field that was never set.
/// Restoring it must put the record back as it was, so a <c>null</c> in the version has to clear the field —
/// <c>PlayRecord.UpdateDetails</c> reads <c>null</c> as «leave it alone».
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4843")]
public class RestorePlayRecordVersionCommandHandlerTests
{
    private static readonly Guid CreatorId = Guid.NewGuid();
    private static readonly DateTime OriginalDate = DateTime.UtcNow.AddDays(-3);

    /// <summary>A record as the user left it AFTER the edits: it carries notes and a location.</summary>
    private static PlayRecord EditedRecord()
    {
        var record = PlayRecord.CreateFreeForm(
            Guid.NewGuid(),
            "Catan",
            CreatorId,
            OriginalDate,
            PlayRecordVisibility.Private,
            SessionScoringConfig.CreateDefault());
        record.UpdateDetails(notes: "Written after the version", location: "Bar");
        return record;
    }

    private static PlayRecordVersion VersionOf(PlayRecord record, string? notes, string? location, DateTime? sessionDate = null) =>
        new(
            Guid.NewGuid(),
            record.Id,
            versionNumber: 1,
            sessionDate ?? OriginalDate,
            notes,
            location,
            DateTime.UtcNow.AddDays(-2),
            CreatorId);

    private static async Task RestoreAsync(PlayRecord record, PlayRecordVersion version)
    {
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(CreatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var versions = new Mock<IPlayRecordVersionRepository>();
        versions.Setup(r => r.GetByVersionNumberAsync(record.Id, version.VersionNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);
        versions.Setup(r => r.GetNextVersionNumberAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var sut = new RestorePlayRecordVersionCommandHandler(
            repo.Object,
            versions.Object,
            new Mock<IUnitOfWork>().Object,
            TimeProvider.System,
            new PlayRecordPermissionChecker(repo.Object));

        await sut.Handle(
            new RestorePlayRecordVersionCommand(record.Id, version.VersionNumber, CreatorId),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_VersionWithoutNotes_ClearsTheCurrentNotes()
    {
        var record = EditedRecord();
        var version = VersionOf(record, notes: null, location: "Bar");

        await RestoreAsync(record, version);

        record.Notes.Should().BeNull("the version was taken when the record had no notes");
    }

    [Fact]
    public async Task Handle_VersionWithoutLocation_ClearsTheCurrentLocation()
    {
        var record = EditedRecord();
        var version = VersionOf(record, notes: "Written after the version", location: null);

        await RestoreAsync(record, version);

        record.Location.Should().BeNull("the version was taken when the record had no location");
    }

    [Fact]
    public async Task Handle_VersionWithEmptyNotes_LeavesEmptyNotes()
    {
        var record = EditedRecord();
        var version = VersionOf(record, notes: "", location: "Bar");

        await RestoreAsync(record, version);

        record.Notes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_VersionWithNotesAndLocation_RestoresBothAndTheDate()
    {
        var record = EditedRecord();
        var earlier = OriginalDate.AddDays(-1);
        var version = VersionOf(record, notes: "Original notes", location: "Home", sessionDate: earlier);

        await RestoreAsync(record, version);

        record.Notes.Should().Be("Original notes");
        record.Location.Should().Be("Home");
        record.SessionDate.Should().Be(earlier);
    }

    [Fact]
    public async Task Handle_RestoringAVersionWithoutNotes_SnapshotsTheNotesItIsAboutToClear()
    {
        // The restore is undo-able: the state it overwrites is saved first, so the cleared notes are not lost.
        var record = EditedRecord();
        var version = VersionOf(record, notes: null, location: "Bar");
        PlayRecordVersion? snapshot = null;

        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(CreatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var versions = new Mock<IPlayRecordVersionRepository>();
        versions.Setup(r => r.GetByVersionNumberAsync(record.Id, 1, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        versions.Setup(r => r.GetNextVersionNumberAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        versions.Setup(r => r.AddAsync(It.IsAny<PlayRecordVersion>(), It.IsAny<CancellationToken>()))
            .Callback<PlayRecordVersion, CancellationToken>((v, _) => snapshot = v)
            .Returns(Task.CompletedTask);
        var sut = new RestorePlayRecordVersionCommandHandler(
            repo.Object, versions.Object, new Mock<IUnitOfWork>().Object, TimeProvider.System,
            new PlayRecordPermissionChecker(repo.Object));

        await sut.Handle(new RestorePlayRecordVersionCommand(record.Id, 1, CreatorId), TestContext.Current.CancellationToken);

        snapshot.Should().NotBeNull();
        snapshot!.Notes.Should().Be("Written after the version");
        record.Notes.Should().BeNull();
    }
}
