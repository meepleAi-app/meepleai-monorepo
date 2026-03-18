namespace Api.BoundedContexts.AgentMemory.Domain.Models;

/// <summary>
/// Represents a member of a play group (registered user or guest).
/// </summary>
internal sealed class GroupMember
{
    public Guid? UserId { get; set; }
    public string? GuestName { get; set; }
    public DateTime JoinedAt { get; set; }
}
