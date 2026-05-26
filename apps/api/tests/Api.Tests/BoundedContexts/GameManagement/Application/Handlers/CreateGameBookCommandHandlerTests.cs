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
public class CreateGameBookCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_ValidCommunityCommand_CreatesBookAndReturnsDto()
    {
        var repo = new Mock<IGameBookRepository>();
        var uow = new Mock<IUnitOfWork>();
        var handler = new CreateGameBookCommandHandler(repo.Object, uow.Object);

        var cmd = new CreateGameBookCommand(
            GameRef: GameRef.Shared(Guid.NewGuid()),
            OwnerUserId: null,
            DisplayName: "Press Start",
            Roles: (int)(GameBookRole.Tutorial | GameBookRole.Setup),
            ParagraphScheme: (int)ParagraphScheme.None,
            Language: "en",
            SequentialRead: false,
            KbSourceDocId: null,
            PhysicalOnly: true,
            RequestedBy: AdminId);

        var dto = await handler.Handle(cmd, TestContext.Current.CancellationToken);

        dto.DisplayName.Should().Be("Press Start");
        repo.Verify(r => r.AddAsync(It.IsAny<GameBook>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_KbSourceConflict_ThrowsConflictException()
    {
        var pdfId = Guid.NewGuid();
        var existing = GameBook.CreateCommunity(
            GameRef.Shared(Guid.NewGuid()), "Existing", GameBookRole.RulesReference,
            ParagraphScheme.None, "en", false, pdfId, false, AdminId);

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.FindCommunityByKbSourceAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var uow = new Mock<IUnitOfWork>();
        var handler = new CreateGameBookCommandHandler(repo.Object, uow.Object);

        var cmd = new CreateGameBookCommand(
            GameRef.Shared(Guid.NewGuid()), null, "Conflicting",
            (int)GameBookRole.RulesReference, (int)ParagraphScheme.None, "en", false,
            pdfId, false, AdminId);

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ConflictException>();
    }
}
