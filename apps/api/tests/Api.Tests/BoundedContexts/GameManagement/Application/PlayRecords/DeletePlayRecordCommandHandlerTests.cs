using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
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
/// Unit tests for <see cref="DeletePlayRecordCommandHandler"/> (issue #2439).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2439")]
public class DeletePlayRecordCommandHandlerTests
{
    private static PlayRecord MakeRecord(Guid creatorId) =>
        PlayRecord.CreateFreeForm(
            Guid.NewGuid(), "Catan", creatorId,
            DateTime.UtcNow.AddDays(-1), PlayRecordVisibility.Private,
            SessionScoringConfig.CreateDefault());

    [Fact]
    public async Task Handle_RecordNotFound_ThrowsNotFound()
    {
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayRecord?)null);
        var uow = new Mock<IUnitOfWork>();
        var checker = new PlayRecordPermissionChecker(repo.Object);
        var handler = new DeletePlayRecordCommandHandler(repo.Object, uow.Object, checker);

        var act = () => handler.Handle(
            new DeletePlayRecordCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotCreator_ThrowsForbidden()
    {
        var creatorId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var record = MakeRecord(creatorId);
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(otherUser, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var uow = new Mock<IUnitOfWork>();
        var checker = new PlayRecordPermissionChecker(repo.Object);
        var handler = new DeletePlayRecordCommandHandler(repo.Object, uow.Object, checker);

        var act = () => handler.Handle(
            new DeletePlayRecordCommand(record.Id, otherUser), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Creator_SoftDeletesAndSaves()
    {
        var creatorId = Guid.NewGuid();
        var record = MakeRecord(creatorId);
        var repo = new Mock<IPlayRecordRepository>();
        repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        repo.Setup(r => r.CanUserEditAsync(creatorId, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var uow = new Mock<IUnitOfWork>();
        var checker = new PlayRecordPermissionChecker(repo.Object);
        var handler = new DeletePlayRecordCommandHandler(repo.Object, uow.Object, checker);

        await handler.Handle(new DeletePlayRecordCommand(record.Id, creatorId), CancellationToken.None);

        record.IsDeleted.Should().BeTrue();
        repo.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
