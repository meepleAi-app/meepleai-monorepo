using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ReuploadBggCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Tests.Constants;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.ReuploadBggCover;

/// <summary>
/// #3590 Slice B — il re-upload server-to-server della cover BGG per un gioco già a catalogo
/// (ADR-059 §2). Path distinto dalla cover manuale a URL libero, dove gli host geekdo restano
/// banditi da <c>BggHostDenyList</c>: questi test verificano anche che quella deny-list non sia
/// stata toccata.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ReuploadBggCoverCommandHandlerTests
{
    private const int BggId = 13;
    private const string RemoteImage = "https://cf.geekdo-images.com/x/original/cover.jpg";
    private const string R2Key = "bgg-covers/13/cover";

    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly Mock<ISharedGameRepository> _repository = new();
    private readonly Mock<IBggApiService> _bggApi = new();
    private readonly Mock<IBggCoverDownloader> _downloader = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IHybridCacheService> _cache = new();
    private readonly Mock<ICacheInvalidationRetryPolicy> _cacheRetryPolicy = new();

    public ReuploadBggCoverCommandHandlerTests()
    {
        _cacheRetryPolicy
            .Setup(p => p.ExecuteAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, ValueTask> op, string _, CancellationToken ct) => op(ct).AsTask());
        _cache
            .Setup(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private ReuploadBggCoverCommandHandler CreateHandler() => new(
        _repository.Object,
        _bggApi.Object,
        _downloader.Object,
        _unitOfWork.Object,
        _cache.Object,
        _cacheRetryPolicy.Object,
        NullLogger<ReuploadBggCoverCommandHandler>.Instance);

    // Il BggId va passato alla factory: AssignBggId è ammesso solo negli stati Skeleton/Failed,
    // mentre Create() produce un aggregato Complete.
    private static SharedGame NewGame(int? bggId = BggId) => SharedGame.Create(
        "Catan", 1995, "desc", 3, 4, 90, 10, 2.5m, 7.8m,
        "https://example.com/c.jpg", "https://example.com/c-thumb.jpg", null, AdminId, bggId);

    private static BggGameDetailsDto Details(string? imageUrl) => new(
        BggId, "Catan", "desc", 1995, 3, 4, 90, 60, 120, 10,
        7.8, 7.5, 1000, 2.5, "https://example.com/thumb.jpg", imageUrl,
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public async Task Handle_HappyPath_PersistsR2KeyOnAggregate()
    {
        var game = NewGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _bggApi.Setup(b => b.GetGameDetailsAsync(BggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Details(RemoteImage));
        _downloader.Setup(d => d.DownloadAndUploadAsync(BggId, RemoteImage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(R2Key);

        var result = await CreateHandler().Handle(new ReuploadBggCoverCommand(game.Id, AdminId), CancellationToken.None);

        result.R2Key.Should().Be(R2Key);
        game.BggCoverR2Key.Should().Be(R2Key, "la chiave va sull'aggregato, non sull'entità EF");
        _repository.Verify(r => r.Update(game), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GameWithoutBggId_Throws409_AndNeverFetches()
    {
        var game = NewGame(bggId: null);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var act = () => CreateHandler().Handle(new ReuploadBggCoverCommand(game.Id, AdminId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _bggApi.Verify(b => b.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _downloader.VerifyNoOtherCalls();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownGame_Throws404()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var act = () => CreateHandler().Handle(new ReuploadBggCoverCommand(Guid.NewGuid(), AdminId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_BggExposesNoImage_Throws409()
    {
        var game = NewGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _bggApi.Setup(b => b.GetGameDetailsAsync(BggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Details(imageUrl: null));

        var act = () => CreateHandler().Handle(new ReuploadBggCoverCommand(game.Id, AdminId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _downloader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_DownloaderReturnsNull_Throws409_AndWritesNothing()
    {
        // Il downloader logga e ritorna null invece di lanciare: senza questa traduzione l'admin
        // vedrebbe un 200 con nessun cambiamento.
        var game = NewGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _bggApi.Setup(b => b.GetGameDetailsAsync(BggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Details(RemoteImage));
        _downloader.Setup(d => d.DownloadAndUploadAsync(BggId, RemoteImage, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var act = () => CreateHandler().Handle(new ReuploadBggCoverCommand(game.Id, AdminId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        game.BggCoverR2Key.Should().BeNull();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ManualCoverPath_StillBansBggHosts()
    {
        // Non-regressione ADR-059 §5: questo carve-out NON allenta la deny-list sul campo a URL
        // libero. Se questo test diventa rosso, il carve-out è stato implementato nel modo sbagliato.
        Api.SharedKernel.Infrastructure.Http.BggHostDenyList.IsBanned(RemoteImage)
            .Should().BeTrue("il path manuale a URL arbitrario deve continuare a rifiutare geekdo");
    }
}
