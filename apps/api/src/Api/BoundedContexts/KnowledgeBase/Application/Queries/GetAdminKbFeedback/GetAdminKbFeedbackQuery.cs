using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;

/// <summary>
/// Query to retrieve paginated user feedback on KB responses (admin view).
/// KB-08: Admin Feedback Review backend. #4469: il gioco non è più obbligatorio, e si filtra per
/// versione del profilo: <see cref="Version"/> è l'id di una versione, <c>none</c> per i feedback senza
/// versione, o nullo per tutti.
/// </summary>
internal sealed record GetAdminKbFeedbackQuery(
    Guid? GameId,
    string? Version,
    string? OutcomeFilter,
    DateTime? FromDate,
    int Page,
    int PageSize) : IQuery<AdminKbFeedbackDto>;

/// <summary>
/// Paginated result containing feedback items.
/// </summary>
internal sealed record AdminKbFeedbackDto(
    int Total,
    List<AdminKbFeedbackItemDto> Items);

/// <summary>
/// Single feedback item in the admin feedback list. #4469: il nome del gioco, la versione del profilo
/// che ha prodotto la risposta, e l'inizio della risposta, nullo se la risposta non c'è più.
/// </summary>
internal sealed record AdminKbFeedbackItemDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    string? GameName,
    Guid ChatSessionId,
    Guid MessageId,
    Guid? ProfileVersionId,
    int? VersionNumber,
    string Outcome,
    string? Comment,
    DateTime CreatedAt,
    string? AnswerExcerpt);

/// <summary>
/// #4469 (ADR-095 fetta 7a): i 👍👎 per versione del profilo, e i giochi che hanno feedback, per
/// sceglierli per nome.
/// </summary>
internal sealed record GetAdminKbFeedbackSummaryQuery : IQuery<AdminKbFeedbackSummaryDto>;

internal sealed record AdminKbFeedbackSummaryDto(
    List<AdminKbFeedbackVersionDto> Versions,
    List<AdminKbFeedbackGameDto> Games);

/// <summary>
/// Utili e non utili di una versione del profilo. <see cref="ProfileVersionId"/> nullo è la voce dei
/// feedback senza versione (anteriori a #4168, o su risposte salvate senza provenienza).
/// </summary>
internal sealed record AdminKbFeedbackVersionDto(
    Guid? ProfileVersionId,
    int? VersionNumber,
    string? Status,
    DateTime? PublishedAt,
    int Helpful,
    int NotHelpful);

/// <summary>Il nome è nullo se il gioco non è più né nel catalogo né fra i giochi privati.</summary>
internal sealed record AdminKbFeedbackGameDto(Guid GameId, string? GameName);
