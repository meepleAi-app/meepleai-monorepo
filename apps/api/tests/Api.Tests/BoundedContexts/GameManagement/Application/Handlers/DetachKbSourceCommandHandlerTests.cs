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
public class DetachKbSourceCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_DetachKbSource_ClearsKbSourceDocId()
    {
        var pdfId = Guid.NewGuid();
        var book = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()), "Rules",
            GameBookRole.RulesReference, ParagraphScheme.None, "en", false,
            pdfId, false, AdminId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
        var uow = new Mock<IUnitOfWork>();
        var handler = new DetachKbSourceCommandHandler(repo.Object, uow.Object);

        var dto = await handler.Handle(new DetachKbSourceCommand(book.Id, AdminId), TestContext.Current.CancellationToken);

        dto.KbSourceDocId.Should().BeNull();
        repo.Verify(r => r.UpdateAsync(book, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
