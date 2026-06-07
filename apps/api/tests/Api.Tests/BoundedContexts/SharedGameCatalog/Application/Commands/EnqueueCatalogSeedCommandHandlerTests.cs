using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class EnqueueCatalogSeedCommandHandlerTests
{
    private readonly Mock<ICatalogSeedDraftRepository> _repoMock = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly EnqueueCatalogSeedCommandHandler _handler;
    private readonly Guid _actor = Guid.NewGuid();

    public EnqueueCatalogSeedCommandHandlerTests()
    {
        _handler = new EnqueueCatalogSeedCommandHandler(
            _repoMock.Object,
            _uowMock.Object,
            NullLogger<EnqueueCatalogSeedCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NewBggId_InsertsPendingDraftAndSaves()
    {
        const int bggId = 174430;
        _repoMock.Setup(r => r.ExistsAsync(bggId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        CatalogSeedDraftEntity? captured = null;
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<CatalogSeedDraftEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CatalogSeedDraftEntity, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new EnqueueCatalogSeedCommand(bggId, null, null, _actor);
        var result = await _handler.Handle(cmd, TestContext.Current.CancellationToken);

        result.IsDuplicate.Should().BeFalse();
        result.BggId.Should().Be(bggId);
        result.Status.Should().Be(nameof(CatalogSeedStatus.Pending));
        result.Id.Should().NotBeEmpty();

        captured.Should().NotBeNull();
        captured!.BggId.Should().Be(bggId);
        captured.Status.Should().Be(nameof(CatalogSeedStatus.Pending));
        captured.CreatedByUserId.Should().Be(_actor);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingBggId_ReturnsDuplicateWithoutInsert()
    {
        const int bggId = 13;
        _repoMock.Setup(r => r.ExistsAsync(bggId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cmd = new EnqueueCatalogSeedCommand(bggId, null, null, _actor);
        var result = await _handler.Handle(cmd, TestContext.Current.CancellationToken);

        result.IsDuplicate.Should().BeTrue();
        result.BggId.Should().Be(bggId);

        _repoMock.Verify(
            r => r.AddAsync(It.IsAny<CatalogSeedDraftEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public async Task Handle_WikidataOnly_InsertsDraft()
    {
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<CatalogSeedDraftEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new EnqueueCatalogSeedCommand(BggId: null, WikidataQid: "Q42", SearchTermInput: null, _actor);
        var result = await _handler.Handle(cmd, TestContext.Current.CancellationToken);

        result.IsDuplicate.Should().BeFalse();
        result.BggId.Should().BeNull();

        // ExistsAsync only consulted when BggId is set
        _repoMock.Verify(r => r.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullRepo_Throws()
    {
        var act = () => new EnqueueCatalogSeedCommandHandler(
            repository: null!,
            _uowMock.Object,
            NullLogger<EnqueueCatalogSeedCommandHandler>.Instance);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullUow_Throws()
    {
        var act = () => new EnqueueCatalogSeedCommandHandler(
            _repoMock.Object,
            unitOfWork: null!,
            NullLogger<EnqueueCatalogSeedCommandHandler>.Instance);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }
}
