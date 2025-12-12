using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// Junction table for chat threads and document collections.
/// Issue #2051: Tracks which document collections are selected for each chat thread
/// </summary>
[Table("chat_thread_collections")]
public class ChatThreadCollectionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChatThreadId { get; set; }
    public Guid CollectionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ChatThreadEntity ChatThread { get; set; } = default!;
    public DocumentCollectionEntity Collection { get; set; } = default!;
}
