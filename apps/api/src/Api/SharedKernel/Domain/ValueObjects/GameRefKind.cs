namespace Api.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Discriminator for <see cref="GameRef"/> indicating which aggregate type the reference points to.
/// Issue #1320.
/// </summary>
public enum GameRefKind
{
    /// <summary>Reference to a <c>SharedGame</c> in the community catalog.</summary>
    Shared = 0,

    /// <summary>Reference to a <c>PrivateGame</c> in a user's library.</summary>
    Private = 1
}
