namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Discriminator for BggImportQueue items.
/// Determines whether an item is a full import (new game) or enrichment (existing skeleton).
/// </summary>
public enum BggQueueJobType
{
    /// <summary>
    /// Standard BGG import — creates a new SharedGame from BGG data.
    /// </summary>
    Import = 0,

    /// <summary>
    /// Enrichment of an existing skeleton SharedGame with BGG data.
    /// Requires SharedGameId to be set on the queue item.
    /// </summary>
    Enrichment = 1
}
