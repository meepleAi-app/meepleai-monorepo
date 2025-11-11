using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing valid player count range for a game.
/// </summary>
public sealed class PlayerCount : ValueObject
{
    private const int AbsoluteMin = 1;
    private const int AbsoluteMax = 100;

    public int Min { get; }
    public int Max { get; }

    public PlayerCount(int min, int max)
    {
        if (min < AbsoluteMin)
            throw new ValidationException($"Minimum player count cannot be less than {AbsoluteMin}");

        if (max > AbsoluteMax)
            throw new ValidationException($"Maximum player count cannot exceed {AbsoluteMax}");

        if (min > max)
            throw new ValidationException("Minimum player count cannot exceed maximum");

        Min = min;
        Max = max;
    }

    /// <summary>
    /// Checks if the given player count is supported by this game.
    /// </summary>
    public bool Supports(int playerCount)
    {
        return playerCount >= Min && playerCount <= Max;
    }

    /// <summary>
    /// Checks if this game is suitable for solo play.
    /// </summary>
    public bool SupportsSolo => Min == 1;

    /// <summary>
    /// Creates a player count for solo-only games.
    /// </summary>
    public static PlayerCount Solo => new PlayerCount(1, 1);

    /// <summary>
    /// Creates a player count for standard multiplayer games (2-4).
    /// </summary>
    public static PlayerCount Standard => new PlayerCount(2, 4);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Min;
        yield return Max;
    }

    public override string ToString() => Min == Max ? $"{Min}" : $"{Min}-{Max}";
}
