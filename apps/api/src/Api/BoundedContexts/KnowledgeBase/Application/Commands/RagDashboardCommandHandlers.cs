using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Issue #3304, #5311: RAG Dashboard command handlers with DB persistence.
/// </summary>

/// <summary>
/// Handler for SaveRagConfigCommand — persists config to database.
/// </summary>
internal sealed class SaveRagConfigCommandHandler : ICommandHandler<SaveRagConfigCommand, RagConfigDto>
{
    private readonly IRagUserConfigRepository _repository;
    private readonly ILogger<SaveRagConfigCommandHandler> _logger;

    public SaveRagConfigCommandHandler(
        IRagUserConfigRepository repository,
        ILogger<SaveRagConfigCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RagConfigDto> Handle(SaveRagConfigCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Saving RAG config for UserId={UserId}, Strategy={Strategy}",
            command.UserId,
            command.Config.ActiveStrategy);

        var configJson = JsonSerializer.Serialize(command.Config, JsonSerializerOptions);
        await _repository.UpsertAsync(command.UserId, configJson, cancellationToken).ConfigureAwait(false);

        return command.Config;
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

/// <summary>
/// Handler for ResetRagConfigCommand — deletes user config from database and returns defaults.
/// </summary>
internal sealed class ResetRagConfigCommandHandler : ICommandHandler<ResetRagConfigCommand, RagConfigDto>
{
    private readonly IRagUserConfigRepository _repository;
    private readonly ILogger<ResetRagConfigCommandHandler> _logger;

    public ResetRagConfigCommandHandler(
        IRagUserConfigRepository repository,
        ILogger<ResetRagConfigCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RagConfigDto> Handle(ResetRagConfigCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Resetting RAG config for UserId={UserId}, Strategy={Strategy}",
            command.UserId,
            command.Strategy ?? "all");

        await _repository.DeleteByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        var defaultConfig = new RagConfigDto
        {
            Generation = new GenerationParamsDto
            {
                Temperature = 0.7,
                TopK = 40,
                TopP = 0.9,
                MaxTokens = 1000
            },
            Retrieval = new RetrievalParamsDto
            {
                ChunkSize = 500,
                ChunkOverlap = 10,
                TopResults = 5,
                SimilarityThreshold = 0.7
            },
            Reranker = new RerankerSettingsDto
            {
                Enabled = true,
                Model = "cross-encoder/ms-marco-MiniLM-L-12-v2",
                TopN = 10
            },
            Models = new ModelSelectionDto
            {
                PrimaryModel = "gpt-4o-mini",
                FallbackModel = null,
                EvaluationModel = null
            },
            StrategySpecific = new StrategySpecificSettingsDto
            {
                HybridAlpha = 0.5,
                ContextWindow = 5,
                MaxHops = 3
            },
            ActiveStrategy = command.Strategy ?? "Hybrid"
        };

        return defaultConfig;
    }
}
