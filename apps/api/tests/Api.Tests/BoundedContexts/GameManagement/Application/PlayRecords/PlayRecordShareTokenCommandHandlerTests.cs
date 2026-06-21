using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for <see cref="GeneratePlayRecordShareTokenCommandHandler"/>
/// and <see cref="RevokePlayRecordShareTokenCommandHandler"/> (issue #2437-2).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2437")]
public class PlayRecordShareTokenCommandHandlerTests
{
    private static PlayRecord MakeRecord(Guid creatorId) =>
        PlayRecord.CreateFreeForm(
            Guid.NewGuid(), "Catan", creatorId,
            DateTime.UtcNow.AddDays(-1), PlayRecordVisibility.Private,
            SessionScoringConfig.CreateDefault());

    // ─── Generate ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_ReturnsNonEmptyToken_AndSaves()
    {
        var creatorId = Guid.NewGuid();
        var record = MakeRecord(creatorId);
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        var uow = new Mock<IUnitOfWork>();

        var handler = new GeneratePlayRecordShareTokenCommandHandler(repo.Object, uow.Object);
        var result = await handler.Handle(
            new GeneratePlayRecordShareTokenCommand(record.Id, creatorId), CancellationToken.None);

        result.Should().NotBeNull();
        result.ShareToken.Should().NotBeNullOrEmpty();
        result.ShareUrl.Should().Contain(result.ShareToken);

        repo.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Generate_NotCreator_ThrowsForbiddenException()
    {
        var creatorId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var record = MakeRecord(creatorId);
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        var uow = new Mock<IUnitOfWork>();

        var handler = new GeneratePlayRecordShareTokenCommandHandler(repo.Object, uow.Object);
        var act = () => handler.Handle(
            new GeneratePlayRecordShareTokenCommand(record.Id, otherUser), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Generate_RecordNotFound_ThrowsNotFoundException()
    {
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayRecord?)null);
        var uow = new Mock<IUnitOfWork>();

        var handler = new GeneratePlayRecordShareTokenCommandHandler(repo.Object, uow.Object);
        var act = () => handler.Handle(
            new GeneratePlayRecordShareTokenCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Revoke ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_ClearsToken_AndSaves()
    {
        var creatorId = Guid.NewGuid();
        var record = MakeRecord(creatorId);
        // Pre-generate so there is a token to revoke.
        record.GenerateShareToken();
        record.ShareToken.Should().NotBeNull("pre-condition: token must exist before revoke");

        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        var uow = new Mock<IUnitOfWork>();

        var handler = new RevokePlayRecordShareTokenCommandHandler(repo.Object, uow.Object);
        await handler.Handle(
            new RevokePlayRecordShareTokenCommand(record.Id, creatorId), CancellationToken.None);

        record.ShareToken.Should().BeNull();
        repo.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Revoke_NotCreator_ThrowsForbiddenException()
    {
        var creatorId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var record = MakeRecord(creatorId);
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        var uow = new Mock<IUnitOfWork>();

        var handler = new RevokePlayRecordShareTokenCommandHandler(repo.Object, uow.Object);
        var act = () => handler.Handle(
            new RevokePlayRecordShareTokenCommand(record.Id, otherUser), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revoke_RecordNotFound_ThrowsNotFoundException()
    {
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayRecord?)null);
        var uow = new Mock<IUnitOfWork>();

        var handler = new RevokePlayRecordShareTokenCommandHandler(repo.Object, uow.Object);
        var act = () => handler.Handle(
            new RevokePlayRecordShareTokenCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
