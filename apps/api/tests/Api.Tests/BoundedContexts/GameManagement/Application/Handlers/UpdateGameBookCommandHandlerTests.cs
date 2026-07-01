using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class UpdateGameBookCommandHandlerTests
{
    private static GameBook PersonalBook(Guid ownerId) => GameBook.CreatePersonal(
        GameRef.Shared(Guid.NewGuid()), ownerId, "Old Name",
        GameBookRole.Tutorial, ParagraphScheme.None, "en", false, null, true);

    [Fact]
    public async Task Handle_ByOwner_RenameAndUpdateRoles_PersistsChanges()
    {
        var ownerId = Guid.NewGuid();
        var book = PersonalBook(ownerId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var uow = new Mock<IUnitOfWork>();
        var handler = new UpdateGameBookCommandHandler(repo.Object, uow.Object);

        var cmd = new UpdateGameBookCommand(
            book.Id, "New Name",
            Roles: (int)(GameBookRole.Tutorial | GameBookRole.RulesReference),
            RequestedBy: ownerId);

        var dto = await handler.Handle(cmd, TestContext.Current.CancellationToken);

        dto.DisplayName.Should().Be("New Name");
        dto.Roles.Should().Be((int)(GameBookRole.Tutorial | GameBookRole.RulesReference));
        repo.Verify(r => r.UpdateAsync(book, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ByNonOwner_ThrowsForbiddenAndDoesNotPersist()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var book = PersonalBook(ownerId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var uow = new Mock<IUnitOfWork>();
        var handler = new UpdateGameBookCommandHandler(repo.Object, uow.Object);

        var cmd = new UpdateGameBookCommand(book.Id, "Hijacked", (int)GameBookRole.Narrative, RequestedBy: otherUserId);

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        repo.Verify(r => r.UpdateAsync(It.IsAny<GameBook>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_BookNotFound_ThrowsNotFoundException()
    {
        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameBook?)null);

        var handler = new UpdateGameBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);
        var cmd = new UpdateGameBookCommand(Guid.NewGuid(), "X", (int)GameBookRole.Tutorial, Guid.NewGuid());

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
