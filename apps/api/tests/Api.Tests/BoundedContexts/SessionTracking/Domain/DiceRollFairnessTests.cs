using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// NFR-5: Verifica la fairness del RNG crittografico usato da <see cref="DiceRoll.Create"/>.
///
/// <para>
/// Metodo: Chi-square goodness-of-fit test su 6 000 tiri di 1d6.
/// Expected frequency per faccia = 6000 / 6 = 1000.
/// Gradi di libertà (df) = 5, alpha = 0.05 → valore critico = 11.07.
/// </para>
///
/// <para>
/// Il test è puramente unitario: non richiede DB né infrastruttura.
/// <see cref="DiceRoll.Create"/> usa <c>System.Security.Cryptography.RandomNumberGenerator</c>
/// (RNG crittografico) — nessun seed controllabile, quindi il test è statistico
/// e probabilistico: la probabilità di falso positivo è &lt; 5 %.
/// In pratica, per un dado equo, il chi-square osservato è tipicamente &lt; 5.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "NFR-5-DiceRollFairness")]
public sealed class DiceRollFairnessTests
{
    private static readonly Guid AnySessionId = Guid.Parse("aaaaaaaa-0000-4000-8000-aaaaaaaaaaaa");
    private static readonly Guid AnyParticipantId = Guid.Parse("bbbbbbbb-0000-4000-8000-bbbbbbbbbbbb");

    /// <summary>
    /// 6 000 tiri di 1d6: Chi-square &lt; 11.07 (α = 0.05, df = 5).
    /// </summary>
    [Fact]
    public void RollDice_1d6_PassesChiSquareFairnessTest()
    {
        const int rolls = 6_000;
        const int sides = 6;
        const double expectedPerFace = (double)rolls / sides; // 1000.0
        const double criticalValue = 11.07;                   // α=0.05, df=5

        var counts = new int[sides + 1]; // index 1..6

        for (var i = 0; i < rolls; i++)
        {
            var diceRoll = DiceRoll.Create(AnySessionId, AnyParticipantId, "1d6");
            var result = diceRoll.GetRolls()[0];
            counts[result]++;
        }

        var chiSquare = 0.0;
        for (var face = 1; face <= sides; face++)
        {
            var diff = counts[face] - expectedPerFace;
            chiSquare += diff * diff / expectedPerFace;
        }

        chiSquare.Should().BeLessThan(criticalValue,
            because: $"il RNG crittografico deve produrre una distribuzione uniforme su 1d6 " +
                     $"(chi-square={chiSquare:F3}, critical={criticalValue}, df={sides - 1}). " +
                     $"Conteggi per faccia: [{string.Join(", ", counts[1..])}]");
    }

    /// <summary>
    /// 2 000 tiri di 1d20: Chi-square &lt; 30.14 (α = 0.05, df = 19).
    /// </summary>
    [Fact]
    public void RollDice_1d20_PassesChiSquareFairnessTest()
    {
        const int rolls = 2_000;
        const int sides = 20;
        const double expectedPerFace = (double)rolls / sides; // 100.0
        const double criticalValue = 30.14;                   // α=0.05, df=19

        var counts = new int[sides + 1]; // index 1..20

        for (var i = 0; i < rolls; i++)
        {
            var diceRoll = DiceRoll.Create(AnySessionId, AnyParticipantId, "1d20");
            var result = diceRoll.GetRolls()[0];
            counts[result]++;
        }

        var chiSquare = 0.0;
        for (var face = 1; face <= sides; face++)
        {
            var diff = counts[face] - expectedPerFace;
            chiSquare += diff * diff / expectedPerFace;
        }

        chiSquare.Should().BeLessThan(criticalValue,
            because: $"il RNG crittografico deve produrre una distribuzione uniforme su 1d20 " +
                     $"(chi-square={chiSquare:F3}, critical={criticalValue}, df={sides - 1}). " +
                     $"Conteggi per faccia: [{string.Join(", ", counts[1..])}]");
    }

    /// <summary>
    /// Verifica che i totali di 1 000 tiri di 2d6 ricadano nel range [2, 12].
    /// Complementare al chi-square: nessun risultato fuori bounds.
    /// </summary>
    [Fact]
    public void RollDice_2d6_TotalsAlwaysInRange()
    {
        for (var i = 0; i < 1_000; i++)
        {
            var diceRoll = DiceRoll.Create(AnySessionId, AnyParticipantId, "2d6");
            diceRoll.Total.Should().BeInRange(2, 12,
                because: "2d6 deve produrre totali tra 2 e 12");

            var individualRolls = diceRoll.GetRolls();
            individualRolls.Should().HaveCount(2);
            foreach (var roll in individualRolls)
                roll.Should().BeInRange(1, 6, because: "ogni dado deve essere tra 1 e 6");
        }
    }
}
