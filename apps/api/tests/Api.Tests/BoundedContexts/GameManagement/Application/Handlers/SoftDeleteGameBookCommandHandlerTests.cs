using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
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
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_SoftDelete_MarksBookAsDeleted()
    {
        var book = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()), "Rules",
            GameBookRole.RulesReference, ParagraphScheme.None, "en", false,
            null, true, AdminId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
        var uow = new Mock<IUnitOfWork>();
        var handler = new SoftDeleteGameBookCommandHandler(repo.Object, uow.Object);

        await handler.Handle(new SoftDeleteGameBookCommand(book.Id, AdminId), TestContext.Current.CancellationToken);

        book.IsDeleted.Should().BeTrue();
        book.DeletedAt.Should().NotBeNull();
        repo.Verify(r => r.UpdateAsync(book, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
