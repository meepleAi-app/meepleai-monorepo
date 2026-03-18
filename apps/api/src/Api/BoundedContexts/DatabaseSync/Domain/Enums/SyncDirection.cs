namespace Api.BoundedContexts.DatabaseSync.Domain.Enums;

/// <summary>
/// Direction for sync operations. PascalCase values match .NET default JSON serialization.
/// </summary>
internal enum SyncDirection
{
    LocalToStaging,
    StagingToLocal
}
