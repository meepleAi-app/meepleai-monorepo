using System.Text;
using System.Text.Json;
using Api.Constants;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// ADMIN-01: Enhanced prompt template service with database-driven prompts and Redis caching
/// Combines AI-07.1 few-shot learning with admin-configurable prompt management
/// Architecture: Redis cache-first → PostgreSQL fallback → Configuration fallback
/// </summary>
public class PromptTemplateService : IPromptTemplateService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly RagPromptsConfiguration _config;
    private readonly ILogger<PromptTemplateService> _logger;
    private readonly TimeProvider _timeProvider;

    // ADMIN-01: Cache configuration
    private const string CacheKeyPrefix = "prompt:";
    private const int DefaultCacheTtlSeconds = TimeConstants.PromptTemplateCacheTtlSeconds;

    // Fallback hardcoded prompts for backward compatibility
    private static readonly PromptTemplate FallbackTemplate = new()
    {
        SystemPrompt = @"You are a board game rules assistant. Your job is to answer questions about board game rules based ONLY on the provided context from the rulebook.

CRITICAL INSTRUCTIONS:
- If the answer to the question is clearly found in the provided context, answer it concisely and accurately.
- If the answer is NOT in the provided context or you're uncertain, respond with EXACTLY: ""Not specified""
- Do NOT make assumptions or use external knowledge about the game.
- Do NOT hallucinate or invent information.
- Keep your answers brief and to the point (2-3 sentences maximum).
- Reference page numbers when relevant.",
        UserPromptTemplate = @"CONTEXT FROM RULEBOOK:
{context}

QUESTION:
{query}

ANSWER:",
        FewShotExamples = new List<FewShotExample>()
    };

    public PromptTemplateService(
        MeepleAiDbContext dbContext,
        IConnectionMultiplexer redis,
        IOptions<RagPromptsConfiguration> config,
        ILogger<PromptTemplateService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _config = config?.Value ?? new RagPromptsConfiguration();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the appropriate prompt template for a game and question type
    /// Priority: Game-specific > Question-type-specific > Default > Fallback
    /// </summary>
    public Task<PromptTemplate> GetTemplateAsync(Guid? gameId, QuestionType questionType)
    {
        try
        {
            // Priority 1: Game-specific template
            if (gameId.HasValue && _config.GameTemplates.TryGetValue(gameId.Value.ToString(), out var gameTemplates))
            {
                var questionTypeKey = questionType.ToString();
                if (gameTemplates.TryGetValue(questionTypeKey, out var gameTemplate))
                {
                    _logger.LogDebug(
                        "Using game-specific template for game {GameId}, question type {QuestionType}",
                        gameId, questionType);
                    return Task.FromResult(ConvertToPromptTemplate(gameTemplate, gameId, questionType));
                }
            }

            // Priority 2: Question-type-specific template
            var questionTypeKeyStr = questionType.ToString();
            if (_config.Templates.TryGetValue(questionTypeKeyStr, out var typeTemplate))
            {
                _logger.LogDebug(
                    "Using question-type-specific template for type {QuestionType}",
                    questionType);
                return Task.FromResult(ConvertToPromptTemplate(typeTemplate, null, questionType));
            }

            // Priority 3: Default template
            if (_config.Default != null)
            {
                _logger.LogDebug("Using default template");
                return Task.FromResult(ConvertToPromptTemplate(_config.Default, null, QuestionType.General));
            }

            // Priority 4: Fallback hardcoded template
            _logger.LogWarning(
                "No configuration found for game {GameId}, question type {QuestionType}. Using fallback template.",
                gameId, questionType);
            return Task.FromResult(FallbackTemplate);
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error loading prompt template for game {GameId}, question type {QuestionType}. Using fallback.",
                gameId, questionType);
            return Task.FromResult(FallbackTemplate);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Database error loading prompt template for game {GameId}, question type {QuestionType}. Using fallback.",
                gameId, questionType);
            return Task.FromResult(FallbackTemplate);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "JSON parsing error loading prompt template for game {GameId}, question type {QuestionType}. Using fallback.",
                gameId, questionType);
            return Task.FromResult(FallbackTemplate);
        }
    }

    /// <summary>
    /// Renders system prompt with few-shot examples in LangChain format
    /// </summary>
    public string RenderSystemPrompt(PromptTemplate template)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (template.FewShotExamples == null || template.FewShotExamples.Count == 0)
        {
            return template.SystemPrompt;
        }

        var sb = new StringBuilder();
        sb.AppendLine(template.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine("EXAMPLES:");

        foreach (var example in template.FewShotExamples)
        {
            sb.AppendLine($"Q: {example.Question}");
            sb.AppendLine($"A: {example.Answer}");
            sb.AppendLine();
        }

        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("- Be precise and cite page numbers when available");
        sb.AppendLine("- If the answer is not in the provided context, respond with \"Not specified\"");
        sb.AppendLine("- State confidence level if uncertain");
        sb.AppendLine("- For ambiguous questions, ask for clarification");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Renders user prompt with context and query placeholders replaced
    /// </summary>
    public string RenderUserPrompt(PromptTemplate template, string context, string query)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (string.IsNullOrEmpty(template.UserPromptTemplate))
        {
            throw new ArgumentException("UserPromptTemplate cannot be null or empty", nameof(template));
        }

        return template.UserPromptTemplate
            .Replace("{context}", context ?? string.Empty)
            .Replace("{query}", query ?? string.Empty);
    }

    /// <summary>
    /// Classifies question based on keyword matching
    /// Keywords are case-insensitive and checked in priority order
    /// </summary>
    public QuestionType ClassifyQuestion(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return QuestionType.General;
        }

        var queryLower = query.ToLowerInvariant();

        // Check keywords in priority order (most specific first)

        // EdgeCases keywords (check first as they're most specific)
        if (ContainsAny(queryLower, new[]
            { "en passant", "castling", "castle", "stalemate", "special", "exception", "edge case" }))
        {
            return QuestionType.EdgeCases;
        }

        // WinningConditions keywords
        if (ContainsAny(queryLower, new[]
            { "win", "victory", "lose", "checkmate", "three in a row", "winning", "defeat" }))
        {
            return QuestionType.WinningConditions;
        }

        // Setup keywords
        if (ContainsAny(queryLower, new[]
            { "setup", "set up", "start", "begin", "prepare", "place pieces", "initial", "arrange" }))
        {
            return QuestionType.Setup;
        }

        // Gameplay keywords (most common, check last)
        if (ContainsAny(queryLower, new[]
            { "move", "turn", "action", "can i", "allowed", "play", "rules", "how does" }))
        {
            return QuestionType.Gameplay;
        }

        // Default to General if no keywords match
        return QuestionType.General;
    }

    #region Private Helper Methods

    private bool ContainsAny(string text, string[] keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private PromptTemplate ConvertToPromptTemplate(
        PromptTemplateConfig config,
        Guid? gameId,
        QuestionType questionType)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var fewShotExamples = config.FewShotExamples?
            .Select(e => new FewShotExample
            {
                Question = e.Question,
                Answer = e.Answer,
                Category = e.Category
            })
            .ToList() ?? new List<FewShotExample>();

        return new PromptTemplate
        {
            SystemPrompt = config.SystemPrompt,
            UserPromptTemplate = config.UserPromptTemplate,
            FewShotExamples = fewShotExamples,
            GameId = gameId,
            QuestionType = questionType
        };
    }

    #endregion

    #region ADMIN-01: Database-Driven Prompt Management with Redis Caching

    /// <summary>
    /// ADMIN-01: Gets active prompt from cache-first architecture
    /// Flow: Redis cache → PostgreSQL → Configuration fallback
    /// </summary>
    public async Task<string?> GetActivePromptAsync(string templateName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));
        }

        var cacheKey = $"{CacheKeyPrefix}{templateName}:active";

        try
        {
            // Step 1: Try Redis cache first (< 10ms target)
            var db = _redis.GetDatabase();
            var cachedPrompt = await db.StringGetAsync(cacheKey);

            if (cachedPrompt.HasValue)
            {
                _logger.LogDebug("Cache HIT for prompt template {TemplateName}", templateName);
                return cachedPrompt.ToString();
            }

            _logger.LogDebug("Cache MISS for prompt template {TemplateName}, querying database", templateName);

            // Step 2: Query PostgreSQL for active version
            var activeVersion = await _dbContext.Set<PromptVersionEntity>()
                .AsNoTracking() // PERF-06: Read-only query optimization
                .Include(v => v.Template)
                .Where(v => v.Template.Name == templateName && v.IsActive)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync(ct);

            if (activeVersion != null)
            {
                // Step 3: Populate cache with TTL (FIX: Wait for acknowledgment instead of fire-and-forget)
                var cacheSet = await db.StringSetAsync(
                    cacheKey,
                    activeVersion.Content,
                    TimeSpan.FromSeconds(DefaultCacheTtlSeconds));

                if (cacheSet)
                {
                    _logger.LogInformation(
                        "Loaded active prompt {TemplateName} version {Version} from database and cached successfully",
                        templateName, activeVersion.VersionNumber);
                }
                else
                {
                    _logger.LogWarning(
                        "Cache write failed for prompt {TemplateName}, will retry on next cache miss",
                        templateName);
                }

                return activeVersion.Content;
            }

            // Step 4: Fallback to configuration (backward compatibility)
            _logger.LogWarning(
                "No active database prompt found for {TemplateName}, falling back to configuration",
                templateName);

            return null; // Caller should handle fallback logic
        }
        catch (RedisException ex)
        {
            // Redis failure: fallback to database (degraded mode)
            _logger.LogWarning(ex, "Redis unavailable for prompt {TemplateName}, using database directly", templateName);

            var activeVersion = await _dbContext.Set<PromptVersionEntity>()
                .AsNoTracking()
                .Include(v => v.Template)
                .Where(v => v.Template.Name == templateName && v.IsActive)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync(ct);

            return activeVersion?.Content;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error retrieving prompt template {TemplateName}", templateName);
            throw new InvalidOperationException($"Failed to retrieve prompt template '{templateName}' due to database error", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for prompt template {TemplateName}", templateName);
            throw new InvalidOperationException($"Failed to parse prompt template '{templateName}' content", ex);
        }
    }

    /// <summary>
    /// ADMIN-01: Activates a prompt version with transaction safety and cache invalidation
    /// Critical: Ensures only ONE active version per template + atomic cache invalidation
    /// </summary>
    public async Task<bool> ActivateVersionAsync(Guid templateId, Guid versionId, Guid activatedByUserId, CancellationToken ct = default)
    {
        if (templateId == Guid.Empty)
        {
            throw new ArgumentException("Template ID cannot be empty", nameof(templateId));
        }
        if (versionId == Guid.Empty)
        {
            throw new ArgumentException("Version ID cannot be empty", nameof(versionId));
        }
        if (activatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Activated by user ID cannot be empty", nameof(activatedByUserId));
        }

        // FIX: Load user BEFORE transaction to reduce transaction scope and lock duration
        // Note: NOT using AsNoTracking because user entity is reused in audit log (needs tracking)
        var changedByUser = await _dbContext.Set<UserEntity>()
            .FirstOrDefaultAsync(u => u.Id == activatedByUserId, ct).ConfigureAwait(false);

        if (changedByUser == null)
        {
            _logger.LogWarning("User {UserId} not found for activation", activatedByUserId);
            throw new InvalidOperationException($"User {activatedByUserId} not found");
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            // Step 1: Verify version exists and belongs to template
            var versionToActivate = await _dbContext.Set<PromptVersionEntity>()
                .Include(v => v.Template)
                .FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == templateId, ct)
                .ConfigureAwait(false);

            if (versionToActivate == null)
            {
                _logger.LogWarning(
                    "Version {VersionId} not found for template {TemplateId}",
                    versionId, templateId);
                return false;
            }

            // Step 2: Deactivate all other versions for this template (ensure single active)
            var otherVersions = await _dbContext.Set<PromptVersionEntity>()
                .Where(v => v.TemplateId == templateId && v.Id != versionId && v.IsActive)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var version in otherVersions)
            {
                version.IsActive = false;
                _logger.LogDebug(
                    "Deactivating version {VersionId} (v{VersionNumber})",
                    version.Id, version.VersionNumber);
            }

            // Step 3: Activate the target version
            versionToActivate.IsActive = true;

            // Step 4: Create audit log entry (navigation properties set via FK, not entity references)
            // Note: changedByUser was fetched with AsNoTracking(), so we use only the FK
            var auditLog = new PromptAuditLogEntity
            {
                Id = Guid.NewGuid(),
                TemplateId = templateId,
                VersionId = versionId,
                Action = "version_activated",
                ChangedByUserId = activatedByUserId,
                ChangedAt = _timeProvider.GetUtcNow().UtcDateTime,
                Details = $"Activated version {versionToActivate.VersionNumber}",
                Template = versionToActivate.Template,
                ChangedBy = changedByUser // EF Core will handle FK relationship
            };

            _dbContext.Set<PromptAuditLogEntity>().Add(auditLog);

            // Step 5: Save changes (within transaction)
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            // Step 6: Commit transaction (atomic) - FIX: Commit BEFORE cache invalidation
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Transaction committed for template {TemplateName} version {VersionNumber}",
                versionToActivate.Template.Name, versionToActivate.VersionNumber);

            // Step 7: Invalidate cache AFTER transaction commit (prevents stale cache)
            // This ensures cache doesn't contain old data while transaction uncommitted
            var cacheKey = $"{CacheKeyPrefix}{versionToActivate.Template.Name}:active";
            var db = _redis.GetDatabase();
            var deleted = await db.KeyDeleteAsync(cacheKey).ConfigureAwait(false);

            _logger.LogInformation(
                "Cache invalidation for template {TemplateName} after activation: {Result}",
                versionToActivate.Template.Name, deleted ? "SUCCESS" : "KEY_NOT_FOUND");

            return true;
        }
        catch (DbUpdateException ex)
        {
            // Rollback on any error
            await transaction.RollbackAsync(ct);
            _logger.LogError(
                ex,
                "Database error activating version {VersionId} for template {TemplateId}",
                versionId, templateId);
            throw new InvalidOperationException($"Failed to activate version {versionId} due to database error", ex);
        }
        catch (RedisException ex)
        {
            // Transaction already committed, cache invalidation failed (degraded mode)
            _logger.LogError(
                ex,
                "Redis error during cache invalidation for version {VersionId}, template {TemplateId}. Activation succeeded but cache may be stale.",
                versionId, templateId);
            // Don't rollback - transaction already committed
            return true;
        }
    }

    /// <summary>
    /// ADMIN-01: Invalidates cache for a specific template
    /// Used for manual cache refresh or debugging
    /// </summary>
    public async Task InvalidateCacheAsync(string templateName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));
        }

        var cacheKey = $"{CacheKeyPrefix}{templateName}:active";
        var db = _redis.GetDatabase();
        var deleted = await db.KeyDeleteAsync(cacheKey).ConfigureAwait(false);

        _logger.LogInformation(
            "Cache invalidation for template {TemplateName}: {Result}",
            templateName, deleted ? "SUCCESS" : "KEY_NOT_FOUND");
    }

    #endregion
}
