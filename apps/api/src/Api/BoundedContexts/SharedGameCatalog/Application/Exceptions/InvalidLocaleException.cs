namespace Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;

/// <summary>
/// Thrown when a string fails to parse as a valid ISO 639-1 locale (optionally with
/// ISO 3166-1 regional suffix) — see <see cref="Domain.ValueObjects.Locale"/>.
/// Issue #2339 — sub-PR 1/3.
/// </summary>
public sealed class InvalidLocaleException : ArgumentException
{
    public InvalidLocaleException(string message) : base(message)
    {
    }
}
