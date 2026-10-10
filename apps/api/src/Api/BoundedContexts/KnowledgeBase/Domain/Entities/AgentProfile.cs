using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Il profilo dell'agente di sistema (ADR-095 D1): un aggregato singleton con versioni immutabili.
/// <para>
/// Invarianti di D2: al più una <see cref="AgentProfileVersionStatus.Draft"/>; esattamente una
/// <see cref="AgentProfileVersionStatus.Published"/>; una versione pubblicata o archiviata non si
/// modifica; il rollback crea una nuova versione, così il numero identifica sempre un contenuto.
/// </para>
/// </summary>
public sealed class AgentProfile : AggregateRoot<Guid>
{
    /// <summary>L'unico profilo di sistema. La tabella lo impone con un vincolo di check.</summary>
    public static readonly Guid SingletonId = new("a9e7f5c1-4167-4095-9a1e-000000000001");

    private readonly List<AgentProfileVersion> _versions = new();

    public IReadOnlyList<AgentProfileVersion> Versions => _versions.AsReadOnly();

    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Toccato da ogni comando, anche quando cambiano solo le versioni: fa avanzare lo
    /// <see cref="Xmin"/> della radice, così due pubblicazioni concorrenti non passano entrambe.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Concorrenza ottimistica sulla colonna di sistema <c>xmin</c> (ADR-060).</summary>
    public uint Xmin { get; private set; }

    public AgentProfileVersion Published =>
        _versions.Single(v => v.Status == AgentProfileVersionStatus.Published);

    public AgentProfileVersion? Draft =>
        _versions.SingleOrDefault(v => v.Status == AgentProfileVersionStatus.Draft);

#pragma warning disable CS8618
    private AgentProfile() : base() { } // EF Core
#pragma warning restore CS8618

    private AgentProfile(DateTime now) : base(SingletonId)
    {
        CreatedAt = now;
        UpdatedAt = now;

        // Una riga mai persistita non ha ancora un xmin; assegnarlo qui evita S1144 sul setter (#3688).
        Xmin = 0;
    }

    /// <summary>Il primo rilascio: la versione 1 nasce già pubblicata, senza bozza.</summary>
    public static AgentProfile CreateWithPublishedVersion(AgentProfileContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var now = DateTime.UtcNow;
        var profile = new AgentProfile(now);
        var first = AgentProfileVersion.NewDraft(profile.Id, 1, content, now);
        first.Publish(now);
        profile._versions.Add(first);
        return profile;
    }

    public AgentProfileVersion CreateDraft(AgentProfileContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (Draft is { } existing)
        {
            throw new ConflictException(
                $"Agent profile already has draft version {existing.VersionNumber}; update or publish it first.");
        }

        var now = Touch();
        var draft = AgentProfileVersion.NewDraft(Id, NextVersionNumber(), content, now);
        _versions.Add(draft);
        return draft;
    }

    public void UpdateDraft(AgentProfileContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var draft = Draft ?? throw new ConflictException(
            "Agent profile has no draft: published and archived versions cannot be modified.");

        Touch();
        draft.ReplaceContent(content);
    }

    public AgentProfileVersion PublishDraft()
    {
        var draft = Draft ?? throw new ConflictException("Agent profile has no draft to publish.");

        var now = Touch();
        Published.Archive(now);
        draft.Publish(now);
        return draft;
    }

    /// <summary>
    /// Ripubblica il contenuto di una versione archiviata come nuova versione. Un'eventuale bozza resta
    /// com'è: il rollback non cancella lavoro in corso.
    /// </summary>
    public AgentProfileVersion RollbackTo(int versionNumber)
    {
        var target = _versions.SingleOrDefault(v => v.VersionNumber == versionNumber)
            ?? throw new ValidationException(nameof(versionNumber), $"Agent profile version {versionNumber} does not exist.");
        if (target.Status != AgentProfileVersionStatus.Archived)
        {
            throw new ConflictException(
                $"Only archived versions can be rolled back to; version {versionNumber} is {target.Status}.");
        }

        var now = Touch();
        var restored = AgentProfileVersion.NewDraft(Id, NextVersionNumber(), target.Content, now);
        Published.Archive(now);
        restored.Publish(now);
        _versions.Add(restored);
        return restored;
    }

    private int NextVersionNumber() => _versions.Max(v => v.VersionNumber) + 1;

    private DateTime Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        return UpdatedAt;
    }
}
