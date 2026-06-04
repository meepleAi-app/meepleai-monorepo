using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class RejectCatalogSeedCommandHandlerTests
{
    private readonly Mock<ICatalogSeedDraftRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly RejectCatalogSeedCommandHandler _handler;
    private readonly Guid _draftId = Guid.NewGuid();
    private readonly Guid _rejecter = Guid.NewGuid();

    public RejectCatalogSeedCommandHandlerTests()
    {
        _handler = new RejectCatalogSeedCommandHandler(
            _repoMock.Object,
            _uowMock.Object,
            NullLogger<RejectCatalogSeedCommandHandler>.Instance,
            _mediatorMock.Object);
    }

    private static CatalogSeedDraftEntity Draft(Guid id) => new()
    {
        Id = id,
        BggId = 9999,
        Status = nameof(CatalogSeedStatus.Fetched),
        CreatedByUserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task Handle_ExistingDraft_SoftDeletesAndPublishesEvent()
    {
        var draft = Draft(_draftId);
        _repoMock.Setup(r => r.GetByIdAsync(_draftId, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _handler.Handle(
            new RejectCatalogSeedCommand(_draftId, _rejecter, "spam"),
            TestContext.Current.CancellationToken);

        draft.Status.Should().Be(nameof(CatalogSeedStatus.Rejected));
        draft.IsDeleted.Should().BeTrue();
        draft.DeletedAt.Should().NotBeNull();

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(
            m => m.Publish(
                It.Is<CatalogSeedRejectedEvent>(e =>
                    e.DraftId == _draftId
                    && e.RejectedByUserId == _rejecter
                    && e.Reason == "spam"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DraftNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(_draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogSeedDraftEntity?)null);

        var act = () => _handler.Handle(
            new RejectCatalogSeedCommand(_draftId, _rejecter, ""),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<CatalogSeedRejectedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SaveThrows_DoesNotPublishEvent()
    {
        var draft = Draft(_draftId);
        _repoMock.Setup(r => r.GetByIdAsync(_draftId, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db boom"));

        var act = () => _handler.Handle(
            new RejectCatalogSeedCommand(_draftId, _rejecter, ""),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("db boom");

        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<CatalogSeedRejectedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NullCommand_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }
}
