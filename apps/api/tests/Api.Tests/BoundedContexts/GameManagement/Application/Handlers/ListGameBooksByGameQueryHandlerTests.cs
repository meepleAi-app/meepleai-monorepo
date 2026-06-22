using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class ListGameBooksByGameQueryHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_ListByGameRef_ReturnsCommunityBooks()
    {
        var gameRef = GameRef.Shared(Guid.NewGuid());
        var books = new[]
        {
            GameBook.CreateCommunity(gameRef, "Press Start", GameBookRole.Tutorial,
                ParagraphScheme.None, "en", false, null, true, AdminId),
            GameBook.CreateCommunity(gameRef, "Rules", GameBookRole.RulesReference,
                ParagraphScheme.None, "en", false, null, true, AdminId),
        };

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.ListByGameRefAsync(gameRef, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(books);

        var handler = new ListGameBooksByGameQueryHandler(repo.Object);
        var result = await handler.Handle(
            new ListGameBooksByGameQuery(gameRef, null), TestContext.Current.CancellationToken);

        result.Count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ListIncludesPersonalForOwner()
    {
        var gameRef = GameRef.Shared(Guid.NewGuid());
        var ownerId = Guid.NewGuid();
        var books = new[]
        {
            GameBook.CreateCommunity(gameRef, "Community", GameBookRole.Tutorial,
                ParagraphScheme.None, "en", false, null, true, AdminId),
            GameBook.CreatePersonal(gameRef, ownerId, "Personal", GameBookRole.Narrative,
                ParagraphScheme.None, "en", false, null, true),
        };

        var repo = new Mock<IGameBookRepository>();
        repo.Setup(r => r.ListByGameRefAsync(gameRef, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(books);

        var handler = new ListGameBooksByGameQueryHandler(repo.Object);
        var result = await handler.Handle(
            new ListGameBooksByGameQuery(gameRef, ownerId), TestContext.Current.CancellationToken);

        result.Count.Should().Be(2);
        result.Should().Contain(b => b.OwnerUserId == ownerId);
    }
}
