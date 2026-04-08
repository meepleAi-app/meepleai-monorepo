using System.Text.Json;

namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a player's current resources in a game session (L2 state).
/// Resources are generic key-value pairs (e.g., "wood"=3, "ore"=2) supporting any board game.
/// Stored as JSON in SessionCheckpoint.SnapshotData.
/// </summary>
public sealed class PlayerResources
{
    public Guid ParticipantId { get; }
    public IReadOnlyDictionary<string, int> Resources { get; }

    private PlayerResources(Guid participantId, Dictionary<string, int> resources)
    {
        ParticipantId = participantId;
        Resources = resources;
    }

    /// <summary>
    /// Factory method to create a new PlayerResources value object.
    /// </summary>
    /// <param name="participantId">Participant unique identifier (cannot be empty).</param>
    /// <param name="resources">Resource key-value pairs.</param>
    /// <returns>New PlayerResources instance with a defensive copy of the dictionary.</returns>
    public static PlayerResources Create(Guid participantId, Dictionary<string, int> resources)
    {
        if (participantId == Guid.Empty)
            throw new ArgumentException("ParticipantId cannot be empty.", nameof(participantId));

        ArgumentNullException.ThrowIfNull(resources);

        return new PlayerResources(participantId, new Dictionary<string, int>(resources, StringComparer.Ordinal));
    }

    /// <summary>
    /// Returns a new PlayerResources with the specified resource added or updated.
    /// Original instance remains unchanged (immutability).
    /// </summary>
    public PlayerResources WithResource(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Resource key cannot be empty.", nameof(key));

        var dict = new Dictionary<string, int>(Resources, StringComparer.Ordinal) { [key] = value };
        return new PlayerResources(ParticipantId, dict);
    }

    /// <summary>
    /// Serializes the resource dictionary to JSON.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(Resources);

    /// <summary>
    /// Deserializes a JSON string into a PlayerResources value object.
    /// </summary>
    /// <param name="participantId">Participant unique identifier.</param>
    /// <param name="json">JSON string representing the resources dictionary.</param>
    /// <returns>New PlayerResources instance.</returns>
    public static PlayerResources FromJson(Guid participantId, string json)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        return Create(participantId, dict);
    }
}
