using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Una versione di <see cref="AgentProfile"/> (ADR-095 D2). Il numero di versione identifica sempre
/// lo stesso contenuto: solo una <see cref="AgentProfileVersionStatus.Draft"/> può cambiarlo, e solo
/// l'aggregato può far avanzare lo stato.
/// </summary>
public sealed class AgentProfileVersion : Entity<Guid>
{
    public Guid AgentProfileId { get; private set; }
    public int VersionNumber { get; private set; }
    public AgentProfileVersionStatus Status { get; private set; }
    public AgentProfileContent Content { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }

#pragma warning disable CS8618
    private AgentProfileVersion() : base() { } // EF Core
#pragma warning restore CS8618

    private AgentProfileVersion(Guid agentProfileId, int versionNumber, AgentProfileContent content, DateTime now)
        : base(Guid.NewGuid())
    {
        AgentProfileId = agentProfileId;
        VersionNumber = versionNumber;
        Content = content;
        Status = AgentProfileVersionStatus.Draft;
        CreatedAt = now;
    }

    internal static AgentProfileVersion NewDraft(Guid agentProfileId, int versionNumber, AgentProfileContent content, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new AgentProfileVersion(agentProfileId, versionNumber, content, now);
    }

    internal void ReplaceContent(AgentProfileContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureStatus(AgentProfileVersionStatus.Draft, "modify");
        Content = content;
    }

    internal void Publish(DateTime now)
    {
        EnsureStatus(AgentProfileVersionStatus.Draft, "publish");
        Status = AgentProfileVersionStatus.Published;
        PublishedAt = now;
    }

    internal void Archive(DateTime now)
    {
        EnsureStatus(AgentProfileVersionStatus.Published, "archive");
        Status = AgentProfileVersionStatus.Archived;
        ArchivedAt = now;
    }

    private void EnsureStatus(AgentProfileVersionStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new ConflictException(
                $"Cannot {action} agent profile version {VersionNumber}: it is {Status}, not {expected}.");
        }
    }
}
