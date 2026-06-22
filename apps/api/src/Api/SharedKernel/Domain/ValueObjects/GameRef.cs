namespace Api.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Discriminated reference to either a <c>SharedGame</c> or a <c>PrivateGame</c>.
/// Replaces bare <c>Guid GameId</c> fields across bounded contexts (issue #1320).
/// Integrity is enforced at the application layer — there is no DB-side FK constraint
/// because the target table depends on <see cref="Kind"/>.
/// </summary>
public sealed record GameRef
{
    public GameRefKind Kind { get; }
    public Guid Id { get; }

    private GameRef(GameRefKind kind, Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("GameRef id cannot be empty", nameof(id));
        Kind = kind;
        Id = id;
    }

    public static GameRef Shared(Guid id) => new(GameRefKind.Shared, id);
    public static GameRef Private(Guid id) => new(GameRefKind.Private, id);

    public override string ToString() => $"{Kind}:{Id}";
}
