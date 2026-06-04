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
public sealed class ApproveCatalogSeedCommandHandlerTests
{
    private readonly Mock<ICatalogSeedDraftRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly ApproveCatalogSeedCommandHandler _handler;
    private readonly Guid _draftId = Guid.NewGuid();
    private readonly Guid _approver = Guid.NewGuid();

    public ApproveCatalogSeedCommandHandlerTests()
    {
        _handler = new ApproveCatalogSeedCommandHandler(
            _repoMock.Object,
            _uowMock.Object,
            NullLogger<ApproveCatalogSeedCommandHandler>.Instance,
            _mediatorMock.Object);
    }

    private static CatalogSeedDraftEntity FetchedDraft(Guid id) => new()
    {
        Id = id,
        BggId = 174430,
        Status = nameof(CatalogSeedStatus.Fetched),
        ProvenanceJson = "{}",
        CreatedByUserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        FetchedAt = DateTime.UtcNow.AddMinutes(-1),
    };

    [Fact]
    public async Task Handle_FetchedDraft_TransitionsToApprovedAndPublishesEvent()
    {
        var draft = FetchedDraft(_draftId);
        _repoMock.Setup(r => r.GetByIdAsync(_draftId, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(
            new ApproveCatalogSeedCommand(_draftId, _approver),
            TestContext.Current.CancellationToken);

        result.DraftId.Should().Be(_draftId);
        result.ResultingSharedGameId.Should().NotBeEmpty();

        draft.Status.Should().Be(nameof(CatalogSeedStatus.Approved));
        draft.ApprovedAt.Should().NotBeNull();
        draft.ApprovedByUserId.Should().Be(_approver);
        draft.ResultingSharedGameId.Should().Be(result.ResultingSharedGameId);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(
            m => m.Publish(
                It.Is<CatalogSeedApprovedEvent>(e => e.DraftId == _draftId && e.ApprovedByUserId == _approver),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DraftStatusNotFetched_ThrowsConflictException()
    {
        var draft = FetchedDraft(_draftId);
        draft.Status = nameof(CatalogSeedStatus.Pending);
        _repoMock.Setup(r => r.GetByIdAsync(_draftId, It.IsAny<CancellationToken>())).ReturnsAsync(draft);

        var act = () => _handler.Handle(
            new ApproveCatalogSeedCommand(_draftId, _approver),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"*{_draftId}*Pending*");

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<CatalogSeedApprovedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DraftNotFound_ThrowsNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(_draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogSeedDraftEntity?)null);

        var act = () => _handler.Handle(
            new ApproveCatalogSeedCommand(_draftId, _approver),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<CatalogSeedApprovedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SaveThrows_DoesNotPublishEvent()
    {
        var draft = FetchedDraft(_draftId);
        _repoMock.Setup(r => r.GetByIdAsync(_draftId, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db boom"));

        var act = () => _handler.Handle(
            new ApproveCatalogSeedCommand(_draftId, _approver),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("db boom");

        // Critical post-save-publish invariant (issue #1873 H1):
        // event MUST NOT fire if SaveChangesAsync threw.
        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<CatalogSeedApprovedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public void Constructor_NullRepo_Throws()
    {
        var act = () => new ApproveCatalogSeedCommandHandler(
            repository: null!,
            _uowMock.Object,
            NullLogger<ApproveCatalogSeedCommandHandler>.Instance,
            _mediatorMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_NullMediator_Throws()
    {
        var act = () => new ApproveCatalogSeedCommandHandler(
            _repoMock.Object,
            _uowMock.Object,
            NullLogger<ApproveCatalogSeedCommandHandler>.Instance,
            mediator: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("mediator");
    }
}
