using System.Text.Json.Serialization;

namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of a PDF deletion operation.
/// </summary>
/// <param name="Success">The request was handled; it does not say the PDF is gone (see <paramref name="Outcome"/>).</param>
/// <param name="Message">What happened, in words.</param>
/// <param name="GameId">The game the PDF was stored under, for the callers that invalidate by game.</param>
/// <param name="Outcome">
/// What happened to the PDF. Kept out of the JSON of the admin sandbox route that serializes this record whole:
/// the user-facing route builds its own answer from it (#4627).
/// </param>
internal record PdfDeleteResult(
    bool Success,
    string Message,
    string? GameId,
    [property: JsonIgnore] PdfDeleteOutcome Outcome = PdfDeleteOutcome.Deleted);

/// <summary>
/// What a delete request did to the PDF (#4627). A PDF that another game still uses is not deleted (#2949): the answer
/// must not read as if it were.
/// </summary>
internal enum PdfDeleteOutcome
{
    /// <summary>The row, the vectors and the file are gone (or the PDF was not found, with <c>Success = false</c>).</summary>
    Deleted,

    /// <summary>Another game still uses the PDF: the game it belongs to let go of it, and the PDF stays for the others.</summary>
    Unlinked,

    /// <summary>Another game still uses the PDF and there was no link of the caller's game left to remove: nothing changed.</summary>
    Kept,
}
