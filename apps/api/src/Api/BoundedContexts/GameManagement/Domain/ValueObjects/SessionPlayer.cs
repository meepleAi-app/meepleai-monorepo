using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing a player in a game session.
/// </summary>
internal sealed class SessionPlayer : ValueObject
{
    public string PlayerName { get; }
    public int PlayerOrder { get; }
    public string? Color { get; }

    public SessionPlayer(string playerName, int playerOrder, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            throw new ValidationException("Player name cannot be empty");

        var trimmed = playerName.Trim();
        if (trimmed.Length > 50)
            throw new ValidationException("Player name cannot exceed 50 characters");

        if (playerOrder < 1)
            throw new ValidationException("Player order must be at least 1");

        if (playerOrder > 100)
            throw new ValidationException("Player order cannot exceed 100");

        PlayerName = trimmed;
        PlayerOrder = playerOrder;
        Color = color?.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PlayerName.ToLowerInvariant();
        yield return PlayerOrder;
    }

    public override string ToString() =>
        Color != null
            ? $"{PlayerName} ({Color}, P{PlayerOrder})"
            : $"{PlayerName} (P{PlayerOrder})";
}
