using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Junction entity linking a UserLibraryEntry to a GameLabel.
/// Represents the assignment of a label to a specific game in a user's library.
/// </summary>
internal sealed class UserGameLabel : Entity<Guid>
{
    /// <summary>
    /// The ID of the library entry this label is assigned to.
    /// </summary>
    public Guid UserLibraryEntryId { get; private set; }

    /// <summary>
    /// The ID of the label assigned to the game.
    /// </summary>
    public Guid LabelId { get; private set; }

    /// <summary>
    /// When this label was assigned to the game.
    /// </summary>
    public DateTime AssignedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private UserGameLabel() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new label assignment.
    /// </summary>
    private UserGameLabel(Guid id, Guid userLibraryEntryId, Guid labelId) : base(id)
    {
        if (userLibraryEntryId == Guid.Empty)
            throw new ArgumentException("UserLibraryEntryId cannot be empty", nameof(userLibraryEntryId));

        if (labelId == Guid.Empty)
            throw new ArgumentException("LabelId cannot be empty", nameof(labelId));

        UserLibraryEntryId = userLibraryEntryId;
        LabelId = labelId;
        AssignedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a new label assignment.
    /// </summary>
    public static UserGameLabel Create(Guid userLibraryEntryId, Guid labelId)
    {
        return new UserGameLabel(Guid.NewGuid(), userLibraryEntryId, labelId);
    }
}
