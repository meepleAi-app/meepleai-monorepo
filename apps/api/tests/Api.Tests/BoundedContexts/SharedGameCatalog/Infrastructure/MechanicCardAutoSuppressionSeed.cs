using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Shared seed helpers for the #534 auto-suppression repository + handler integration tests: a published
/// (optionally suppressed) mechanic card with N negative / M positive feedback rows, plus a config setter.
/// </summary>
internal static class MechanicCardAutoSuppressionSeed
{
    public static async Task<Guid> CardWithFeedbackAsync(
        IServiceScope scope, Guid userId, int negatives, int positives, bool isSuppressed = false)
    {
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Catan",
            Description = "auto-suppression seed",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            Status = 1,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        });

        var analysisId = Guid.NewGuid();
        db.MechanicAnalyses.Add(new MechanicAnalysisEntity
        {
            Id = analysisId,
            SharedGameId = gameId,
            PdfDocumentId = Guid.NewGuid(),
            PromptVersion = "v1.0.0",
            Status = (int)Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicAnalysisStatus.Published,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            TotalTokensUsed = 0,
            EstimatedCostUsd = 0m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m
        });

        var cardId = Guid.NewGuid();
        db.Set<MechanicCardEntity>().Add(new MechanicCardEntity
        {
            Id = cardId,
            SharedGameId = gameId,
            OriginAnalysisId = analysisId,
            Origin = "ai_reviewed",
            Title = "Catan — Comprehension Card",
            Content = "{}",
            Version = 1,
            IsSuppressed = isSuppressed,
            SuppressedReason = isSuppressed ? "seed suppressed for exclusion test coverage" : null,
            SuppressedAt = isSuppressed ? DateTime.UtcNow : (DateTime?)null,
            SuppressedBy = isSuppressed ? userId : (Guid?)null,
            ErrorReportsCount = 0,
            FeedbackScore = null,
            PublishedAt = DateTime.UtcNow,
            PublishedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        for (var i = 0; i < negatives; i++)
        {
            db.MechanicCardFeedback.Add(Feedback(cardId, isPositive: false));
        }
        for (var i = 0; i < positives; i++)
        {
            db.MechanicCardFeedback.Add(Feedback(cardId, isPositive: true));
        }

        await db.SaveChangesAsync();
        return cardId;
    }

    public static async Task SetConfigAsync(
        MeepleAiDbContext db, string key, string value, string valueType, Guid createdByUserId)
    {
        var existing = await db.Set<SystemConfigurationEntity>()
            .FirstOrDefaultAsync(c => c.Key == key && c.Environment == "All");
        if (existing is null)
        {
            db.Set<SystemConfigurationEntity>().Add(new SystemConfigurationEntity
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = value,
                ValueType = valueType,
                Category = "MechanicCard",
                Environment = "All",
                IsActive = true,
                RequiresRestart = false,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedByUserId = createdByUserId // FK to users (Restrict) → must be a real user
            });
        }
        else
        {
            existing.Value = value;
            existing.ValueType = valueType;
        }
        await db.SaveChangesAsync();
    }

    private static MechanicCardFeedbackEntity Feedback(Guid cardId, bool isPositive) => new()
    {
        Id = Guid.NewGuid(),
        CardId = cardId,
        UserId = Guid.NewGuid(),
        ClaimId = Guid.NewGuid(),
        IsPositive = isPositive,
        ErrorType = isPositive ? null : "factual",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
