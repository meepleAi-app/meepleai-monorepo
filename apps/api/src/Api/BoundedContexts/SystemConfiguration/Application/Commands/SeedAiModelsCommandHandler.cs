using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

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

        // Define default AI models (Issue #2520: JSON settings approach)
        var models = new[]
        {
            AiModelConfiguration.Create(
                modelId: "meta-llama/llama-3.3-70b-instruct:free",
                displayName: "Llama 3.3 70B Instruct (Free)",
                provider: "OpenRouter",
                priority: 1,
                settings: new ModelSettings(
                    maxTokens: 4096,
                    temperature: 0.7m,
                    pricing: ModelPricing.Free
                ),
                isActive: true,
                isPrimary: true
            ),
            AiModelConfiguration.Create(
                modelId: "meta-llama/llama-3.3-70b-instruct",
                displayName: "Llama 3.3 70B Instruct",
                provider: "OpenRouter",
                priority: 2,
                settings: new ModelSettings(
                    maxTokens: 4096,
                    temperature: 0.7m,
                    pricing: new ModelPricing(
                        inputPricePerMillion: 0.10m,  // OpenRouter 2026 rates
                        outputPricePerMillion: 0.32m
                    )
                ),
                isActive: true,
                isPrimary: false
            ),
            AiModelConfiguration.Create(
                modelId: "google/gemini-2.0-flash-exp:free",
                displayName: "Gemini 2.0 Flash Experimental (Free)",
                provider: "OpenRouter",
                priority: 3,
                settings: new ModelSettings(
                    maxTokens: 8192,
                    temperature: 0.7m,
                    pricing: ModelPricing.Free
                ),
                isActive: true,
                isPrimary: false
            ),
            AiModelConfiguration.Create(
                modelId: "deepseek/deepseek-chat",
                displayName: "DeepSeek Chat",
                provider: "OpenRouter",
                priority: 4,
                settings: new ModelSettings(
                    maxTokens: 4096,
                    temperature: 0.7m,
                    pricing: new ModelPricing(
                        inputPricePerMillion: 0.19m,  // OpenRouter 2026 rates
                        outputPricePerMillion: 0.87m
                    )
                ),
                isActive: true,
                isPrimary: false
            ),
            AiModelConfiguration.Create(
                modelId: "llama3:8b",
                displayName: "Llama 3 8B (Local)",
                provider: "Ollama",
                priority: 5,
                settings: new ModelSettings(
                    maxTokens: 2048,
                    temperature: 0.7m,
                    pricing: ModelPricing.Free
                ),
                isActive: false,
                isPrimary: false
            ),
            AiModelConfiguration.Create(
                modelId: "mistral",
                displayName: "Mistral (Local)",
                provider: "Ollama",
                priority: 6,
                settings: new ModelSettings(
                    maxTokens: 2048,
                    temperature: 0.7m,
                    pricing: ModelPricing.Free
                ),
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
