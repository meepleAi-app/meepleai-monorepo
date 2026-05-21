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
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_RenameAndUpdateRoles_PersistsChanges()
    {
        var book = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()), "Old Name",
            GameBookRole.Tutorial, ParagraphScheme.None, "en", false, null, true, AdminId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var uow = new Mock<IUnitOfWork>();
        var handler = new UpdateGameBookCommandHandler(repo.Object, uow.Object);

        var cmd = new UpdateGameBookCommand(
            book.Id, "New Name",
            Roles: (int)(GameBookRole.Tutorial | GameBookRole.RulesReference),
            RequestedBy: AdminId);

        var dto = await handler.Handle(cmd, TestContext.Current.CancellationToken);

        dto.DisplayName.Should().Be("New Name");
        dto.Roles.Should().Be((int)(GameBookRole.Tutorial | GameBookRole.RulesReference));
        repo.Verify(r => r.UpdateAsync(book, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BookNotFound_ThrowsNotFoundException()
    {
        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameBook?)null);

        var handler = new UpdateGameBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);
        var cmd = new UpdateGameBookCommand(Guid.NewGuid(), "X", (int)GameBookRole.Tutorial, AdminId);

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
