namespace Api.BoundedContexts.KnowledgeBase.Domain.Exceptions;

/// <summary>
/// Exception thrown when game state is malformed or invalid.
/// Issue #3772: Used by Game State Parser for validation errors.
/// </summary>
public sealed class InvalidGameStateException : Exception
{
    public InvalidGameStateException(string message) : base(message)
    {
    }

    public InvalidGameStateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
