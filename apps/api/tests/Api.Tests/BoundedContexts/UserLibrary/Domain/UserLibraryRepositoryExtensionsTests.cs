using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain;

/// <summary>
/// #4187. Chi voleva «tutta la libreria» chiedeva una pagina grande (1000 per l'anteprima di
/// downgrade, 100 per la vista condivisa) e il repository ne serviva al massimo 100, in
/// silenzio. L'estensione scorre le pagine fino al totale.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class UserLibraryRepositoryExtensionsTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    /// <summary>Un repository che si comporta come quello vero: pagine troncate a 100.</summary>
    internal static Mock<IUserLibraryRepository> PagedRepository(int count, int? declaredTotal = null)
    {
        var all = Enumerable.Range(0, count)
            .Select(_ => new UserLibraryEntry(Guid.NewGuid(), UserId, Guid.NewGuid()))
            .ToList();
        var mock = new Mock<IUserLibraryRepository>();
        mock.Setup(r => r.GetUserLibraryPaginatedAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string[]?>(),
                It.IsAny<bool?>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string? _, bool? _, string[]? _, bool? _, string? _, bool _, int page, int pageSize,
                CancellationToken _) =>
            {
                var size = UserLibraryPaging.NormalizePageSize(pageSize);
                var served = all.Skip((UserLibraryPaging.NormalizePage(page) - 1) * size).Take(size).ToList();
                return (served, declaredTotal ?? all.Count);
            });
        return mock;
    }

    [Fact]
    public async Task GetAll_ReadsEveryPage_UntilTheTotal()
    {
        var repo = PagedRepository(250);

        var (entries, total) = await repo.Object.GetAllUserLibraryEntriesAsync(
            UserId, sortBy: "addedAt", descending: true, TestContext.Current.CancellationToken);

        entries.Should().HaveCount(250);
        total.Should().Be(250);
        repo.Verify(r => r.GetUserLibraryPaginatedAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string[]?>(),
            It.IsAny<bool?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<int>(), UserLibraryPaging.MaxPageSize,
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GetAll_EmptyLibrary_AsksOnce()
    {
        var repo = PagedRepository(0);

        var (entries, _) = await repo.Object.GetAllUserLibraryEntriesAsync(
            UserId, sortBy: "addedAt", descending: true, TestContext.Current.CancellationToken);

        entries.Should().BeEmpty();
        repo.Verify(r => r.GetUserLibraryPaginatedAsync(
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string[]?>(),
            It.IsAny<bool?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_TotalLargerThanWhatExists_StopsAtTheFirstEmptyPage()
    {
        // Il totale e le pagine si leggono in momenti diversi: se una voce sparisce nel mezzo il
        // totale resta piu' alto di cio' che si trova. Il ciclo non deve girare per sempre.
        var repo = PagedRepository(120, declaredTotal: 500);

        var (entries, _) = await repo.Object.GetAllUserLibraryEntriesAsync(
            UserId, sortBy: "addedAt", descending: true, TestContext.Current.CancellationToken);

        entries.Should().HaveCount(120);
    }
}
