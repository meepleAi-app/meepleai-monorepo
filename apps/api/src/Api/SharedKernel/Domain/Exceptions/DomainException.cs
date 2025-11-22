namespace Api.SharedKernel.Domain.Exceptions;

/// <summary>
/// Base exception for domain-related errors.
/// Thrown when a domain rule or invariant is violated.
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
