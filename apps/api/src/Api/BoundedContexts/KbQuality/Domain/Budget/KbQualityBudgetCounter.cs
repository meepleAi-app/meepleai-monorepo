using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.KbQuality.Domain.Budget;

/// <summary>
/// Per-tenant per-calendar-month spent counter (A1 self-contained cost cap store).
///
/// <para>Composite PK <c>(TenantId, YearMonth)</c>; <c>YearMonth</c> is the canonical
/// <c>"yyyy-MM"</c> string so PostgreSQL's lexicographic compare aligns with
/// chronological order (used by the monthly reset job).</para>
///
/// <para>DDD: private setters + factory + domain mutator follow the project pattern
/// established by <c>DocumentEvaluationRun</c>. Il concurrency token <see cref="Xmin"/> (colonna di
/// sistema di Postgres, #3651) rende osservabili i conflitti sugli incrementi paralleli, che
/// <c>EvaluationRepository.IncrementSpentAsync</c> intercetta e riprova con una lettura fresca.
/// Prima di #3651 quel retry non veniva mai eseguito: il token era <c>bytea</c> e l'eccezione non
/// poteva essere sollevata.</para>
/// </summary>
public sealed class KbQualityBudgetCounter
{
    public Guid TenantId { get; private set; }
    public string YearMonth { get; private set; } = default!;
    public decimal SpentUsd { get; private set; }

    /// <summary>
    /// Concurrency token (#3651, ADR-060) sulla colonna di sistema <c>xmin</c>.
    ///
    /// <para>
    /// La forma precedente era <c>[Timestamp] byte[]?</c> con <c>.IsRowVersion()</c>, e il commento
    /// dichiarava «auto-mapped to Postgres xmin by Npgsql». <b>Non lo era</b>: su Npgsql
    /// <c>IsRowVersion()</c> su un <c>byte[]</c> genera una colonna <c>bytea</c>, che Postgres non
    /// popola. Il token restava <c>null</c>, EF confrontava <c>NULL = NULL</c> e nessun conflitto
    /// veniva mai rilevato.
    /// </para>
    /// <para>
    /// La conseguenza qui è concreta: <c>EvaluationRepository.IncrementSpentAsync</c> ha un retry
    /// loop su <c>DbUpdateConcurrencyException</c> che <b>non è mai entrato in funzione</b>, perché
    /// quell'eccezione non poteva essere sollevata. Due valutazioni concorrenti dello stesso tenant
    /// si sovrascrivevano l'incremento, e il tetto di spesa diventava superabile in silenzio.
    /// </para>
    /// <para>
    /// ⚠️ Il commento precedente citava come modello <c>PdfDocumentEntity.RowVersion</c> (#1802) e
    /// «the PhotoBatchUpload landmine»: entrambe erano rotte allo stesso modo, ed entrambe sono
    /// state convertite da #3651. È così che il difetto si è propagato — copiando la convenzione
    /// sbagliata credendola la cura.
    /// </para>
    /// </summary>
    public uint Xmin { get; private set; }

    // EF Core ctor
    private KbQualityBudgetCounter() { }

    public static KbQualityBudgetCounter Create(Guid tenantId, string yearMonth, decimal initialSpent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);
        if (initialSpent < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(initialSpent), "Initial spent must be >= 0");
        }

        return new KbQualityBudgetCounter
        {
            TenantId = tenantId,
            YearMonth = yearMonth,
            SpentUsd = initialSpent,
            // Assegnazione esplicita: il setter è altrimenti raggiunto solo dalla
            // materializzazione EF e S1144 farebbe fallire la build. È anche semanticamente vero —
            // una riga mai persistita non ha ancora un xmin (#3688 lo documenta).
            Xmin = 0,
        };
    }

    public void IncrementSpent(decimal amountUsd)
    {
        if (amountUsd < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amountUsd), "Increment must be >= 0");
        }

        SpentUsd += amountUsd;
    }
}
