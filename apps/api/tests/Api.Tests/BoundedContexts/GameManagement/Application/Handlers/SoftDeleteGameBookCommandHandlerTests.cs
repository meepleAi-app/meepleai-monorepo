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
public class SoftDeleteGameBookCommandHandlerTests
{
    private static GameBook PersonalBook(Guid ownerId) => GameBook.CreatePersonal(
        GameRef.Shared(Guid.NewGuid()), ownerId, "Rules",
        GameBookRole.RulesReference, ParagraphScheme.None, "en", false, null, true);

    [Fact]
    public async Task Handle_ByOwner_SoftDelete_MarksBookAsDeleted()
    {
        var ownerId = Guid.NewGuid();
        var book = PersonalBook(ownerId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
        var uow = new Mock<IUnitOfWork>();
        var handler = new SoftDeleteGameBookCommandHandler(repo.Object, uow.Object);

        await handler.Handle(new SoftDeleteGameBookCommand(book.Id, ownerId), TestContext.Current.CancellationToken);

        book.IsDeleted.Should().BeTrue();
        book.DeletedAt.Should().NotBeNull();
        repo.Verify(r => r.UpdateAsync(book, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ByNonOwner_ThrowsForbiddenAndDoesNotDelete()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var book = PersonalBook(ownerId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
        var uow = new Mock<IUnitOfWork>();
        var handler = new SoftDeleteGameBookCommandHandler(repo.Object, uow.Object);

        var act = () => handler.Handle(new SoftDeleteGameBookCommand(book.Id, otherUserId), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        book.IsDeleted.Should().BeFalse();
        repo.Verify(r => r.UpdateAsync(It.IsAny<GameBook>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_BookNotFound_ThrowsNotFoundException()
    {
        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameBook?)null);

        var handler = new SoftDeleteGameBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);

        var act = () => handler.Handle(new SoftDeleteGameBookCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
