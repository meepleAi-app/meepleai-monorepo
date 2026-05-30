using System.Security.Cryptography;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Issue #1691: regression coverage for the rejection-sampling implementation of
/// <see cref="DiceRoll.RollDiceCore"/>.
///
/// <para>
/// These tests do NOT replace <see cref="DiceRollFairnessTests"/> (chi-square statistical
/// fairness). They cover deterministic edge cases the statistical tests cannot:
/// <list type="bullet">
///   <item>The pre-#1691 code used <c>Math.Abs(BitConverter.ToInt32(...))</c>, which
///   throws on <c>int.MinValue</c> (~1 / 2³² of inputs) → latent prod crash.</item>
///   <item>The pre-#1691 code used <c>value % sides</c>, introducing modulo bias for
///   any <c>sides</c> that does not divide 2³¹ evenly.</item>
/// </list>
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "1691")]
public sealed class DiceRollRejectionSamplingTests
{
    /// <summary>
    /// Deterministic RNG that emits a pre-baked sequence of 4-byte values. Each call to
    /// <see cref="GetBytes(byte[])"/> writes the next <see cref="uint"/> in little-endian.
    /// When the sequence is exhausted it cycles. Cycling lets us test the rejection-sampling
    /// loop by following a "rejected" value with an "accepted" one.
    /// </summary>
    private sealed class SequenceRng : RandomNumberGenerator
    {
        private readonly uint[] _values;
        private int _index;

        public SequenceRng(params uint[] values)
        {
            if (values is null || values.Length == 0)
            {
                throw new ArgumentException("Sequence must contain at least one value", nameof(values));
            }
            _values = values;
        }

        public override void GetBytes(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length != 4)
            {
                throw new InvalidOperationException($"SequenceRng only supports 4-byte fills (got {data.Length})");
            }
            var value = _values[_index];
            _index = (_index + 1) % _values.Length;
            BitConverter.TryWriteBytes(data.AsSpan(), value);
        }

        public override void GetBytes(Span<byte> data)
        {
            if (data.Length != 4)
            {
                throw new InvalidOperationException($"SequenceRng only supports 4-byte fills (got {data.Length})");
            }
            var value = _values[_index];
            _index = (_index + 1) % _values.Length;
            BitConverter.TryWriteBytes(data, value);
        }
    }

    /// <summary>
    /// The pre-#1691 implementation did <c>Math.Abs(BitConverter.ToInt32(...))</c>; the byte
    /// sequence <c>{0x00, 0x00, 0x00, 0x80}</c> decodes to <see cref="int.MinValue"/>, and
    /// <c>Math.Abs(int.MinValue)</c> overflows. The new implementation reads the same bytes as
    /// <see cref="uint"/> (= 2_147_483_648) and rejection-samples — no exception is ever thrown.
    /// </summary>
    [Fact]
    public void RollDiceCore_BytesEncodeIntMinValue_DoesNotThrow_AndStaysInRange()
    {
        // 2_147_483_648 = 0x80000000 — the bit pattern of int.MinValue.
        var rng = new SequenceRng(0x80000000u);

        var result = DiceRoll.RollDiceCore(count: 1, sides: 6, rng: rng);

        result.Should().HaveCount(1);
        result[0].Should().BeInRange(1, 6);
    }

    /// <summary>
    /// Exercises the rejection loop. A value equal to (or above) the largest multiple of
    /// <c>sides</c> ≤ <see cref="uint.MaxValue"/> must be rejected; the next value in the
    /// sequence is used instead. For sides=6, limit = (2³² / 6) * 6 = 4_294_967_292 →
    /// values 4_294_967_292..4_294_967_295 are rejected.
    /// </summary>
    [Fact]
    public void RollDiceCore_ValueAtRejectionThreshold_IsResampled()
    {
        // First value 4_294_967_292 is the smallest rejected sample for sides=6.
        // Second value 0 is accepted → 0 % 6 + 1 = 1.
        var rng = new SequenceRng(4_294_967_292u, 0u);

        var result = DiceRoll.RollDiceCore(count: 1, sides: 6, rng: rng);

        result.Should().HaveCount(1);
        result[0].Should().Be(1, "the rejected first sample is replaced by the accepted second sample (0 % 6 + 1 = 1)");
    }

    /// <summary>
    /// Stress test: 1_000_000 rolls across every standard die size never throw and never
    /// produce out-of-range values. Acceptance criterion DICE-CRASH-01 in issue #1691.
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(20)]
    [InlineData(100)]
    public void RollDiceCore_OneMillionRolls_AllInRangeAndNoThrow(int sides)
    {
        using var rng = RandomNumberGenerator.Create();

        var result = DiceRoll.RollDiceCore(count: 1_000_000, sides: sides, rng: rng);

        result.Should().HaveCount(1_000_000);
        result.Should().OnlyContain(r => r >= 1 && r <= sides);
    }
}
