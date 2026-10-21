using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// #4603 — ogni handler che cambia lo stato di un gioco (o lo cancella) toglie dalla cache il dettaglio e
/// la lista DOPO il salvataggio, e non fa niente se il comando fallisce.
/// </summary>
/// <remarks>
/// <para>
/// Il registro <c>_events</c> mette in fila il salvataggio e le evizioni: un'evizione prima del
/// salvataggio lascerebbe che una lettura ripopoli la cache col contenuto vecchio. L'invalidatore è un
/// doppio, perché cosa tolga lo dicono <c>SharedGameCacheInvalidatorTests</c>; qui conta che ogni handler
/// lo chiami, col gioco giusto e al momento giusto. La prova che la voce in cache segua davvero lo stato
/// è end-to-end, in <c>SharedGameCatalogEndpointsIntegrationTests.StatusCache</c>, su Postgres.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameStatusChangeCacheEvictionTests
{
    private const string Saved = "saved";

    private static readonly Guid Admin = Guid.NewGuid();

    private readonly Mock<ISharedGameRepository> _repository = new();
    private readonly Mock<ISharedGameDeleteRequestRepository> _deleteRequests = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISharedGameCacheInvalidator> _invalidator = new();

    /// <summary>Il salvataggio e le evizioni, nell'ordine in cui sono avvenuti.</summary>
    private readonly List<string> _events = [];

    public SharedGameStatusChangeCacheEvictionTests()
    {
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _events.Add(Saved))
            .ReturnsAsync(1);
        _invalidator
            .Setup(i => i.EvictDetailAndListAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) => _events.Add(EvictedOne(id)))
            .Returns(Task.CompletedTask);
        _invalidator
            .Setup(i => i.EvictDetailsAndListAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((ids, _) => _events.Add(EvictedMany(ids)))
            .Returns(Task.CompletedTask);
    }

    private static string EvictedOne(Guid id) => $"evict:{id}";

    private static string EvictedMany(IEnumerable<Guid> ids) =>
        $"evict-many:{string.Join(",", ids.OrderBy(i => i))}";

    private static SharedGame Draft() => DetailCacheEvictionDoubles.NewGame();

    private static SharedGame Pending()
    {
        var game = Draft();
        game.SubmitForApproval(Admin);
        return game;
    }

    private static SharedGame Published()
    {
        var game = Pending();
        game.ApprovePublication(Admin);
        return game;
    }

    private SharedGame Stored(SharedGame game)
    {
        _repository
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        return game;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Un gioco: salva, poi evince
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Archive_EvictsTheGameAfterTheSave()
    {
        var game = Stored(Published());
        var handler = new ArchiveSharedGameCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object, NullLogger<ArchiveSharedGameCommandHandler>.Instance);

        await handler.Handle(new ArchiveSharedGameCommand(game.Id, Admin), TestContext.Current.CancellationToken);

        _events.Should().Equal(Saved, EvictedOne(game.Id));
    }

    [Fact]
    public async Task Delete_EvictsTheGameAfterTheSave()
    {
        var game = Stored(Published());
        var handler = new DeleteSharedGameCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object, NullLogger<DeleteSharedGameCommandHandler>.Instance);

        await handler.Handle(new DeleteSharedGameCommand(game.Id, Admin), TestContext.Current.CancellationToken);

        _events.Should().Equal(Saved, EvictedOne(game.Id));
    }

    [Fact]
    public async Task ApprovePublication_EvictsTheGameAfterTheSave()
    {
        var game = Stored(Pending());
        var handler = new ApproveSharedGamePublicationCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object,
            NullLogger<ApproveSharedGamePublicationCommandHandler>.Instance);

        await handler.Handle(new ApproveSharedGamePublicationCommand(game.Id, Admin), TestContext.Current.CancellationToken);

        _events.Should().Equal(Saved, EvictedOne(game.Id));
    }

    [Fact]
    public async Task QuickPublish_EvictsTheGameAfterTheSave()
    {
        var game = Stored(Draft());
        var handler = new QuickPublishSharedGameCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object,
            NullLogger<QuickPublishSharedGameCommandHandler>.Instance);

        await handler.Handle(new QuickPublishSharedGameCommand(game.Id, Admin), TestContext.Current.CancellationToken);

        _events.Should().Equal(Saved, EvictedOne(game.Id));
    }

    [Fact]
    public async Task RejectPublication_EvictsTheGameAfterTheSave()
    {
        var game = Stored(Pending());
        var handler = new RejectSharedGamePublicationCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object,
            NullLogger<RejectSharedGamePublicationCommandHandler>.Instance);

        await handler.Handle(
            new RejectSharedGamePublicationCommand(game.Id, Admin, "Mancano le immagini"),
            TestContext.Current.CancellationToken);

        _events.Should().Equal(Saved, EvictedOne(game.Id));
    }

    [Fact]
    public async Task SubmitForApproval_EvictsTheGameAfterTheSave()
    {
        var game = Stored(Draft());
        var handler = new SubmitSharedGameForApprovalCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object,
            NullLogger<SubmitSharedGameForApprovalCommandHandler>.Instance);

        await handler.Handle(new SubmitSharedGameForApprovalCommand(game.Id, Admin), TestContext.Current.CancellationToken);

        _events.Should().Equal(Saved, EvictedOne(game.Id));
    }

    [Fact]
    public async Task ApproveDeleteRequest_EvictsTheGameOfTheRequestAfterTheSave()
    {
        var game = Stored(Published());
        var request = SharedGameDeleteRequest.Create(game.Id, Guid.NewGuid(), "Duplicato");
        _deleteRequests
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        var handler = new ApproveDeleteRequestCommandHandler(
            _deleteRequests.Object, _repository.Object, _unitOfWork.Object, _invalidator.Object,
            NullLogger<ApproveDeleteRequestCommandHandler>.Instance);

        await handler.Handle(new ApproveDeleteRequestCommand(request.Id, Admin, null), TestContext.Current.CancellationToken);

        _events.Should().Equal(Saved, EvictedOne(game.Id));
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Più giochi: evince solo quelli che sono cambiati
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchApprove_EvictsOnlyTheGamesThatChanged_AfterTheSave()
    {
        var approved = Stored(Pending());
        var alsoApproved = Stored(Pending());
        var notPending = Stored(Draft());
        var missing = Guid.NewGuid();
        _repository
            .Setup(r => r.GetByIdAsync(missing, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);
        var handler = new BatchApproveGamesCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object,
            NullLogger<BatchApproveGamesCommandHandler>.Instance);

        var result = await handler.Handle(
            new BatchApproveGamesCommand([approved.Id, notPending.Id, missing, alsoApproved.Id], Admin, null),
            TestContext.Current.CancellationToken);

        result.SuccessCount.Should().Be(2);
        _events.Should().Equal(Saved, EvictedMany([approved.Id, alsoApproved.Id]));
    }

    [Fact]
    public async Task BatchReject_EvictsOnlyTheGamesThatChanged_AfterTheSave()
    {
        var rejected = Stored(Pending());
        var notPending = Stored(Draft());
        var handler = new BatchRejectGamesCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object,
            NullLogger<BatchRejectGamesCommandHandler>.Instance);

        var result = await handler.Handle(
            new BatchRejectGamesCommand([rejected.Id, notPending.Id], Admin, "Mancano le immagini"),
            TestContext.Current.CancellationToken);

        result.SuccessCount.Should().Be(1);
        _events.Should().Equal(Saved, EvictedMany([rejected.Id]));
    }

    [Fact]
    public async Task BatchApprove_WhenNoGameChanged_AsksToEvictNoGame()
    {
        var notPending = Stored(Draft());
        var handler = new BatchApproveGamesCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object,
            NullLogger<BatchApproveGamesCommandHandler>.Instance);

        await handler.Handle(
            new BatchApproveGamesCommand([notPending.Id], Admin, null),
            TestContext.Current.CancellationToken);

        // L'elenco vuoto è un no-op dell'invalidatore (SharedGameCacheInvalidatorTests): nemmeno la lista.
        _events.Should().Equal(Saved, EvictedMany([]));
    }

    [Fact]
    public async Task ExcelConfirm_EvictsOnlyTheDraftsItDeleted_AfterTheSaves()
    {
        var draft = Stored(Draft());
        var published = Stored(Published());
        var handler = new ConfirmExcelImportCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object, TimeProvider.System,
            NullLogger<ConfirmExcelImportCommandHandler>.Instance);

        var result = await handler.Handle(
            new ConfirmExcelImportCommand(
                new ConfirmExcelImportRequest([], [], [draft.Id, published.Id]), Admin),
            TestContext.Current.CancellationToken);

        result.Deleted.Should().Be(1);
        result.ProtectedSkipped.Should().Be(1);
        _events.Should().Equal(Saved, EvictedMany([draft.Id]));
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Se il comando fallisce, niente evizione
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Archive_OfAnAlreadyArchivedGame_EvictsNothing()
    {
        var game = Stored(Published());
        game.Archive(Admin);
        var handler = new ArchiveSharedGameCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object, NullLogger<ArchiveSharedGameCommandHandler>.Instance);

        var act = () => handler.Handle(new ArchiveSharedGameCommand(game.Id, Admin), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _events.Should().BeEmpty("a failed command saved nothing and has nothing to evict");
    }

    [Fact]
    public async Task Delete_OfAMissingGame_EvictsNothing()
    {
        var handler = new DeleteSharedGameCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object, NullLogger<DeleteSharedGameCommandHandler>.Instance);

        var act = () => handler.Handle(new DeleteSharedGameCommand(Guid.NewGuid(), Admin), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
        _events.Should().BeEmpty();
    }

    [Fact]
    public async Task QuickPublish_OfAGameThatIsNotADraft_EvictsNothing()
    {
        var game = Stored(Published());
        var handler = new QuickPublishSharedGameCommandHandler(
            _repository.Object, _unitOfWork.Object, _invalidator.Object,
            NullLogger<QuickPublishSharedGameCommandHandler>.Instance);

        var act = () => handler.Handle(new QuickPublishSharedGameCommand(game.Id, Admin), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _events.Should().BeEmpty();
    }
}
