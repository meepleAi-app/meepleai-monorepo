using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class GetGameBookByIdQueryHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_ExistingBook_ReturnsDto()
    {
        var book = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()), "Rules",
            GameBookRole.RulesReference, ParagraphScheme.None, "en", false,
            null, true, AdminId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var handler = new GetGameBookByIdQueryHandler(repo.Object);
        var dto = await handler.Handle(new GetGameBookByIdQuery(book.Id), TestContext.Current.CancellationToken);

        dto.Id.Should().Be(book.Id);
    }

    [Fact]
    public async Task Handle_BookNotFound_ThrowsNotFoundException()
    {
        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameBook?)null);

        var handler = new GetGameBookByIdQueryHandler(repo.Object);
        var act = () => handler.Handle(new GetGameBookByIdQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
