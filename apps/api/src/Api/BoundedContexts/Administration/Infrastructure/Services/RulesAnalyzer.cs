using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.Infrastructure;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Analyzes user's saved rulebook PDFs to detect unreviewed rules (7+ days).
/// </summary>
internal sealed class RulesAnalyzer : IRulesAnalyzer
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<RulesAnalyzer> _logger;
    private const int ReminderThresholdDays = 7;
    private const int MaxInsights = 3;

    public RulesAnalyzer(
        MeepleAiDbContext dbContext,
        ILogger<RulesAnalyzer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<AIInsight>> AnalyzeRulebooksAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thresholdDate = DateTime.UtcNow.AddDays(-ReminderThresholdDays);

            // Find PDFs uploaded by user and not accessed recently
            // Note: Requires LastAccessedAt tracking on PdfDocument (may need to add)
            var unreviewedPdfs = await _dbContext.Set<PdfDocument>()
                .AsNoTracking()
                .Where(p => p.UploadedByUserId == userId)
                .Where(p => p.UploadedAt < thresholdDate) // Uploaded 7+ days ago
                // FUTURE: Add LastAccessedAt filter when available
                .OrderBy(p => p.UploadedAt)
                .Take(MaxInsights)
                .Select(p => new { p.Id, p.FileName })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (unreviewedPdfs.Count == 0)
                return new List<AIInsight>();

            var insights = new List<AIInsight>();

            if (unreviewedPdfs.Count > 1)
            {
                insights.Add(AIInsight.Create(
                    type: InsightType.RulesReminder,
                    title: $"{unreviewedPdfs.Count} regolamenti da rivedere",
                    description: $"Hai {unreviewedPdfs.Count} regolamenti salvati non consultati di recente. Rinfrescati la memoria!",
                    actionLabel: "Vedi Regolamenti →",
                    actionUrl: "/library/pdfs",
                    priority: 5
                ));
            }
            else
            {
                var pdf = unreviewedPdfs[0];
                var fileName = pdf.FileName?.Value != null
                    ? Path.GetFileNameWithoutExtension(pdf.FileName.Value)
                    : "Regolamento";
                insights.Add(AIInsight.Create(
                    type: InsightType.RulesReminder,
                    title: $"Rivedi: {fileName}",
                    description: "Non hai consultato questo regolamento di recente. Un ripasso veloce?",
                    actionLabel: "Apri PDF →",
                    actionUrl: $"/pdfs/{pdf.Id}",
                    priority: 4
                ));
            }

            _logger.LogInformation(
                "RulesAnalyzer generated {InsightCount} insights for user {UserId}",
                insights.Count,
                userId);

            return insights;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error analyzing rulebooks for user {UserId}",
                userId);
            return new List<AIInsight>(); // Graceful degradation
        }
    }
}
