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
public class AttachKbSourceCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_AttachKbToCommunityBook_PersistsKbSource()
    {
        var book = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()), "Rules",
            GameBookRole.RulesReference, ParagraphScheme.None, "en", false,
            kbSourceDocId: null, physicalOnly: false, AdminId);
        var pdfId = Guid.NewGuid();

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
        repo.Setup(r => r.FindCommunityByKbSourceAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameBook?)null);

        var uow = new Mock<IUnitOfWork>();
        var handler = new AttachKbSourceCommandHandler(repo.Object, uow.Object);
        var cmd = new AttachKbSourceCommand(book.Id, pdfId, AdminId);

        var dto = await handler.Handle(cmd, TestContext.Current.CancellationToken);

        dto.KbSourceDocId.Should().Be(pdfId);
        repo.Verify(r => r.UpdateAsync(book, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AttachKbConflict_ThrowsConflictException()
    {
        var gameRef = GameRef.Shared(Guid.NewGuid());
        var book = GameBook.CreateCommunity(
            gameRef, "Target", GameBookRole.RulesReference,
            ParagraphScheme.None, "en", false, null, false, AdminId);
        var pdfId = Guid.NewGuid();
        var existing = GameBook.CreateCommunity(
            gameRef, "Existing", GameBookRole.RulesReference,
            ParagraphScheme.None, "en", false, pdfId, false, AdminId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.GetByIdAsync(book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(book);
        repo.Setup(r => r.FindCommunityByKbSourceAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new AttachKbSourceCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);
        var cmd = new AttachKbSourceCommand(book.Id, pdfId, AdminId);

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ConflictException>();
    }
}
