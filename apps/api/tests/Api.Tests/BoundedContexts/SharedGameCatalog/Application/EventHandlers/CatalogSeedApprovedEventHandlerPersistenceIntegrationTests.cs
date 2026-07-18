using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Issue #3153 — end-to-end persistence test for the draft→game promotion. Drives the
/// REAL CatalogSeedApprovedEventHandler with a real SharedGameRepository + UnitOfWork +
/// DbContext (Testcontainers Postgres); only the drafts repository is a Moq input carrier.
/// A unit test with a mocked repo (CatalogSeedApprovedEventHandlerTests) cannot catch the
/// #3147 class of bug where designers/publishers are silently dropped on persist — this
/// exercises provenance-read → EnrichFromProvenance → AddAsync → get-or-create → join rows.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedApprovedEventHandlerPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private SharedGameRepository _games = null!;
    private UnitOfWork _uow = null!;

    public CatalogSeedApprovedEventHandlerPersistenceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"seedapproved_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        var eventCollector = new Mock<IDomainEventCollector>();
        eventCollector.Setup(x => x.GetAndClearEvents()).Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, new Mock<IMediator>().Object, eventCollector.Object);
        await _dbContext.Database.MigrateAsync();

        _games = new SharedGameRepository(_dbContext, eventCollector.Object);
        _uow = new UnitOfWork(_dbContext);
    }

    public async ValueTask DisposeAsync() => await _dbContext.DisposeAsync();

    [Fact]
    public async Task Handle_WikidataDraftWithDesignersPublishers_PersistsJoinRows()
    {
        // Arrange — a pure-Wikidata draft whose provenance carries designer + publisher names.
        var qidUrl = "https://www.wikidata.org/wiki/Q17271";
        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal)
        {
            ["title"] = new("wikidata", qidUrl, "labels.en", DateTime.UtcNow, "Catan"),
            ["designers"] = new("wikidata", qidUrl, "P178", DateTime.UtcNow, new List<string> { "Klaus Teuber" }),
            ["publishers"] = new("wikidata", qidUrl, "P123", DateTime.UtcNow, new List<string> { "Kosmos" }),
        };
        var approvedBy = Guid.NewGuid();
        var draft = new CatalogSeedDraftEntity
        {
            Id = Guid.NewGuid(),
            BggId = null,
            Status = "Approved",
            ProvenanceJson = new CatalogSeedProvenance(fields).ToJson(),
            ResultingSharedGameId = Guid.NewGuid(), // M4.4 placeholder
            ApprovedAt = DateTime.UtcNow,
            ApprovedByUserId = approvedBy,
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };

        var drafts = new Mock<ICatalogSeedDraftRepository>();
        drafts.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);

        var handler = new CatalogSeedApprovedEventHandler(
            drafts.Object, _games, _uow, TimeProvider.System,
            NullLogger<CatalogSeedApprovedEventHandler>.Instance);

        // Act — run the real promotion pipeline. (The draft is a Moq-returned, untracked
        // entity, so its ResultingSharedGameId FK rewrite is not asserted here — that is
        // covered by the unit tests; this test targets the SharedGame M:N persistence.)
        await handler.Handle(
            new CatalogSeedApprovedEvent(draft.Id, draft.ResultingSharedGameId!.Value, approvedBy),
            default);
        _dbContext.ChangeTracker.Clear();

        // Assert — the materialised SharedGame has the M:N join rows persisted in the DB.
        var game = await _dbContext.SharedGames
            .Include(g => g.Designers)
            .Include(g => g.Publishers)
            .AsSplitQuery()
            .SingleAsync(g => g.Title == "Catan");
        game.Designers.Select(d => d.Name).Should().ContainSingle().Which.Should().Be("Klaus Teuber");
        game.Publishers.Select(p => p.Name).Should().ContainSingle().Which.Should().Be("Kosmos");

        (await _dbContext.GameDesigners.CountAsync(d => d.Name == "Klaus Teuber")).Should().Be(1);
        (await _dbContext.GamePublishers.CountAsync(p => p.Name == "Kosmos")).Should().Be(1);
    }
}
