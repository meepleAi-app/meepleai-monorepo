using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// #4782 — the page rule of <see cref="GetDocumentsWithUnreliablePageNumbersQueryHandler"/> against a real PostgreSQL
/// database (Testcontainers). The unit tests use the in-memory provider, which accepts any LINQ shape; the handler's
/// value is its two EF queries (a <c>GROUP BY (document, page)</c> with a count, and a lifted <c>int? &gt; 1</c>), so the
/// translation is asserted here, end to end.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4782")]
public sealed class GetDocumentsWithUnreliablePageNumbersQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Guid _uploaderId = Guid.NewGuid();
    private string _dbName = null!;
    private MeepleAiDbContext _dbContext = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public GetDocumentsWithUnreliablePageNumbersQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_unreliable_pages_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            TestDbContextFactory.CreateMockMediator().Object,
            TestDbContextFactory.CreateMockEventCollector().Object);
        await _dbContext.Database.MigrateAsync(Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_dbName);
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();
    }

    [Fact(Timeout = 90_000)]
    public async Task Handle_RealPostgres_FlagsTheDocumentsWhosePagesAreNotBelieved_AndOnlyThose()
    {
        _dbContext.Users.Add(new UserEntity
        {
            Id = _uploaderId,
            Email = $"unreliable-pages-{_uploaderId:N}@test.local",
            DisplayName = "Unreliable Pages Test User",
            PasswordHash = "hashed",
            Role = "Admin",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = false,
        });

        // The flat path: forty passages that say page 1, plus one table written later with its real page.
        var flat = SeedPdf(pageCount: 12, chunkPages: [.. Enumerable.Repeat<int?>(1, 40), 7]);
        // A page of 0 is no page: the passages that say 1 are what is left, and they are all of them.
        var zeros = SeedPdf(pageCount: 12, chunkPages: [0, 0, 1, 1]);
        // Pages told apart.
        var withPages = SeedPdf(pageCount: 12, chunkPages: [1, 2, 2, 4, 5, 7, 9, 11]);
        // One page, and no count at all: nothing to confuse, nothing to say the numbers are wrong.
        var onePage = SeedPdf(pageCount: 1, chunkPages: [1, 1, 1]);
        var unknownCount = SeedPdf(pageCount: null, chunkPages: [1, 1, 1]);
        // Several pages, nothing that carries one.
        var noPages = SeedPdf(pageCount: 8, chunkPages: [null, null]);
        // Never asked about.
        var notAsked = SeedPdf(pageCount: 12, chunkPages: [1, 1, 1]);
        await _dbContext.SaveChangesAsync(Token);

        var sut = new GetDocumentsWithUnreliablePageNumbersQueryHandler(_dbContext);

        var result = await sut.Handle(
            new GetDocumentsWithUnreliablePageNumbersQuery([flat, zeros, withPages, onePage, unknownCount, noPages]),
            Token);

        result.Should().BeEquivalentTo(new[] { flat, zeros, noPages });
        result.Should().NotContain(notAsked);
    }

    private Guid SeedPdf(int? pageCount, int?[] chunkPages)
    {
        var id = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = $"{id:N}.pdf",
            FilePath = $"/tmp/{id:N}.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = _uploaderId,
            IsPublic = true,
            PageCount = pageCount,
        });
        for (var i = 0; i < chunkPages.Length; i++)
        {
            _dbContext.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = id,
                Content = $"passage {i}",
                ChunkIndex = i,
                CharacterCount = 9,
                PageNumber = chunkPages[i],
                ElementType = "NarrativeText",
                Level = 1,
            });
        }

        return id;
    }
}
