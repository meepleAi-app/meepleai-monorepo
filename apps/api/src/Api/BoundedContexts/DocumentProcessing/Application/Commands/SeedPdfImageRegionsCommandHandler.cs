using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
        var regions = ImageRegionExtractor.FromHiResJson(command.HiResJson);

        var existing = await _dbContext.PdfImageRegions
            .Where(r => r.PdfDocumentId == command.PdfId)
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
