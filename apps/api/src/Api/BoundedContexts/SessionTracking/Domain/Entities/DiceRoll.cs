using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Dice roll entity representing a dice roll in a session.
/// Supports standard dice types (d4, d6, d8, d10, d12, d20, d100) and formulas (2d6+3).
/// </summary>
public class DiceRoll
{
    /// <summary>
    /// Dice roll unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Session reference.
    /// </summary>
    public Guid SessionId { get; private set; }

    /// <summary>
    /// Participant who performed the roll.
    /// </summary>
    public Guid ParticipantId { get; private set; }

    /// <summary>
    /// Original formula used (e.g., "2d6+3", "1d20-2").
    /// </summary>
    [MaxLength(50)]
    public string Formula { get; private set; } = string.Empty;

    /// <summary>
    /// Optional label for the roll (e.g., "Attack roll", "Damage").
    /// </summary>
    [MaxLength(100)]
    public string? Label { get; private set; }

    /// <summary>
    /// Individual dice results (JSON array).
    /// Example: [4, 2, 6]
    /// </summary>
    public string Rolls { get; private set; } = "[]";

    /// <summary>
    /// Modifier applied to the roll (can be positive or negative).
    /// </summary>
    public int Modifier { get; private set; }

    /// <summary>
    /// Total result (sum of rolls + modifier).
    /// </summary>
    public int Total { get; private set; }

    /// <summary>
    /// When the roll occurred.
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Soft delete timestamp.
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private DiceRoll()
    {
    }

    /// <summary>
    /// Factory method to create a new dice roll from a formula.
    /// </summary>
    /// <param name="sessionId">Session where the roll occurs.</param>
    /// <param name="participantId">Participant performing the roll.</param>
    /// <param name="formula">Dice formula (e.g., "2d6+3").</param>
    /// <param name="label">Optional label for the roll.</param>
    /// <param name="rng">
    /// Optional source of randomness. When <c>null</c> (default) the process-wide
    /// cryptographic singleton is used. Tests can pass a deterministic
    /// <see cref="System.Security.Cryptography.RandomNumberGenerator"/> to assert
    /// exact roll outcomes (issue #1693).
    /// </param>
    /// <returns>New DiceRoll instance with results.</returns>
    public static DiceRoll Create(
        Guid sessionId,
        Guid participantId,
        string formula,
        string? label = null,
        System.Security.Cryptography.RandomNumberGenerator? rng = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (participantId == Guid.Empty)
            throw new ArgumentException("Participant ID cannot be empty.", nameof(participantId));

        if (string.IsNullOrWhiteSpace(formula))
            throw new ArgumentException("Formula cannot be empty.", nameof(formula));

        var parsed = DiceFormulaParser.Parse(formula);
        var rolls = RollDiceCore(parsed.Count, parsed.Sides, rng ?? SharedRng);
        var total = rolls.Sum() + parsed.Modifier;

        return new DiceRoll
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ParticipantId = participantId,
            Formula = formula.ToUpperInvariant(),
            Label = label,
            Rolls = JsonSerializer.Serialize(rolls),
            Modifier = parsed.Modifier,
            Total = total,
            Timestamp = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    /// <summary>
    /// Gets the individual roll results as an array.
    /// </summary>
    public int[] GetRolls()
    {
        return JsonSerializer.Deserialize<int[]>(Rolls) ?? [];
    }

    /// <summary>
    /// Soft deletes the dice roll.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Process-wide cryptographic RNG. <see cref="System.Security.Cryptography.RandomNumberGenerator"/>
    /// is thread-safe in .NET 6+, so a single shared instance avoids the per-roll allocation +
    /// kernel handle acquisition cost of <c>RandomNumberGenerator.Create()</c> (issue #1693).
    /// </summary>
    private static readonly System.Security.Cryptography.RandomNumberGenerator SharedRng
        = System.Security.Cryptography.RandomNumberGenerator.Create();

    /// <summary>
    /// Pure rolling logic exposed for deterministic testing.
    /// Uses rejection sampling to produce a uniform distribution over [1, sides] with
    /// zero modulo bias and no <c>Math.Abs(int.MinValue)</c> overflow (issue #1691).
    /// </summary>
    /// <param name="count">Number of dice to roll (must be ≥ 0).</param>
    /// <param name="sides">Number of sides per die (must be ≥ 1).</param>
    /// <param name="rng">Source of randomness. Must produce 4-byte fills via <see cref="System.Security.Cryptography.RandomNumberGenerator.GetBytes(byte[])"/>.</param>
    /// <returns>Array of <paramref name="count"/> integers, each in <c>[1, sides]</c>.</returns>
    internal static int[] RollDiceCore(int count, int sides, System.Security.Cryptography.RandomNumberGenerator rng)
    {
        // Largest multiple of `sides` that fits in [0, uint.MaxValue]. Values ≥ limit are
        // rejected and re-sampled to eliminate modulo bias. Rejection probability is
        // (uint.MaxValue + 1 - limit) / (uint.MaxValue + 1) ≤ (sides - 1) / 2^32 ≈ 10⁻⁹
        // for standard dice (d4..d100) → performance impact negligible.
        var limit = (uint.MaxValue / (uint)sides) * (uint)sides;

        var results = new int[count];
        Span<byte> buffer = stackalloc byte[4];

        for (int i = 0; i < count; i++)
        {
            uint value;
            do
            {
                rng.GetBytes(buffer);
                value = BitConverter.ToUInt32(buffer);
            } while (value >= limit);

            results[i] = (int)(value % (uint)sides) + 1;
        }

        return results;
    }
}

/// <summary>
/// Parses dice formulas like "2d6+3", "1d20-2", "d100".
/// </summary>
public static partial class DiceFormulaParser
{
    /// <summary>
    /// Supported dice types.
    /// </summary>
    public static readonly int[] SupportedSides = [4, 6, 8, 10, 12, 20, 100];

    /// <summary>
    /// Compiled regex for dice formula parsing (source-generated for performance and DoS safety).
    /// Pattern: [count]d[sides][+/-modifier]
    /// Examples: 2d6+3, 1d20-2, d100, 3d8
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(
        @"^(?<count>\d*)D(?<sides>\d+)(?<modifier>[+-]\d+)?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex DiceFormulaRegex();

    /// <summary>
    /// Parses a dice formula into its components.
    /// </summary>
    /// <param name="formula">Formula like "2d6+3" or "1d20-2".</param>
    /// <returns>Parsed components: count, sides, modifier.</returns>
    public static (int Count, int Sides, int Modifier) Parse(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new ArgumentException("Formula cannot be empty.", nameof(formula));

        // Normalize: uppercase, remove spaces
        formula = formula.ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);

        var match = DiceFormulaRegex().Match(formula);

        if (!match.Success)
            throw new ArgumentException($"Invalid dice formula: {formula}. Expected format: NdS or NdS+M (e.g., 2d6+3).", nameof(formula));

        var countStr = match.Groups["count"].Value;
        var sidesStr = match.Groups["sides"].Value;
        var modifierStr = match.Groups["modifier"].Value;

        // Parse count (default to 1 if not specified)
        var count = string.IsNullOrEmpty(countStr) ? 1 : int.Parse(countStr, System.Globalization.CultureInfo.InvariantCulture);

        // Validate count
        if (count < 1 || count > 100)
            throw new ArgumentException($"Dice count must be between 1 and 100. Got: {count}.", nameof(formula));

        // Parse sides
        var sides = int.Parse(sidesStr, System.Globalization.CultureInfo.InvariantCulture);

        // Validate sides (allow standard dice types)
        if (!SupportedSides.Contains(sides))
            throw new ArgumentException($"Unsupported dice type: d{sides}. Supported: d4, d6, d8, d10, d12, d20, d100.", nameof(formula));

        // Parse modifier (default to 0)
        var modifier = string.IsNullOrEmpty(modifierStr) ? 0 : int.Parse(modifierStr, System.Globalization.CultureInfo.InvariantCulture);

        // Validate modifier range
        if (modifier < -100 || modifier > 100)
            throw new ArgumentException($"Modifier must be between -100 and 100. Got: {modifier}.", nameof(formula));

        return (count, sides, modifier);
    }
}
