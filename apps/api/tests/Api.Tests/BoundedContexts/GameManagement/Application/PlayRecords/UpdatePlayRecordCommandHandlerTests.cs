using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for <see cref="UpdatePlayRecordCommandHandler"/>.
/// Issue #3889 (update) + #2437-3 (pre-update versioning + last-5 cap).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2437")]
public class UpdatePlayRecordCommandHandlerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static readonly Guid CreatorId = Guid.NewGuid();

    /// <summary>Creates a real PlayRecord with known initial Notes and Location.</summary>
    private static PlayRecord MakeRecord(string? notes = "Initial notes", string? location = "Home") =>
        PlayRecord.CreateFreeForm(
            Guid.NewGuid(),
            "Catan",
            CreatorId,
            DateTime.UtcNow.AddDays(-1),
            PlayRecordVisibility.Private,
            SessionScoringConfig.CreateDefault());

    private static UpdatePlayRecordCommandHandler CreateSut(
        Mock<IPlayRecordRepository> repo,
        Mock<IPlayRecordVersionRepository> versionRepo,
        Mock<IUnitOfWork> uow,
        TimeProvider? timeProvider = null)
    {
        return new UpdatePlayRecordCommandHandler(
            repo.Object,
            versionRepo.Object,
            uow.Object,
            timeProvider ?? TimeProvider.System,
            new PlayRecordPermissionChecker(repo.Object));
    }

    /// <summary>
    /// Overload for the #2461 retry tests — injects a custom conflict detector so tests can
    /// simulate a version-number unique violation without a real Npgsql.PostgresException.
    /// </summary>
    private static UpdatePlayRecordCommandHandler CreateSutWithConflictDetector(
        Mock<IPlayRecordRepository> repo,
        Mock<IPlayRecordVersionRepository> versionRepo,
        Mock<IUnitOfWork> uow,
        Func<DbUpdateException, bool> isVersionConflict,
        TimeProvider? timeProvider = null)
    {
        return new UpdatePlayRecordCommandHandler(
            repo.Object,
            versionRepo.Object,
            uow.Object,
            timeProvider ?? TimeProvider.System,
            new PlayRecordPermissionChecker(repo.Object),
            isVersionConflict);
    }

    // -------------------------------------------------------------------------
    // Guard / auth tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_RecordNotFound_ThrowsNotFound()
    {
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayRecord?)null);
        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        var uow = new Mock<IUnitOfWork>();

        var act = () => CreateSut(repo, versionRepo, uow).Handle(
            new UpdatePlayRecordCommand(Guid.NewGuid(), CreatorId, Notes: "x"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        versionRepo.Verify(v => v.AddAsync(It.IsAny<PlayRecordVersion>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotCreator_ThrowsForbidden()
    {
        var otherUser = Guid.NewGuid();
        var record = MakeRecord();
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(otherUser, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        var uow = new Mock<IUnitOfWork>();

        var act = () => CreateSut(repo, versionRepo, uow).Handle(
            new UpdatePlayRecordCommand(record.Id, otherUser, Notes: "x"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        versionRepo.Verify(v => v.AddAsync(It.IsAny<PlayRecordVersion>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Versioning: snapshot captures PRE-edit values
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_Creator_SnapshotsCapturesPreEditValues()
    {
        // Arrange: record starts with Notes = "Original notes", Location = "Cave"
        var record = MakeRecord();

        // Manually set initial notes via UpdateDetails (factory doesn't accept notes).
        // We call UpdateDetails with the "original" values so the snapshot can capture them.
        record.UpdateDetails(notes: "Original notes", location: "Cave");
        // Clear any events the domain raised so counts stay clean.

        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(CreatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        versionRepo.Setup(v => v.GetNextVersionNumberAsync(record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        PlayRecordVersion? capturedVersion = null;
        versionRepo
            .Setup(v => v.AddAsync(It.IsAny<PlayRecordVersion>(), It.IsAny<CancellationToken>()))
            .Callback<PlayRecordVersion, CancellationToken>((ver, _) => capturedVersion = ver)
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();

        // Act: update with new notes — snapshot should capture the OLD values
        await CreateSut(repo, versionRepo, uow).Handle(
            new UpdatePlayRecordCommand(record.Id, CreatorId, Notes: "Updated notes", Location: "Library"),
            CancellationToken.None);

        // Assert: snapshot contains pre-edit values
        capturedVersion.Should().NotBeNull("AddAsync must have been called");
        capturedVersion!.PlayRecordId.Should().Be(record.Id);
        capturedVersion.VersionNumber.Should().Be(1);
        capturedVersion.Notes.Should().Be("Original notes",   "snapshot must capture the PRE-edit notes");
        capturedVersion.Location.Should().Be("Cave",          "snapshot must capture the PRE-edit location");
        capturedVersion.CreatedByUserId.Should().Be(CreatorId);

        // The record itself must now reflect the new values.
        record.Notes.Should().Be("Updated notes");
        record.Location.Should().Be("Library");
    }

    [Fact]
    public async Task Handle_Creator_CallsGetNextVersionNumber_And_AddAsync_And_PruneOldest()
    {
        var record = MakeRecord();
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(CreatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        versionRepo.Setup(v => v.GetNextVersionNumberAsync(record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var uow = new Mock<IUnitOfWork>();

        await CreateSut(repo, versionRepo, uow).Handle(
            new UpdatePlayRecordCommand(record.Id, CreatorId, Notes: "New"),
            CancellationToken.None);

        versionRepo.Verify(v => v.GetNextVersionNumberAsync(record.Id, It.IsAny<CancellationToken>()), Times.Once);
        versionRepo.Verify(v => v.AddAsync(It.IsAny<PlayRecordVersion>(), It.IsAny<CancellationToken>()), Times.Once);
        versionRepo.Verify(v => v.PruneOldestAsync(record.Id, PlayRecordVersionSnapshotter.MaxVersions, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Creator_SavesChanges_Twice_UpdateThenPrune()
    {
        // Verifies the two-save ordering: save1=update+version, save2=prune.
        var record = MakeRecord();
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(CreatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        versionRepo.Setup(v => v.GetNextVersionNumberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var uow = new Mock<IUnitOfWork>();

        await CreateSut(repo, versionRepo, uow).Handle(
            new UpdatePlayRecordCommand(record.Id, CreatorId, Notes: "x"),
            CancellationToken.None);

        // Two saves: one after update+snapshot, one after prune.
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // -------------------------------------------------------------------------
    // Existing behaviour still passes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_Creator_UpdatesRecord_And_Saves()
    {
        var record = MakeRecord();
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(CreatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        versionRepo.Setup(v => v.GetNextVersionNumberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var uow = new Mock<IUnitOfWork>();

        await CreateSut(repo, versionRepo, uow).Handle(
            new UpdatePlayRecordCommand(record.Id, CreatorId, Notes: "My note", Location: "Pub"),
            CancellationToken.None);

        record.Notes.Should().Be("My note");
        record.Location.Should().Be("Pub");
        repo.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // #2461: version-number conflict predicate
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("23505", "UX_play_record_versions_record_version", true)]
    [InlineData("23505", "ux_play_record_versions_record_version", false)] // case-sensitive
    [InlineData("23505", "UX_some_other_unique_index", false)]             // wrong constraint
    [InlineData("23503", "UX_play_record_versions_record_version", false)] // FK violation, not unique
    [InlineData("23505", null, false)]
    [InlineData(null, "UX_play_record_versions_record_version", false)]
    [InlineData(null, null, false)]
    public void IsVersionNumberConflict_PredicateBehavior(
        string? sqlState, string? constraintName, bool expected)
    {
        var result = PlayRecordVersionRepository.IsVersionNumberConflict(sqlState, constraintName);

        result.Should().Be(expected, "predicate must match exactly SqlState 23505 + the right constraint name");
    }

    // -------------------------------------------------------------------------
    // #2461: retry on version-number unique conflict
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Handle_VersionNumberConflictOnFirstSave_RetriesAndSucceeds()
    {
        // Arrange: happy-path record + permissions
        var record = MakeRecord();
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(CreatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // GetNextVersionNumberAsync is called:
        //  - once by SnapshotCurrentAsync (initial number)
        //  - once by ReassignVersionNumberAsync (re-read after conflict)
        // Both return 1 here; in production the second call would return a higher value.
        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        versionRepo.Setup(v => v.GetNextVersionNumberAsync(record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        versionRepo.Setup(v => v.ReassignVersionNumberAsync(
                record.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // SaveChangesAsync: throw on attempt 1 (version conflict), succeed on attempt 2+.
        var saveCallCount = 0;
        var conflictException = new DbUpdateException("version conflict simulation");
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                saveCallCount++;
                if (saveCallCount == 1)
                    throw conflictException; // first save: simulate unique violation
                return 1; // subsequent saves: succeed
            });

        // Inject a conflict detector that recognises our synthetic exception.
        Func<DbUpdateException, bool> detector = ex => ReferenceEquals(ex, conflictException);

        var sut = CreateSutWithConflictDetector(repo, versionRepo, uow, detector);

        // Act — must NOT throw
        var act = () => sut.Handle(
            new UpdatePlayRecordCommand(record.Id, CreatorId, Notes: "Retry notes"),
            CancellationToken.None);

        await act.Should().NotThrowAsync("the handler must absorb the version conflict and retry");

        // Assert: ReassignVersionNumberAsync was invoked exactly once (the retry)
        versionRepo.Verify(
            v => v.ReassignVersionNumberAsync(record.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "must re-read the max version number on conflict");

        // Two save cycles: (1) first save = conflict caught → retry; (2) second save = OK.
        // Plus one more save for the prune step → total 3 SaveChangesAsync calls.
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));

        // The record update must still have been applied despite the initial conflict.
        record.Notes.Should().Be("Retry notes", "the update must persist after the retry");
    }

    [Fact]
    public async Task Handle_NonVersionConflictDbUpdateException_Rethrows()
    {
        // Arrange: simulate a DbUpdateException that is NOT a version-number conflict.
        var record = MakeRecord();
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(CreatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var versionRepo = new Mock<IPlayRecordVersionRepository>();
        versionRepo.Setup(v => v.GetNextVersionNumberAsync(record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var differentException = new DbUpdateException("other db error");
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(differentException);

        // Detector always returns false (this is not a version conflict)
        Func<DbUpdateException, bool> detector = _ => false;

        var sut = CreateSutWithConflictDetector(repo, versionRepo, uow, detector);

        var act = () => sut.Handle(
            new UpdatePlayRecordCommand(record.Id, CreatorId, Notes: "x"),
            CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>()
            .Where(ex => ReferenceEquals(ex, differentException),
                   "unrelated DbUpdateExceptions must not be swallowed");

        // ReassignVersionNumberAsync must never be called on a non-version conflict
        versionRepo.Verify(
            v => v.ReassignVersionNumberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
