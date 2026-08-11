using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCoverGap;

/// <summary>
/// #3590 — elenca i giochi del catalogo SENZA alcuna cover (tutte e quattro le chiavi nulle), con
/// la CAUSA per cui la pipeline cover-da-PDF non li copre.
/// <para>
/// Il collo di bottiglia non era risolvere questi casi — il picker manuale da URL esiste da #3545 —
/// ma TROVARLI: non esisteva alcuna vista dei giochi senza cover, e l'unico accesso all'editor era
/// l'affordance a matita in hover sulla griglia pubblica.
/// </para>
/// </summary>
/// <param name="PageNumber">Pagina richiesta, 1-based.</param>
/// <param name="PageSize">Dimensione pagina (1-100).</param>
/// <param name="Cause">Filtro opzionale su una delle cause note. Null = tutte.</param>
internal record GetCoverGapQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Cause = null) : IQuery<PagedResult<CoverGapGameDto>>;

/// <summary>Un gioco senza cover, con la causa derivata dallo stato dei suoi PDF collegati.</summary>
/// <param name="GameId">Id del gioco a catalogo.</param>
/// <param name="Title">Titolo del gioco.</param>
/// <param name="BggId">Id BoardGameGeek, quando noto.</param>
/// <param name="Cause">Una delle costanti di <see cref="CoverGapCauses"/>.</param>
/// <param name="PdfFileName">Nome del PDF più rilevante collegato, se esiste.</param>
/// <param name="PdfSizeBytes">Dimensione di quel PDF in byte, se esiste.</param>
/// <param name="ErrorCategory">Categoria d'errore di quel PDF, se valorizzata.</param>
internal record CoverGapGameDto(
    Guid GameId,
    string Title,
    int? BggId,
    string Cause,
    string? PdfFileName,
    long? PdfSizeBytes,
    string? ErrorCategory);

/// <summary>
/// Cause bounded. Sono un contratto con il front-end (enum Zod in
/// <c>admin-cover.schemas.ts</c>) e con il filtro <see cref="GetCoverGapQuery.Cause"/>:
/// cambiarle qui richiede di cambiarle anche lì.
/// </summary>
internal static class CoverGapCauses
{
    /// <summary>Il PDF eccede il limite di dimensione del servizio di estrazione.</summary>
    public const string PdfTooLarge = "pdf_too_large";

    /// <summary>Nessuna pagina del rulebook è una cover accettabile — esito CORRETTO, non un guasto.</summary>
    public const string HeuristicRejected = "heuristic_rejected";

    /// <summary>Nessun PDF collegato: non esiste una sorgente da cui generare.</summary>
    public const string NoSource = "no_source";

    /// <summary>Tutto il resto: PDF ancora in lavorazione o fallimenti non classificati.</summary>
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All =
        new[] { PdfTooLarge, HeuristicRejected, NoSource, Other };
}
