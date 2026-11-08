using System.Text.Json.Serialization;

namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO for a wishlist item.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal record WishlistItemDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    string Priority,
    decimal? TargetPrice,
    string? Notes,
    DateTime AddedAt,
    DateTime? UpdatedAt,
    string Visibility,
    // Titolo del gioco nel catalogo condiviso (#4227). Lo valorizzano solo le letture della wishlist;
    // quando manca (risposte di add/update, gioco non piu' nel catalogo) il campo viene omesso, non
    // serializzato come null: un client che lo dichiara facoltativo ma non nullable continua a leggerlo.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? GameName = null
);
