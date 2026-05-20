using System.Text.Json;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetSystemPrompt;

/// <summary>
/// Handler for <see cref="GetSystemPromptQuery"/> — returns the agent system
/// prompt projection gated by viewer class (owner vs public)
/// per spec-panel 2026-05-18 §2 / issue #822.
/// </summary>
/// <remarks>
/// <para>
/// Cache strategy: 10min HybridCache per <c>(toolkitId, viewerClass)</c>
/// where viewerClass is the binary <c>owner</c>/<c>public</c> partition —
/// not per-user, to avoid cache proliferation (spec-panel §5 matrix).
/// Tagged with <c>"toolkit:{id}"</c> so publish/yank events invalidate
/// the prompt cache alongside the rest of the toolkit surface.
/// </para>
/// <para>
/// Visibility rule mirrors <c>GetToolkitDetailQueryHandler</c>: non-author
/// viewers only see published+approved toolkits; otherwise return <c>null</c>
/// (endpoint → 404).
/// </para>
/// </remarks>
internal sealed class GetSystemPromptQueryHandler
    : IRequestHandler<GetSystemPromptQuery, SystemPromptResponse?>
{
    private const string DefaultAgentMode = "default";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GetSystemPromptQueryHandler> _logger;

    public GetSystemPromptQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        TimeProvider timeProvider,
        ILogger<GetSystemPromptQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SystemPromptResponse?> Handle(
        GetSystemPromptQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Visibility check runs OUTSIDE the cache so a non-author cannot get
        // a stale owner-cached payload if the toolkit becomes a draft after
        // a previous owner-class read populated the cache.
        var entity = await _context.GameToolkits
            .AsNoTracking()
            .Where(t => t.Id == request.ToolkitId)
            .Select(t => new
            {
                t.Id,
                t.CreatedByUserId,
                t.IsPublished,
                t.TemplateStatus,
                t.AgentConfig,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogInformation(
                "Toolkit {ToolkitId} not found for system-prompt request (viewer {ViewerId})",
                request.ToolkitId,
                request.ViewerId);
            return null;
        }

        var isOwner = entity.CreatedByUserId == request.ViewerId;
        var isPublished = entity.IsPublished
            && (TemplateStatus)entity.TemplateStatus == TemplateStatus.Approved;

        if (!isOwner && !isPublished)
        {
            _logger.LogInformation(
                "Toolkit {ToolkitId} system-prompt hidden from viewer {ViewerId}",
                request.ToolkitId,
                request.ViewerId);
            return null;
        }

        var viewerClass = isOwner ? "owner" : "public";
        var cacheKey = $"toolkits:{request.ToolkitId:N}:system-prompt:{viewerClass}";

        var cached = await _cache.GetOrCreateAsync(
            cacheKey,
            ct => Task.FromResult(BuildResponse(entity.AgentConfig, isOwner)),
            tags:
            [
                $"toolkit:{request.ToolkitId:N}",
                "toolkitSystemPrompt",
            ],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        return cached;
    }

    /// <summary>
    /// Parses the toolkit's <c>AgentConfig</c> JSON to extract the system
    /// prompt and agent mode, then projects into the owner-or-public DTO.
    /// </summary>
    private SystemPromptResponse BuildResponse(string? agentConfig, bool isOwner)
    {
        var (fullPrompt, agentMode) = ExtractPromptAndMode(agentConfig);
        var now = _timeProvider.GetUtcNow();

        if (isOwner)
        {
            return new SystemPromptResponse(
                Owner: new SystemPromptOwnerDto(
                    FullPrompt: fullPrompt,
                    AgentMode: agentMode,
                    GeneratedAt: now),
                Public: null);
        }

        return new SystemPromptResponse(
            Owner: null,
            Public: new SystemPromptPublicDto(
                AgentMode: agentMode,
                CharacterCount: fullPrompt.Length));
    }

    /// <summary>
    /// Tolerant JSON extraction — mirrors <c>GetToolkitDetailQueryHandler.BuildAgentSummary</c>:
    /// malformed JSON yields empty prompt + default mode rather than 500.
    /// </summary>
    private static (string FullPrompt, string AgentMode) ExtractPromptAndMode(string? agentConfig)
    {
        if (string.IsNullOrWhiteSpace(agentConfig))
        {
            return (string.Empty, DefaultAgentMode);
        }

        try
        {
            using var doc = JsonDocument.Parse(agentConfig);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (string.Empty, DefaultAgentMode);
            }

            var prompt = TryGetString(root, "systemPrompt") ?? string.Empty;
            var mode = TryGetString(root, "mode")
                ?? TryGetString(root, "agentMode")
                ?? DefaultAgentMode;

            return (prompt, mode);
        }
        catch (JsonException)
        {
            // Malformed AgentConfig — same fallback as GetToolkitDetail surface.
            return (string.Empty, DefaultAgentMode);
        }
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var prop)
            && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}
