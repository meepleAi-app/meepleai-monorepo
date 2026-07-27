using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.IntegrationEvents;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Regression for the <see cref="VectorDocumentReadyStateHandler"/> NoTracking silent no-op.
///
/// The compensating handler loads the PdfDocument via a bare
/// <c>_dbContext.PdfDocuments.FirstOrDefaultAsync(...)</c> (no <c>.AsTracking()</c>). The DbContext
/// defaults to <c>QueryTrackingBehavior.NoTracking</c> (PERF-06,
/// <c>InfrastructureServiceExtensions.cs</c>), so the returned entity is detached — the whole point
/// of the handler (set <c>ProcessingState=Ready</c> + stamp <c>IndexerVersion</c> then
/// <c>SaveChangesAsync</c>) is a guaranteed no-op and the PDF is never advanced to Ready after vector
/// indexing completes.
///
/// The existing Category=Unit <c>VectorDocumentReadyStateHandlerTests</c> misses this because
/// <c>TestDbContextFactory</c> uses the EF Core in-memory provider with default track-all semantics.
/// This test runs against real Postgres via Testcontainers with the production NoTracking default, so
/// the mutation must actually persist.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class VectorDocumentReadyStateNoTrackingPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"vecdoc_ready_notracking_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public VectorDocumentReadyStateNoTrackingPersistenceIntegrationTests(SharedTestcontainersFixture fixture)
        => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(conn);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "VectorDocumentReadyStateHandler persists ProcessingState=Ready + IndexerVersion under NoTracking (real DB)")]
    public async Task Handle_UnderProductionNoTrackingDefault_PersistsReadyStateAndIndexerVersion()
    {
        var pdfId = Guid.NewGuid();

        // Arrange — seed a user (FK) and a PDF stuck in the Indexing state, via one scope.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var userId = Guid.NewGuid();
            db.Users.Add(new UserEntity
            {
                Id = userId,
                Email = $"vecdoc-ready-{userId:N}@meepleai.test",
                DisplayName = "VecDoc Ready Test",
                Role = "user",
                CreatedAt = DateTime.UtcNow,
            });
            db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                FileName = "rules.pdf",
                FilePath = $"pdfs/{pdfId:N}/rules.pdf",
                FileSizeBytes = 1024,
                ContentType = "application/pdf",
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow,
                ProcessingState = nameof(PdfProcessingState.Indexing),
                IndexerVersion = null,
                Language = "en",
            });
            await db.SaveChangesAsync();
        }

        // Act — invoke the handler against a fresh NoTracking DbContext resolved from the container.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var handler = new VectorDocumentReadyStateHandler(
                scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
                NullLogger<VectorDocumentReadyStateHandler>.Instance);

            var evt = new VectorDocumentReadyIntegrationEvent
            {
                DocumentId = Guid.NewGuid(),
                GameId = Guid.NewGuid(),
                ChunkCount = 42,
                PdfDocumentId = pdfId,
                UploadedByUserId = Guid.NewGuid(),
                FileName = "rules.pdf",
                CurrentProcessingState = nameof(PdfProcessingState.Indexing),
            };

            await handler.Handle(evt, CancellationToken.None);
        }

        // Assert — fresh NoTracking read; the compensating writes must have actually persisted.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.PdfDocuments.AsNoTracking().FirstAsync(x => x.Id == pdfId);

            entity.ProcessingState.Should().Be(
                nameof(PdfProcessingState.Ready),
                "VectorDocumentReadyStateHandler must load PdfDocuments .AsTracking() so the compensating "
                + "ProcessingState=Ready write is persisted under the NoTracking default (silent no-op otherwise)");
            entity.IndexerVersion.Should().Be(
                IndexerVersionRegistry.Current.Version,
                "the compensating Ready transition must also stamp the current IndexerVersion (#3269)");
        }
    }
}
