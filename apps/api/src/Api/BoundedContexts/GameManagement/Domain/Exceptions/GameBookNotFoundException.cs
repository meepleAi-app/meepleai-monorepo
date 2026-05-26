namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

public sealed class GameBookNotFoundException : Exception
{
    public GameBookNotFoundException(Guid id)
        : base($"GameBook with id '{id}' not found.") { }
}
