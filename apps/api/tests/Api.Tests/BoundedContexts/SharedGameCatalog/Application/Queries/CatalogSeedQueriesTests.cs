using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedQueriesTests
{
    private readonly Mock<ICatalogSeedDraftRepository> _repoMock = new();

    private static CatalogSeedDraftEntity Draft(Guid id, string status, int? bggId = 174430) => new()
    {
        Id = id,
        BggId = bggId,
        Status = status,
        CreatedByUserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    // ---------------- ListCatalogSeedsQuery ----------------

    [Fact]
    public async Task ListHandler_NoFilter_ReturnsAllPagedItems()
    {
        var d1 = Draft(Guid.NewGuid(), nameof(CatalogSeedStatus.Pending));
        var d2 = Draft(Guid.NewGuid(), nameof(CatalogSeedStatus.Fetched));
        _repoMock.Setup(r => r.CountAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _repoMock.Setup(r => r.ListAsync(null, 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { d1, d2 });

        var handler = new ListCatalogSeedsQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new ListCatalogSeedsQuery(StatusFilter: null),
            TestContext.Current.CancellationToken);

        result.Total.Should().Be(2);
        result.Skip.Should().Be(0);
        result.Take.Should().Be(50);
        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(d1.Id);
        result.Items[1].Status.Should().Be(nameof(CatalogSeedStatus.Fetched));
    }

    [Fact]
    public async Task ListHandler_WithStatusFilter_FiltersAndPaginates()
    {
        var d1 = Draft(Guid.NewGuid(), nameof(CatalogSeedStatus.Fetched));
        _repoMock.Setup(r => r.CountAsync("Fetched", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _repoMock.Setup(r => r.ListAsync("Fetched", 10, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { d1 });

        var handler = new ListCatalogSeedsQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new ListCatalogSeedsQuery("Fetched", Skip: 10, Take: 20),
            TestContext.Current.CancellationToken);

        result.Total.Should().Be(1);
        result.Skip.Should().Be(10);
        result.Take.Should().Be(20);
        result.Items.Should().ContainSingle().Which.Status.Should().Be(nameof(CatalogSeedStatus.Fetched));
    }

    [Fact]
    public async Task ListHandler_EmptyResult_ReturnsEmptyItems()
    {
        _repoMock.Setup(r => r.CountAsync("Rejected", It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _repoMock.Setup(r => r.ListAsync("Rejected", 0, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CatalogSeedDraftEntity>());

        var handler = new ListCatalogSeedsQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new ListCatalogSeedsQuery("Rejected"),
            TestContext.Current.CancellationToken);

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListHandler_NullQuery_Throws()
    {
        var handler = new ListCatalogSeedsQueryHandler(_repoMock.Object);
        var act = () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public void ListHandler_NullRepo_Throws()
    {
        var act = () => new ListCatalogSeedsQueryHandler(repository: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    // ---------------- GetCatalogSeedByIdQuery ----------------

    [Fact]
    public async Task GetByIdHandler_ExistingId_ReturnsDto()
    {
        var id = Guid.NewGuid();
        var entity = Draft(id, nameof(CatalogSeedStatus.Fetched), bggId: 13);
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var handler = new GetCatalogSeedByIdQueryHandler(_repoMock.Object);
        var result = await handler.Handle(new GetCatalogSeedByIdQuery(id), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.BggId.Should().Be(13);
        result.Status.Should().Be(nameof(CatalogSeedStatus.Fetched));
    }

    [Fact]
    public async Task GetByIdHandler_MissingId_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogSeedDraftEntity?)null);

        var handler = new GetCatalogSeedByIdQueryHandler(_repoMock.Object);
        var result = await handler.Handle(new GetCatalogSeedByIdQuery(id), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdHandler_NullQuery_Throws()
    {
        var handler = new GetCatalogSeedByIdQueryHandler(_repoMock.Object);
        var act = () => handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    // ---------------- Mapper ----------------

    [Fact]
    public void DtoMapper_MapsAllFields()
    {
        var entity = new CatalogSeedDraftEntity
        {
            Id = Guid.NewGuid(),
            BggId = 174430,
            WikidataQid = "Q42",
            SearchTermInput = "gloomhaven",
            Status = nameof(CatalogSeedStatus.Approved),
            ErrorMessage = "test",
            ResultingSharedGameId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc),
            FetchedAt = new DateTime(2026, 6, 4, 12, 5, 0, DateTimeKind.Utc),
            ApprovedAt = new DateTime(2026, 6, 4, 12, 10, 0, DateTimeKind.Utc),
            ApprovedByUserId = Guid.NewGuid(),
        };

        var dto = CatalogSeedDraftDtoMapper.ToDto(entity);

        dto.Id.Should().Be(entity.Id);
        dto.BggId.Should().Be(entity.BggId);
        dto.WikidataQid.Should().Be(entity.WikidataQid);
        dto.SearchTermInput.Should().Be(entity.SearchTermInput);
        dto.Status.Should().Be(entity.Status);
        dto.ErrorMessage.Should().Be(entity.ErrorMessage);
        dto.ResultingSharedGameId.Should().Be(entity.ResultingSharedGameId);
        dto.CreatedByUserId.Should().Be(entity.CreatedByUserId);
        dto.CreatedAt.Should().Be(entity.CreatedAt);
        dto.FetchedAt.Should().Be(entity.FetchedAt);
        dto.ApprovedAt.Should().Be(entity.ApprovedAt);
        dto.ApprovedByUserId.Should().Be(entity.ApprovedByUserId);
    }
}
