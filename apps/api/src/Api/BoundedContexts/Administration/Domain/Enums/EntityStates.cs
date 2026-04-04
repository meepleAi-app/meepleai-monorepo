namespace Api.BoundedContexts.Administration.Domain.Enums;

/// <summary>
/// Entity state enums for permission checks
/// Epic #4068 - Issue #4177
/// </summary>

/// <summary>
/// Game publication states
/// </summary>
public enum GamePublicationState
{
    Draft,
    Published,
    Archived
}

/// <summary>
/// Collection visibility states
/// </summary>
public enum CollectionVisibility
{
    Private,
    Shared,
    Public
}

/// <summary>
/// Document processing states
/// </summary>
public enum DocumentProcessingState
{
    Pending,
    Processing,
    Ready,
    Failed
}

