using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Unit tests for <see cref="ImportGameFromBggCommandHandler"/>.
/// Issue #3099: duplicate BGG ID must surface as 409 Conflict (ConflictException),
/// not a fabricated 500 via InvalidOperationException.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "3099")]
public sealed class ImportGameFromBggCommandHandlerTests : IDisposable
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IBggApiService> _bggApiServiceMock;
    private readonly Mock<ISharedGameCacheInvalidator> _invalidatorMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<ImportGameFromBggCommandHandler>> _loggerMock;
    private readonly ImportGameFromBggCommandHandler _handler;

    /// <summary>Il salvataggio e le evizioni, nell'ordine in cui sono avvenuti.</summary>
    private readonly List<string> _events = [];

    public ImportGameFromBggCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _bggApiServiceMock = new Mock<IBggApiService>();
        _invalidatorMock = new Mock<ISharedGameCacheInvalidator>();
        _loggerMock = new Mock<ILogger<ImportGameFromBggCommandHandler>>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"ImportBggTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new ImportGameFromBggCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _bggApiServiceMock.Object,
            _dbContext,
            _invalidatorMock.Object,
            _loggerMock.Object);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _events.Add(Saved))
            .ReturnsAsync(1);
        _invalidatorMock
            .Setup(i => i.EvictDetailAndListAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) => _events.Add(Evicted(id)))
            .Returns(Task.CompletedTask);
    }

    private const string Saved = "saved";

    private static string Evicted(Guid id) => $"evict:{id}";

    /// <summary>
    /// Un import che riesce: BGG risponde, il repository "salva" l'aggregato nel contesto (il gestore lo
    /// ritrova con `FindAsync` per i collegamenti), lo unit of work registra il salvataggio.
    /// </summary>
    private void ArrangeSuccessfulImport(int bggId)
    {
        _repositoryMock
            .Setup(r => r.ExistsByBggIdAsync(bggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _bggApiServiceMock
            .Setup(s => s.GetGameDetailsAsync(bggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggGameDetailsDto(
                BggId: bggId, Name: "Catan", Description: "Trade and build", YearPublished: 1995,
                MinPlayers: 3, MaxPlayers: 4, PlayingTime: 90, MinPlayTime: 60, MaxPlayTime: 120, MinAge: 10,
                AverageRating: 7.1, BayesAverageRating: 7.0, UsersRated: 1000, AverageWeight: 2.3,
                ThumbnailUrl: null, ImageUrl: null,
                Categories: [], Mechanics: [], Designers: [], Publishers: []));
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((game, _) => _dbContext.Set<SharedGameEntity>().Add(
                new SharedGameEntity { Id = game.Id, Title = game.Title }))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// #4659. L'import in coda con `AutoPublish` crea un gioco già `Published`: entra subito nella lista
    /// pubblica e nelle rail del catalogo, che hanno una voce in cache. La lista e le rail si tolgono DOPO il
    /// salvataggio: prima, una lettura le ripopolerebbe senza il gioco.
    /// </summary>
    [Fact]
    public async Task Handle_AutoPublish_EvictsTheListAndTheRails_AfterTheSave()
    {
        ArrangeSuccessfulImport(13);

        var id = await _handler.Handle(
            new ImportGameFromBggCommand(BggId: 13, UserId: Guid.NewGuid(), AutoPublish: true),
            TestContext.Current.CancellationToken);

        _events.Should().Equal(Saved, Evicted(id));
    }

    [Fact]
    public async Task Handle_WithoutAutoPublish_EvictsNothing_ADraftIsInNoPublicList()
    {
        ArrangeSuccessfulImport(14);

        await _handler.Handle(
            new ImportGameFromBggCommand(BggId: 14, UserId: Guid.NewGuid(), AutoPublish: false),
            TestContext.Current.CancellationToken);

        _events.Should().Equal(Saved);
    }

    [Fact]
    public async Task Handle_WhenTheSaveFails_EvictsNothing()
    {
        ArrangeSuccessfulImport(16);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("boom"));

        var act = () => _handler.Handle(
            new ImportGameFromBggCommand(BggId: 16, UserId: Guid.NewGuid(), AutoPublish: true),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
        _events.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithDuplicateBggId_ThrowsConflictException()
    {
        // Arrange
        var command = new ImportGameFromBggCommand(BggId: 13, UserId: Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.ExistsByBggIdAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists in the catalog*");

        // The conflict must short-circuit before any BGG API call or persistence.
        _bggApiServiceMock.Verify(
            s => s.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
