namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

public sealed class GameBookKbSourceConflictException : Exception
{
    public GameBookKbSourceConflictException(Guid pdfDocId, Guid conflictingBookId)
        : base($"PDF document {pdfDocId} is already kbSource of community GameBook {conflictingBookId}.") { }
}
