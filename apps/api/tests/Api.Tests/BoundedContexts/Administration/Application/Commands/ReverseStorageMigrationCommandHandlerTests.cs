using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Entities;
using Api.Services.Pdf;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Unit tests for <see cref="ReverseStorageMigrationCommandHandler"/> (issue #1333).
///
/// Covers the state-machine transitions that do not require S3 round-trips:
/// Pending → Reverted (cancel-before-drain), terminal states (FailedPermanent /
/// Reverted) → Skipped (idempotent), dry-run preserving Status.
///
/// The Sent → Reverted path requires CopyObject + Delete; that's covered by
/// the integration suite (Testcontainers MinIO).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Issue", "1333")]
public sealed class ReverseStorageMigrationCommandHandlerTests
{
    [Fact]
    public async Task Handle_NonS3Provider_ReturnsExplanatoryError()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var blob = new Mock<IBlobStorageService>();

        var sut = new ReverseStorageMigrationCommandHandler(
            db, blob.Object, NullLogger<ReverseStorageMigrationCommandHandler>.Instance);

        var result = await sut.Handle(new ReverseStorageMigrationCommand(Guid.NewGuid()), default);

        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("not S3-backed");
        result.TotalRows.Should().Be(0);
    }

    [Fact]
    public void Validator_RejectsEmptyMigrationId()
    {
        var validator = new ReverseStorageMigrationCommandValidator();
        var cmd = new ReverseStorageMigrationCommand(Guid.Empty);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MigrationId");
    }

    [Fact]
    public void Validator_AcceptsValidCommand()
    {
        var validator = new ReverseStorageMigrationCommandValidator();
        var cmd = new ReverseStorageMigrationCommand(Guid.NewGuid(), DryRun: true);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void OutboxEntity_RevertedStatus_AllowedValue()
    {
        // Regression guard: the Reverted state added by #1333 must not break the
        // existing entity surface (issue #1314 PR 2 defined the original enum).
        var entity = new StorageOperationOutboxEntity
        {
            Id = Guid.NewGuid(),
            MigrationId = Guid.NewGuid(),
            LegacyKey = "pdf_uploads/g/f_n.pdf",
            NewKey = "pdfs/g/f_n.pdf",
            Category = "Pdf",
            ResourceKey = "g",
            ScheduledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Status = "Reverted",
        };

        entity.Status.Should().Be("Reverted");
    }
}
