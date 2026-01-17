using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handler for SeedAiModelsCommand.
/// Populates default AI model configurations (OpenRouter + Ollama).
/// Idempotent: Only executes if no AI models exist.
/// </summary>
internal sealed class SeedAiModelsCommandHandler : ICommandHandler<SeedAiModelsCommand>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SeedAiModelsCommandHandler> _logger;

    public SeedAiModelsCommandHandler(
        IAiModelConfigurationRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SeedAiModelsCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedAiModelsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check idempotency: Skip if AI models already exist
        var hasModels = await _repository.AnyAsync(cancellationToken).ConfigureAwait(false);

        if (hasModels)
        {
            _logger.LogInformation("AI models already seeded. Skipping seed.");
            return;
        }

        // Define default AI models (Issue #2520: Added pricing and settings)
        var models = new[]
        {
            AiModelConfiguration.Create(
                modelId: "meta-llama/llama-3.3-70b-instruct:free",
                displayName: "Llama 3.3 70B Instruct (Free)",
                provider: "OpenRouter",
                priority: 1,
                maxTokens: 4096,
                temperature: 0.7m,
                costPerInputToken: 0m,      // Free tier
                costPerOutputToken: 0m,     // Free tier
                isActive: true,
                isPrimary: true
            ),
            AiModelConfiguration.Create(
                modelId: "meta-llama/llama-3.3-70b-instruct",
                displayName: "Llama 3.3 70B Instruct",
                provider: "OpenRouter",
                priority: 2,
                maxTokens: 4096,
                temperature: 0.7m,
                costPerInputToken: 0.18m,   // $0.18 per 1M tokens
                costPerOutputToken: 0.18m,
                isActive: true,
                isPrimary: false
            ),
            AiModelConfiguration.Create(
                modelId: "google/gemini-pro",
                displayName: "Gemini Pro",
                provider: "OpenRouter",
                priority: 3,
                maxTokens: 8192,
                temperature: 0.7m,
                costPerInputToken: 0.125m,  // $0.125 per 1M tokens
                costPerOutputToken: 0.375m, // $0.375 per 1M tokens
                isActive: true,
                isPrimary: false
            ),
            AiModelConfiguration.Create(
                modelId: "deepseek/deepseek-chat",
                displayName: "DeepSeek Chat",
                provider: "OpenRouter",
                priority: 4,
                maxTokens: 4096,
                temperature: 0.7m,
                costPerInputToken: 0.27m,   // $0.27 per 1M tokens
                costPerOutputToken: 1.10m,  // $1.10 per 1M tokens
                isActive: true,
                isPrimary: false
            ),
            AiModelConfiguration.Create(
                modelId: "llama3:8b",
                displayName: "Llama 3 8B (Local)",
                provider: "Ollama",
                priority: 5,
                maxTokens: 2048,
                temperature: 0.7m,
                costPerInputToken: 0m,      // Local - no cost
                costPerOutputToken: 0m,
                isActive: false,
                isPrimary: false
            ),
            AiModelConfiguration.Create(
                modelId: "mistral",
                displayName: "Mistral (Local)",
                provider: "Ollama",
                priority: 6,
                maxTokens: 2048,
                temperature: 0.7m,
                costPerInputToken: 0m,      // Local - no cost
                costPerOutputToken: 0m,
                isActive: false,
                isPrimary: false
            )
        };

        // Persist all models
        await _repository.AddRangeAsync(models, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("AI models seeded successfully: {Count} models", models.Length);
    }
}
