namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Represents the subscription tier required to access an AI model.
/// Issue #3377: Models Tier Endpoint
/// </summary>
/// <remarks>
/// Tier hierarchy: Free &lt; Normal &lt; Premium &lt; Custom
/// Users can access models at or below their tier level.
/// </remarks>
public enum ModelTier
{
    /// <summary>
    /// Free tier - available to all users without subscription.
    /// Includes rate-limited free models (e.g., Llama free tier).
    /// </summary>
    Free = 0,

    /// <summary>
    /// Normal tier - basic paid subscription.
    /// Access to standard models with moderate pricing.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Premium tier - premium subscription.
    /// Access to high-performance models like GPT-4, Claude 3.
    /// </summary>
    Premium = 2,

    /// <summary>
    /// Custom tier - enterprise/custom arrangements.
    /// Access to all models including custom fine-tuned models.
    /// </summary>
    Custom = 3
}

/// <summary>
/// Extension methods for ModelTier enum.
/// </summary>
public static class ModelTierExtensions
{
    /// <summary>
    /// Checks if a user tier can access a model tier.
    /// </summary>
    /// <param name="userTier">The user's subscription tier.</param>
    /// <param name="modelTier">The model's required tier.</param>
    /// <returns>True if user can access the model, false otherwise.</returns>
    public static bool CanAccess(this ModelTier userTier, ModelTier modelTier)
        => userTier >= modelTier;

    /// <summary>
    /// Parses a string to ModelTier enum.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <returns>The parsed ModelTier.</returns>
    /// <exception cref="ArgumentException">Thrown when value is not a valid tier.</exception>
    public static ModelTier Parse(string value)
    {
        if (TryParse(value, out var tier))
            return tier;

        throw new ArgumentException($"Invalid model tier: '{value}'. Valid values are: Free, Normal, Premium, Custom", nameof(value));
    }

    /// <summary>
    /// Tries to parse a string to ModelTier enum.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="tier">The parsed tier if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(string? value, out ModelTier tier)
    {
        tier = ModelTier.Free;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        return Enum.TryParse(value, ignoreCase: true, out tier);
    }

    /// <summary>
    /// Gets the display name for a tier.
    /// </summary>
    public static string GetDisplayName(this ModelTier tier) => tier switch
    {
        ModelTier.Free => "Free",
        ModelTier.Normal => "Normal",
        ModelTier.Premium => "Premium",
        ModelTier.Custom => "Custom",
        _ => tier.ToString()
    };
}
