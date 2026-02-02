using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Service for managing AI model configurations and tier-based access control.
/// Issue #3377: Models Tier Endpoint
/// </summary>
public interface IModelConfigurationService
{
    /// <summary>
    /// Gets all available model configurations.
    /// </summary>
    /// <returns>All model configurations.</returns>
    IReadOnlyList<ModelConfiguration> GetAllModels();

    /// <summary>
    /// Gets model configurations filtered by tier.
    /// Returns models at the specified tier and below.
    /// </summary>
    /// <param name="tier">The maximum tier to include.</param>
    /// <returns>Models accessible at the specified tier.</returns>
    IReadOnlyList<ModelConfiguration> GetModelsByTier(ModelTier tier);

    /// <summary>
    /// Gets a specific model configuration by ID.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The model configuration if found, null otherwise.</returns>
    ModelConfiguration? GetModelById(string modelId);

    /// <summary>
    /// Validates whether a user tier can access a specific model.
    /// </summary>
    /// <param name="userTier">The user's subscription tier.</param>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>Validation result with details.</returns>
    ModelTierValidationResult ValidateUserTierForModel(ModelTier userTier, string modelId);
}

/// <summary>
/// Result of model tier validation.
/// </summary>
/// <param name="IsValid">Whether the user can access the model.</param>
/// <param name="UserTier">The user's tier.</param>
/// <param name="RequiredTier">The model's required tier (if model exists).</param>
/// <param name="ModelId">The model identifier.</param>
/// <param name="Message">Validation message.</param>
public record ModelTierValidationResult(
    bool IsValid,
    ModelTier UserTier,
    ModelTier? RequiredTier,
    string ModelId,
    string Message)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ModelTierValidationResult Success(ModelTier userTier, ModelTier requiredTier, string modelId)
        => new(true, userTier, requiredTier, modelId, "Access granted");

    /// <summary>
    /// Creates a failed validation result for insufficient tier.
    /// </summary>
    public static ModelTierValidationResult InsufficientTier(ModelTier userTier, ModelTier requiredTier, string modelId)
        => new(false, userTier, requiredTier, modelId,
            $"Access denied: Model '{modelId}' requires {requiredTier.GetDisplayName()} tier, user has {userTier.GetDisplayName()} tier");

    /// <summary>
    /// Creates a failed validation result for model not found.
    /// </summary>
    public static ModelTierValidationResult ModelNotFound(ModelTier userTier, string modelId)
        => new(false, userTier, null, modelId, $"Model '{modelId}' not found");
}
