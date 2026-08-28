using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

internal class SeedPdfImageRegionsCommandHandler : ICommandHandler<SeedPdfImageRegionsCommand, int>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<SeedPdfImageRegionsCommandHandler> _logger;

    public SeedPdfImageRegionsCommandHandler(
        MeepleAiDbContext dbContext,
        ILogger<SeedPdfImageRegionsCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> Handle(SeedPdfImageRegionsCommand command, CancellationToken cancellationToken)
    {
        // Guard the FK: seeding regions for a non-existent PDF would violate
        // FK_pdf_image_regions_pdf_documents on SaveChanges → 500. Fail with a clean 404 instead
        // (repo convention #2568 — NotFoundException for missing entities, never InvalidOperation/500).
        var pdfExists = await _dbContext.PdfDocuments
            .AnyAsync(p => p.Id == command.PdfId, cancellationToken)
            .ConfigureAwait(false);
        if (!pdfExists)
        {
            throw new NotFoundException("PdfDocument", command.PdfId.ToString());
        }

        var regions = ImageRegionExtractor.FromHiResJson(
            command.HiResJson,
            command.MinAreaFraction ?? ImageRegionExtractor.DefaultMinAreaFraction);

        var existing = await _dbContext.PdfImageRegions
            .Where(r => r.PdfDocumentId == command.PdfId)
            // #3866: tracked on purpose — the write below only reaches the DB on a tracked entity (production defaults to NoTracking, PERF-06).
            .AsTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
        {
            _dbContext.PdfImageRegions.RemoveRange(existing);
        }

        foreach (var r in regions)
        {
            _dbContext.PdfImageRegions.Add(new PdfImageRegionEntity
            {
                PdfDocumentId = command.PdfId,
                PageNumber = r.Page,
                X = r.X,
                Y = r.Y,
                Width = r.Width,
                Height = r.Height,
                ElementType = r.ElementType
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Seeded {Count} image regions for PDF {PdfId}", regions.Count, command.PdfId);
        return regions.Count;
    }
}
