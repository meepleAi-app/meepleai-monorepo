using System.ComponentModel;

namespace Api.BoundedContexts.SessionTracking.Domain.Enums;

/// <summary>
/// DEC-1: Polymorphic scoring type for board game sessions. Each ScoreType
/// maps to an IScoringStrategy implementation that validates + computes
/// winner from JSON score_data. Stored in sessions.scoring_type column.
/// Default = Points (covers ~50% catalog board games).
/// </summary>
public enum ScoreType
{
    /// <summary>Punti numerici per player (Wingspan, Catan, Azul).</summary>
    [Description("Punti numerici per player")]
    Points = 0,

    /// <summary>Winner/Loser binario (cooperativo Pandemic, Forbidden Island).</summary>
    [Description("Winner/Loser binario (cooperativo)")]
    BinaryWin = 1,

    /// <summary>Obiettivi completati per player (T.I.M.E. Stories, Sherlock Holmes).</summary>
    [Description("Obiettivi completati per player")]
    Objectives = 2,

    /// <summary>Posizione 1..N per player (giochi a corsa, racing games).</summary>
    [Description("Posizione 1..N per player")]
    Ranking = 3,
}
