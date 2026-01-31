namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when a requested resource is not found.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    /// Gets the type of the resource that was not found (e.g., "Game", "User").
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// Gets the identifier of the resource that was not found.
    /// </summary>
    public string? ResourceId { get; }

    public NotFoundException()
    {
    }

    [SetsRequiredMembers]
    public NotFoundException(string resourceType, string? resourceId = null)
        : base(FormatMessage(resourceType, resourceId))
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }

    [SetsRequiredMembers]
    public NotFoundException(string resourceType, string? resourceId, Exception innerException)
        : base(FormatMessage(resourceType, resourceId), innerException)
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }

    private static string FormatMessage(string resourceType, string? resourceId)
    {
        return resourceId is not null
            ? $"{resourceType} with identifier '{resourceId}' was not found"
            : $"{resourceType} not found";
    }
}
