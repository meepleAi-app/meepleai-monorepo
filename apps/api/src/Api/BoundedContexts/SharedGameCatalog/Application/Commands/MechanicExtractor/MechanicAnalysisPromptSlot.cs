using System.Globalization;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Middleware.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// The (game, pdf, prompt) "slot" of a mechanic analysis: the filtered unique index
/// <see cref="IndexName"/> admits a single analysis with <c>status &lt;&gt; Rejected</c> per tuple
/// (T7, ADR-051). Every write that can put a second analysis inside the index — a new analysis
/// (#4144) or a Rejected one resubmitted for review — guards against it with an explicit 409, and
/// catches the 23505 of the race that slips past the guard.
/// </summary>
internal static class MechanicAnalysisPromptSlot
{
    /// <summary>
    /// Name of the filtered unique index on (shared_game_id, pdf_document_id, prompt_version) with
    /// <c>status &lt;&gt; 3</c> (see <c>MechanicAnalysisEntityConfiguration</c>). Postgres reports it as
    /// the constraint name of a 23505, on INSERT and on UPDATE alike.
    /// </summary>
    internal const string IndexName = "ux_mechanic_analyses_shared_game_pdf_prompt";

    /// <summary>
    /// True only for a 23505 on <see cref="IndexName"/>: any other unique violation stays a 500,
    /// because it would be a different defect.
    /// </summary>
    internal static bool IsTaken(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg
        && string.Equals(pg.SqlState, Npgsql.PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
        && string.Equals(pg.ConstraintName, IndexName, StringComparison.Ordinal);

    /// <summary>
    /// The 409 for a write that would put a second analysis in the slot held by
    /// <paramref name="holderId"/>. The message says what to do, which depends on where the holder is
    /// in its lifecycle: only InReview and PartiallyExtracted can be rejected by an admin; a Draft is
    /// still running (or orphaned); a Published one is final.
    /// </summary>
    internal static ConflictException Occupied(
        PromptSlotWrite write,
        Guid holderId,
        MechanicAnalysisStatus holderStatus,
        DateTime holderCreatedAt,
        string promptVersion,
        Exception? innerException = null)
    {
        var (verb, impossible) = write switch
        {
            PromptSlotWrite.Regenerate =>
                ("rigenerare", "con lo stesso prompt non se ne può generare un'altra."),
            _ =>
                ("rimettere in revisione questa", "con lo stesso prompt questa non può tornare in revisione."),
        };

        var reason = holderStatus switch
        {
            // A Draft is normally a pipeline still running; but a restart mid-run leaves it Draft until
            // the stale-draft job (#4503) rejects it, so the age is what tells the admin which one it is.
            MechanicAnalysisStatus.Draft =>
                $"Un'analisi per questo PDF con il prompt {promptVersion} è in generazione dal "
                + $"{holderCreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC: "
                + $"aspetta che finisca e, se non risulta già rifiutata, rifiutala prima di {verb}. "
                + "Se resta in generazione da molto è orfana (la pipeline si è interrotta): viene chiusa "
                + "da sola dopo il tempo massimo configurato, di norma alcune ore dalla creazione.",
            MechanicAnalysisStatus.InReview =>
                $"Esiste già un'analisi in revisione per questo PDF con il prompt {promptVersion}: "
                + $"rifiutala prima di {verb}.",
            MechanicAnalysisStatus.PartiallyExtracted =>
                $"Esiste un'analisi parzialmente estratta per questo PDF con il prompt {promptVersion}: "
                + $"rifiutala prima di {verb}.",
            MechanicAnalysisStatus.Published =>
                $"Esiste già un'analisi pubblicata per questo PDF con il prompt {promptVersion}: {impossible}",
            _ =>
                $"Esiste già un'analisi per questo PDF con il prompt {promptVersion} (stato {holderStatus}): "
                + impossible,
        };
        var message = $"{reason} Analisi esistente: {holderId}.";

        return innerException is null
            ? new ConflictException(message)
            : new ConflictException(message, innerException);
    }
}
