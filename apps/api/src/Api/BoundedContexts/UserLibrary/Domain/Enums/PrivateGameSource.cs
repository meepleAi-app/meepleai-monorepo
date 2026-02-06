namespace Api.BoundedContexts.UserLibrary.Domain.Enums;

/// <summary>
/// Source of private game data.
/// </summary>
public enum PrivateGameSource
{
    /// <summary>
    /// User entered all data manually.
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Data fetched from BoardGameGeek API.
    /// </summary>
    BoardGameGeek = 1
}
