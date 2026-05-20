using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

public sealed class GameBook
{
    public Guid Id { get; private set; }
    public GameRef GameRef { get; private set; } = default!;
    public Guid? OwnerUserId { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public GameBookRole Roles { get; private set; }
    public ParagraphScheme ParagraphScheme { get; private set; }
    public string Language { get; private set; } = default!;
    public bool SequentialRead { get; private set; }
    public Guid? KbSourceDocId { get; private set; }
    public bool PhysicalOnly { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private GameBook() { }

    public static GameBook CreateCommunity(
        GameRef gameRef, string displayName, GameBookRole roles,
        ParagraphScheme paragraphScheme, string language, bool sequentialRead,
        Guid? kbSourceDocId, bool physicalOnly, Guid createdBy)
    {
        return CreateInternal(gameRef, ownerUserId: null, displayName, roles,
            paragraphScheme, language, sequentialRead, kbSourceDocId, physicalOnly, createdBy);
    }

    public static GameBook CreatePersonal(
        GameRef gameRef, Guid ownerUserId, string displayName, GameBookRole roles,
        ParagraphScheme paragraphScheme, string language, bool sequentialRead,
        Guid? kbSourceDocId, bool physicalOnly)
    {
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("ownerUserId required", nameof(ownerUserId));

        return CreateInternal(gameRef, ownerUserId, displayName, roles,
            paragraphScheme, language, sequentialRead, kbSourceDocId, physicalOnly, ownerUserId);
    }

    private static GameBook CreateInternal(
        GameRef gameRef, Guid? ownerUserId, string displayName, GameBookRole roles,
        ParagraphScheme paragraphScheme, string language, bool sequentialRead,
        Guid? kbSourceDocId, bool physicalOnly, Guid createdBy)
    {
        ArgumentNullException.ThrowIfNull(gameRef);
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("displayName required", nameof(displayName));
        if (displayName.Length > 120)
            throw new ArgumentException("displayName max length 120", nameof(displayName));
        if (roles == GameBookRole.None)
            throw new ArgumentException("at least one role required", nameof(roles));
        if (string.IsNullOrWhiteSpace(language) || language.Length != 2)
            throw new ArgumentException("language must be ISO 639-1 (2 chars)", nameof(language));
        if (physicalOnly && kbSourceDocId.HasValue)
            throw new GameBookPhysicalCoherenceException(
                "physicalOnly=true implies kbSourceDocId must be null");

        var now = DateTimeOffset.UtcNow;
        return new GameBook
        {
            Id = Guid.NewGuid(),
            GameRef = gameRef,
            OwnerUserId = ownerUserId,
            DisplayName = displayName.Trim(),
            Roles = roles,
            ParagraphScheme = paragraphScheme,
            Language = language.ToLowerInvariant(),
            SequentialRead = sequentialRead,
            KbSourceDocId = kbSourceDocId,
            PhysicalOnly = physicalOnly,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy,
        };
    }

    public void AttachKbSource(Guid pdfDocId, Guid updatedBy)
    {
        if (PhysicalOnly)
            throw new GameBookPhysicalCoherenceException(
                "Cannot attach KB source to physical-only book. Call MarkAsIndexable first.");
        KbSourceDocId = pdfDocId;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void DetachKbSource(Guid updatedBy)
    {
        KbSourceDocId = null;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void UpdateRoles(GameBookRole newRoles, Guid updatedBy)
    {
        if (newRoles == GameBookRole.None)
            throw new ArgumentException("at least one role required", nameof(newRoles));
        Roles = newRoles;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Rename(string newName, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.Length > 120)
            throw new ArgumentException("invalid displayName", nameof(newName));
        DisplayName = newName.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void SoftDelete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DeletedAt.Value;
        UpdatedBy = deletedBy;
    }
}
