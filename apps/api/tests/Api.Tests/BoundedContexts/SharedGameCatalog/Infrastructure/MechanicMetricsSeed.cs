using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Shared seed helpers for the #532 metrics-dashboard query tests: a shared game + mechanic analyses
/// with explicit status / cost / review timestamps / rejection reason.
/// </summary>
internal static class MechanicMetricsSeed
{
    public static async Task<Guid> GameAsync(IServiceScope scope, Guid createdBy, string title = "Catan")
    {
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = title,
            Description = "metrics seed",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            Status = 1,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return gameId;
    }

    public static async Task<Guid> AnalysisAsync(
        IServiceScope scope,
        Guid gameId,
        Guid createdBy,
        int status,
        decimal costUsd,
        DateTime createdAt,
        DateTime? reviewedAt = null,
        Guid? reviewedBy = null,
        string? rejectionReason = null)
    {
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var id = Guid.NewGuid();
        db.MechanicAnalyses.Add(new MechanicAnalysisEntity
        {
            Id = id,
            SharedGameId = gameId,
            PdfDocumentId = Guid.NewGuid(),
            PromptVersion = "v1.0.0",
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt,
            RejectionReason = rejectionReason,
            TotalTokensUsed = 0,
            EstimatedCostUsd = costUsd,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m
        });
        await db.SaveChangesAsync();
        return id;
    }
}
