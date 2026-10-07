namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Available colors for players in a live game session.
/// </summary>
public enum PlayerColor
{
    /// <summary>Red player color.</summary>
    Red = 0,

    /// <summary>Blue player color.</summary>
    Blue = 1,

    /// <summary>Green player color.</summary>
    Green = 2,

    /// <summary>Yellow player color.</summary>
    Yellow = 3,

    /// <summary>Purple player color.</summary>
    Purple = 4,

    /// <summary>Orange player color.</summary>
    Orange = 5,

    /// <summary>Pink player color.</summary>
    Pink = 6,

    /// <summary>Teal player color.</summary>
    Teal = 7,

    // #4107 — White e Black esistevano nel frontend e non qui, quindi un utente che li
    // scegliesse dalla palette riceveva `400`. Misurato: `{"color":"White"}` →
    // 400 domain_error, mentre `{"color":"Blue"}` → 201.
    //
    // La palette UI (`PlayerSetup.PLAYER_COLORS`), l'elenco di `PlayerSetupDialog` e
    // `PlayerColorSchema` offrono tutti e tre dieci colori: era il dominio a non saperne due.
    //
    // Appendere e' sicuro: `session_players.color` e' `character varying` e la conversione
    // JSON e' per NOME (un `{"color":1}` numerico riceve 400), quindi i valori esistenti non
    // si spostano. Non riordinare i membri sopra per la stessa ragione.

    /// <summary>White player color.</summary>
    White = 8,

    /// <summary>Black player color.</summary>
    Black = 9
}
