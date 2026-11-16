using System.Diagnostics.CodeAnalysis;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

/// <summary>
/// #5014: the extracted text of the PDF yields no rule, so no RuleSpec can be generated from it.
/// </summary>
/// <remarks>
/// <para>
/// A 409, not a 400: the request is well formed, and what refuses it is the state of the document, as for the
/// sibling case of the same handler (a PDF not linked to any game, #3096). Re-extracting or replacing the PDF changes
/// the answer; changing the request does not.
/// </para>
/// <para>
/// It replaces an <see cref="InvalidOperationException"/> that the endpoint caught and turned into a 400 with its
/// message: that catch turned every <see cref="InvalidOperationException"/> under the command into a request error.
/// </para>
/// </remarks>
public sealed class PdfHasNoParsedRulesException : ConflictException
{
    public const string Code = "pdf_has_no_parsed_rules";

    public Guid PdfDocumentId { get; }

    [SetsRequiredMembers]
    public PdfHasNoParsedRulesException(Guid pdfDocumentId)
        : base(Code, $"PDF document {pdfDocumentId} does not contain any parsed rules")
    {
        PdfDocumentId = pdfDocumentId;
    }
}
