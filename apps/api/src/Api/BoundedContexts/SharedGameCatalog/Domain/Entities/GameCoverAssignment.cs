using Api.SharedKernel.Domain.Covers;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// An admin's per-context cover choice for a game (epic #3470). Pins which
/// <see cref="CoverAssignmentSource"/> is used for a given <see cref="CoverContext"/>
/// and the focal point of the crop (SD2). At most one assignment per
/// (game, context). Absence of an assignment falls back to the resolver's
/// implicit precedence (backward-compat). The generated per-context WebP key
/// is set once the crop has been rendered.
/// </summary>
public sealed class GameCoverAssignment : Entity<Guid>
{
    private Guid _id;
    private Guid _sharedGameId;
    private readonly CoverContext _context;
    private CoverAssignmentSource _source;
    private double _focalX;
    private double _focalY;
    private string? _generatedR2Key;
    private readonly DateTime _createdAt;
    private readonly Guid _createdBy;
    private DateTime? _updatedAt;
    private Guid? _updatedBy;

    /// <summary>Gets the unique identifier of this assignment.</summary>
    public new Guid Id => _id;

    /// <summary>Gets the ID of the game this assignment belongs to.</summary>
    public Guid SharedGameId => _sharedGameId;

    /// <summary>Gets the UI context this assignment targets.</summary>
    public CoverContext Context => _context;

    /// <summary>Gets the pinned cover source.</summary>
    public CoverAssignmentSource Source => _source;

    /// <summary>Gets the crop focal point X (0..1, 0.5 = center).</summary>
    public double FocalX => _focalX;

    /// <summary>Gets the crop focal point Y (0..1, 0.5 = center).</summary>
    public double FocalY => _focalY;

    /// <summary>Gets the R2 key of the rendered per-context WebP crop, if generated.</summary>
    public string? GeneratedR2Key => _generatedR2Key;

    /// <summary>Gets the creation timestamp.</summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>Gets the id of the admin who created this assignment.</summary>
    public Guid CreatedBy => _createdBy;

    /// <summary>Gets the last update timestamp.</summary>
    public DateTime? UpdatedAt => _updatedAt;

    /// <summary>Gets the id of the admin who last changed source/focal point.</summary>
    public Guid? UpdatedBy => _updatedBy;

    /// <summary>Parameterless constructor for EF Core.</summary>
    private GameCoverAssignment() : base()
    {
    }

    /// <summary>Internal constructor for reconstitution from persistence.</summary>
    internal GameCoverAssignment(
        Guid id,
        Guid sharedGameId,
        CoverContext context,
        CoverAssignmentSource source,
        double focalX,
        double focalY,
        string? generatedR2Key,
        DateTime createdAt,
        Guid createdBy,
        DateTime? updatedAt,
        Guid? updatedBy) : base(id)
    {
        _id = id;
        _sharedGameId = sharedGameId;
        _context = context;
        _source = source;
        _focalX = focalX;
        _focalY = focalY;
        _generatedR2Key = generatedR2Key;
        _createdAt = createdAt;
        _createdBy = createdBy;
        _updatedAt = updatedAt;
        _updatedBy = updatedBy;
    }

    /// <summary>Creates a new assignment with validation.</summary>
    public static GameCoverAssignment Create(
        Guid sharedGameId,
        CoverContext context,
        CoverAssignmentSource source,
        Guid createdBy,
        double focalX = 0.5,
        double focalY = 0.5)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        if (!Enum.IsDefined(context))
            throw new ArgumentException("Invalid CoverContext", nameof(context));

        if (!Enum.IsDefined(source))
            throw new ArgumentException("Invalid CoverAssignmentSource", nameof(source));

        EnsureFocalInRange(focalX, nameof(focalX));
        EnsureFocalInRange(focalY, nameof(focalY));

        return new GameCoverAssignment(
            Guid.NewGuid(),
            sharedGameId,
            context,
            source,
            focalX,
            focalY,
            generatedR2Key: null,
            DateTime.UtcNow,
            createdBy,
            updatedAt: null,
            updatedBy: null);
    }

    /// <summary>Repoints the assignment at a different source; clears the stale rendered crop.</summary>
    public void ChangeSource(CoverAssignmentSource source, Guid updatedBy)
    {
        if (!Enum.IsDefined(source))
            throw new ArgumentException("Invalid CoverAssignmentSource", nameof(source));

        if (updatedBy == Guid.Empty)
            throw new ArgumentException("UpdatedBy cannot be empty", nameof(updatedBy));

        if (_source == source)
            return;

        _source = source;
        _generatedR2Key = null;
        Touch(updatedBy);
    }

    /// <summary>Updates the crop focal point; clears the stale rendered crop.</summary>
    public void SetFocalPoint(double focalX, double focalY, Guid updatedBy)
    {
        EnsureFocalInRange(focalX, nameof(focalX));
        EnsureFocalInRange(focalY, nameof(focalY));

        if (updatedBy == Guid.Empty)
            throw new ArgumentException("UpdatedBy cannot be empty", nameof(updatedBy));

        _focalX = focalX;
        _focalY = focalY;
        _generatedR2Key = null;
        Touch(updatedBy);
    }

    /// <summary>
    /// Records the R2 key of the rendered per-context crop. This is a system
    /// operation (the rendering pipeline), so it stamps the timestamp but not an
    /// admin actor — <see cref="UpdatedBy"/> keeps the last human change.
    /// </summary>
    public void SetGeneratedKey(string generatedR2Key)
    {
        if (string.IsNullOrWhiteSpace(generatedR2Key))
            throw new ArgumentException("GeneratedR2Key is required", nameof(generatedR2Key));

        _generatedR2Key = generatedR2Key;
        _updatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Issue #3615 — drops the rendered crop because the BASE cover it was derived from changed
    /// underneath it (the pipeline regenerated the PDF/BGG/Wikidata image, or an admin replaced the
    /// manual one).
    ///
    /// <para>
    /// <see cref="ChangeSource"/> and <see cref="SetFocalPoint"/> already cover the «the admin
    /// changed their mind» case. This covers the opposite direction, where nobody touched the
    /// assignment at all: without it the crop keeps pointing at the old image indefinitely, since
    /// nothing regenerates it.
    /// </para>
    /// <para>
    /// Clearing the key is enough — the resolver falls back to the base cover when it is null, so
    /// the game shows the NEW image uncropped rather than a stale crop of the old one. The R2
    /// object is deliberately left in place: its key is deterministic
    /// (<c>covers/crops/{gameId}/social.webp</c>), so the next assignment overwrites it, and while
    /// unreferenced it costs storage but can never be served.
    /// </para>
    /// <para>
    /// A system operation like <see cref="SetGeneratedKey"/>: stamps the timestamp but leaves
    /// <see cref="UpdatedBy"/> on the last human change.
    /// </para>
    /// </summary>
    /// <returns><see langword="true"/> when a crop was actually dropped.</returns>
    internal bool InvalidateGeneratedCrop()
    {
        if (_generatedR2Key is null)
        {
            return false;
        }

        _generatedR2Key = null;
        _updatedAt = DateTime.UtcNow;
        return true;
    }

    private void Touch(Guid updatedBy)
    {
        _updatedAt = DateTime.UtcNow;
        _updatedBy = updatedBy;
    }

    private static void EnsureFocalInRange(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 || value > 1.0)
            throw new ArgumentOutOfRangeException(paramName, value, "Focal point must be a finite value in [0, 1].");
    }
}
