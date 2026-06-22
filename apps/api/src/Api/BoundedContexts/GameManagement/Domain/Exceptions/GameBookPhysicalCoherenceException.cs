namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

public sealed class GameBookPhysicalCoherenceException : Exception
{
    public GameBookPhysicalCoherenceException(string message) : base(message) { }
}
