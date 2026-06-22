using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// NFR-5: Verifica la fairness del RNG crittografico usato da <see cref="DiceRoll.Create"/>.
///
/// <para>
/// Metodo: Chi-square goodness-of-fit test sui tiri di un dado.
/// Gradi di libertà (df) = facce − 1, α = 0.05 → valore critico tabulato.
/// </para>
///
/// <para>
/// Il test è puramente unitario: non richiede DB né infrastruttura.
/// <see cref="DiceRoll.Create"/> usa <c>System.Security.Cryptography.RandomNumberGenerator</c>
/// (RNG crittografico) — nessun seed controllabile, quindi il test è statistico.
/// </para>
///
/// <para>
/// <b>Anti-flakiness (issue #1630):</b> un singolo chi-square con α = 0.05 fallisce
/// per definizione il 5 % delle volte anche con un dado perfettamente equo. Con due
/// asserzioni indipendenti la probabilità di un falso positivo per run è
/// 1 − 0.95² ≈ 9.75 %, abbastanza da rompere la CI su PR non correlate. Per questo
/// le asserzioni di distribuzione usano il <b>resampling</b> (vedi
/// <see cref="AssertChiSquareFairness"/>): più campioni indipendenti, fail solo se
/// <i>tutti</i> superano la soglia. Questo non indebolisce il test — un RNG realmente
/// biased fallisce sistematicamente tutti i campioni — ma porta il falso positivo da
/// α a α^attempts (≈ 0.0125 %).
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
    /// 6 000 tiri di 1d6: Chi-square &lt; 11.07 (α = 0.05, df = 5), con resampling.
    /// </summary>
    [Fact]
    public void RollDice_1d6_PassesChiSquareFairnessTest()
    {
        AssertChiSquareFairness(formula: "1d6", rolls: 6_000, sides: 6, criticalValue: 11.07);
    }

    /// <summary>
    /// 2 000 tiri di 1d20: Chi-square &lt; 30.14 (α = 0.05, df = 19), con resampling.
    /// </summary>
    [Fact]
    public void RollDice_1d20_PassesChiSquareFairnessTest()
    {
        AssertChiSquareFairness(formula: "1d20", rolls: 2_000, sides: 20, criticalValue: 30.14);
    }

    /// <summary>
    /// Verifica che i totali di 1 000 tiri di 2d6 ricadano nel range [2, 12].
    /// Complementare al chi-square: invariante deterministica, mai flaky.
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

    /// <summary>
    /// Esegue un chi-square goodness-of-fit con <b>resampling</b> per eliminare la
    /// flakiness intrinseca (issue #1630). Effettua fino a <paramref name="maxAttempts"/>
    /// campioni indipendenti (ogni <see cref="DiceRoll.Create"/> istanzia un nuovo RNG
    /// crittografico, quindi i campioni non sono correlati) e passa appena uno di essi
    /// rientra sotto la soglia. Fallisce solo se <i>tutti</i> i campioni la superano:
    /// per un dado equo P(tutti falliscono) ≈ α^maxAttempts, mentre un RNG realmente
    /// biased supera la soglia in modo sistematico e viene comunque catturato.
    /// </summary>
    private static void AssertChiSquareFairness(string formula, int rolls, int sides, double criticalValue, int maxAttempts = 3)
    {
        var observed = new List<double>(maxAttempts);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var counts = new int[sides + 1]; // index 1..sides
            for (var i = 0; i < rolls; i++)
            {
                var diceRoll = DiceRoll.Create(AnySessionId, AnyParticipantId, formula);
                counts[diceRoll.GetRolls()[0]]++;
            }

            var chiSquare = ComputeChiSquare(counts, sides, expectedPerFace: (double)rolls / sides);
            observed.Add(chiSquare);

            if (chiSquare < criticalValue)
                return; // un solo campione equo è sufficiente: distribuzione uniforme confermata
        }

        // Nessuno dei campioni indipendenti è rientrato sotto la soglia ⇒ bias sistematico,
        // non sfortuna statistica. Falliamo riportando tutti i valori per diagnostica.
        var best = observed.Min();
        best.Should().BeLessThan(criticalValue,
            because: $"il RNG crittografico deve produrre una distribuzione uniforme su {formula} " +
                     $"(df={sides - 1}, α=0.05, critical={criticalValue}); su {maxAttempts} campioni " +
                     $"di resampling indipendenti nessuno è rientrato sotto la soglia " +
                     $"(chi-square=[{string.Join(", ", observed.Select(v => v.ToString("F3")))}]). " +
                     $"Per un dado equo P(tutti falliscono) ≈ α^{maxAttempts} = {Math.Pow(0.05, maxAttempts):P4}, " +
                     $"quindi indica un bias reale del RNG, non rumore statistico (issue #1630).");
    }

    /// <summary>
    /// Chi-square goodness-of-fit: Σ (osservato − atteso)² / atteso, sulle facce 1..<paramref name="sides"/>.
    /// </summary>
    private static double ComputeChiSquare(int[] countsByFace, int sides, double expectedPerFace)
    {
        var chiSquare = 0.0;
        for (var face = 1; face <= sides; face++)
        {
            var diff = countsByFace[face] - expectedPerFace;
            chiSquare += diff * diff / expectedPerFace;
        }

        return chiSquare;
    }
}
