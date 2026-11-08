namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTO for batch job details (Issue #3693)
/// </summary>
public sealed record BatchJobDto(
    Guid Id,
    string Type,
    string Status,
    int Progress,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? DurationSeconds,
    string? ResultSummary,
    string? ErrorMessage,
    DateTime CreatedAt);

/// <summary>
/// DTO for batch job list with pagination (Issue #3693).
/// </summary>
/// <remarks>
/// #3853 — Page e PageSize mancavano, pur essendo una risposta paginata: l'handler calcola
/// gia' skip/take da quei valori, ma non li restituiva. Lo schema Zod del frontend li dichiarava
/// obbligatori, la validazione falliva e nove pagine di amministrazione scartavano dati validi
/// senza dire nulla all'utente.
///
/// Aggiungerli al backend invece di allentare lo schema: una lista paginata che non dice quale
/// pagina sia costringe il client a ricordarsene, ed e' un'informazione che il server ha gia'.
/// L'aggiunta e' additiva, quindi nessun client esistente si rompe.
/// </remarks>
public sealed record BatchJobListDto(
    List<BatchJobDto> Jobs,
    int Total,
    int Page,
    int PageSize);

/// <summary>
/// Request DTO for creating a batch job (Issue #3693)
/// </summary>
public sealed record CreateBatchJobRequest(
    string Type,
    string Parameters);

/// <summary>
/// Response DTO for batch job creation (Issue #3693)
/// </summary>
public sealed record CreateBatchJobResponse(
    Guid JobId);
