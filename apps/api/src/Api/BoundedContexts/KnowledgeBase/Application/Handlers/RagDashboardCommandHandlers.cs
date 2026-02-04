using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Issue #3304: RAG Dashboard command handlers.
/// </summary>

/// <summary>
/// Handler for SaveRagConfigCommand.
/// </summary>
internal sealed class SaveRagConfigCommandHandler : ICommandHandler<SaveRagConfigCommand, RagConfigDto>
{
    private readonly ILogger<SaveRagConfigCommandHandler> _logger;

    public SaveRagConfigCommandHandler(ILogger<SaveRagConfigCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<RagConfigDto> Handle(SaveRagConfigCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Saving RAG config for UserId={UserId}, Strategy={Strategy}",
            command.UserId,
            command.Config.ActiveStrategy);

        // FUTURE: Persist to database when user config persistence is implemented
        // For now, just return the config as-is (validated)

        return Task.FromResult(command.Config);
    }
}

/// <summary>
/// Handler for ResetRagConfigCommand.
/// </summary>
internal sealed class ResetRagConfigCommandHandler : ICommandHandler<ResetRagConfigCommand, RagConfigDto>
{
    private readonly ILogger<ResetRagConfigCommandHandler> _logger;

    public ResetRagConfigCommandHandler(ILogger<ResetRagConfigCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<RagConfigDto> Handle(ResetRagConfigCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Resetting RAG config for UserId={UserId}, Strategy={Strategy}",
            command.UserId,
            command.Strategy ?? "all");

        // FUTURE: Delete user config from database when persistence is implemented
        // For now, return default config

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

        return Task.FromResult(defaultConfig);
    }
}
