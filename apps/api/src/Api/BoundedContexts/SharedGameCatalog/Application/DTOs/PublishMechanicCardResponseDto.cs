namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Response for a successful card publication (#527, AC-6). Returned with HTTP 201 Created.
/// </summary>
/// <param name="CardId">Id of the newly created card.</param>
/// <param name="SharedGameId">Game the card belongs to.</param>
/// <param name="Version">Monotonic version for the game (AD-3).</param>
/// <param name="PublishedAt">Publish timestamp.</param>
/// <param name="PublishedBy">Admin who published.</param>
/// <param name="Title">Resolved card title.</param>
/// <param name="OriginAnalysisId">Source analysis id.</param>
/// <param name="PublicCardUrl">Shape of the player-facing URL (#528, may not be live yet).</param>
internal record PublishMechanicCardResponseDto(
    Guid CardId,
    Guid SharedGameId,
    int Version,
    DateTime PublishedAt,
    Guid PublishedBy,
    string Title,
    Guid OriginAnalysisId,
    string PublicCardUrl);
