using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.RagEnhancements;

/// <summary>
/// Inverts a RAG enhancement flag on behalf of an admin: globally, or for one tier when <see cref="Tier"/> is set.
/// The result is the state after the toggle (<c>true</c> = on).
/// </summary>
/// <remarks>
/// #4594: the two admin routes called <c>IFeatureFlagService</c> without the admin id, so the author of the
/// configuration row was <see cref="Guid.Empty"/> and the request was rejected with 422. The id is a required
/// part of the command, and the validator refuses <see cref="Guid.Empty"/>.
/// </remarks>
/// <param name="FeatureKey">One of <c>FeatureFlagConstants.RagEnhancements</c>.</param>
/// <param name="AdminId">The admin who asked for the change: it is saved as the author of the row.</param>
/// <param name="Tier">The tier to toggle, or <c>null</c> for the global flag.</param>
internal record ToggleRagEnhancementCommand(
    string FeatureKey,
    Guid AdminId,
    UserTier? Tier = null
) : ICommand<bool>;
