using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update an existing shared game with fresh data from BoardGameGeek.
/// Supports selective field updates based on FieldsToUpdate list.
/// Issue: Admin Add Shared Game from BGG flow - "Propose Update" functionality
/// </summary>
/// <param name="GameId">ID of the existing game to update</param>
/// <param name="BggId">BoardGameGeek game ID to fetch fresh data from</param>
/// <param name="UserId">ID of the user performing the update</param>
/// <param name="FieldsToUpdate">List of field names to update. If empty/null, updates all fields.</param>
public record UpdateSharedGameFromBggCommand(
    Guid GameId,
    int BggId,
    Guid UserId,
    List<string>? FieldsToUpdate = null) : ICommand<Guid>;

/// <summary>
/// Available fields that can be selectively updated from BGG.
/// Used to validate FieldsToUpdate parameter.
/// </summary>
public static class BggUpdatableFields
{
    public const string Title = "title";
    public const string Description = "description";
    public const string YearPublished = "yearPublished";
    public const string MinPlayers = "minPlayers";
    public const string MaxPlayers = "maxPlayers";
    public const string PlayingTime = "playingTime";
    public const string MinAge = "minAge";
    public const string ComplexityRating = "complexityRating";
    public const string AverageRating = "averageRating";
    public const string ImageUrl = "imageUrl";
    public const string ThumbnailUrl = "thumbnailUrl";
    public const string Designers = "designers";
    public const string Publishers = "publishers";
    public const string Categories = "categories";
    public const string Mechanics = "mechanics";

    private static readonly HashSet<string> AllFields = new(StringComparer.OrdinalIgnoreCase)
    {
        Title,
        Description,
        YearPublished,
        MinPlayers,
        MaxPlayers,
        PlayingTime,
        MinAge,
        ComplexityRating,
        AverageRating,
        ImageUrl,
        ThumbnailUrl,
        Designers,
        Publishers,
        Categories,
        Mechanics
    };

    /// <summary>
    /// Gets all valid field names as an immutable set.
    /// </summary>
    public static IReadOnlySet<string> All => AllFields;

    public static bool IsValid(string field) => AllFields.Contains(field);
}
