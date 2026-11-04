using System.Globalization;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Le righe figlie di una voce di libreria (sessioni, checklist, etichette assegnate) lette colonna
/// per colonna (#4505). L'elenco delle colonne viene dal modello EF, come nel round-trip della riga
/// radice (#4499): una colonna aggiunta domani entra nel confronto da sola.
/// </summary>
internal static class UserLibraryChildRows
{
    /// <summary>Ogni riga figlia della voce, per id, con tutte le sue colonne mappate.</summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, object?>>> ReadAsync<TChild>(
        UserLibraryMigratedDatabase database, Guid entryId)
        where TChild : class
    {
        await using var context = database.CreateDbContext();
        var rows = await context.Set<TChild>()
            .AsTracking()
            .Where(row => EF.Property<Guid>(row, "UserLibraryEntryId") == entryId)
            .ToListAsync();

        return rows
            .Select(context.Entry)
            .ToDictionary(
                entry => (Guid)entry.Property("Id").CurrentValue!,
                entry => (IReadOnlyDictionary<string, object?>)entry.Metadata.GetProperties()
                    .ToDictionary(p => p.Name, p => entry.Property(p.Name).CurrentValue, StringComparer.Ordinal));
    }

    /// <summary>
    /// Le differenze fra due letture delle stesse righe, una per riga di testo. Una riga sparita o
    /// comparsa è una differenza; <paramref name="changedOnPurpose"/> elenca le celle che il test ha
    /// cambiato apposta, come <c>(id, colonna)</c>.
    /// </summary>
    public static IReadOnlyList<string> Differences(
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, object?>> before,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, object?>> after,
        params (Guid Id, string Column)[] changedOnPurpose)
    {
        var problems = new List<string>();
        foreach (var (id, columnsBefore) in before)
        {
            if (!after.TryGetValue(id, out var columnsAfter))
            {
                problems.Add($"riga {id}: c'era, dopo l'update non c'è più");
                continue;
            }

            foreach (var (column, valueBefore) in columnsBefore)
            {
                if (changedOnPurpose.Contains((id, column)))
                {
                    continue;
                }

                if (!Equals(valueBefore, columnsAfter[column]))
                {
                    problems.Add($"riga {id}, {column}: prima {Show(valueBefore)}, dopo {Show(columnsAfter[column])}");
                }
            }
        }

        problems.AddRange(after.Keys.Where(id => !before.ContainsKey(id)).Select(id => $"riga {id}: non c'era, dopo l'update c'è"));
        return problems;
    }

    /// <summary>Le colonne di una riga che il seme ha lasciato al default, cioè non provate dal confronto.</summary>
    public static IEnumerable<string> ColumnsLeftAtDefault(IReadOnlyDictionary<string, object?> row) =>
        row.Where(c => c.Value is null
                || (c.Value.GetType().IsValueType && c.Value.Equals(Activator.CreateInstance(c.Value.GetType()))))
            .Select(c => c.Key);

    public static string Show(object? value) => value switch
    {
        null => "<null>",
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => $"'{value}'",
    };

    public static DateTime Utc(string iso) =>
        DateTime.SpecifyKind(DateTime.Parse(iso, CultureInfo.InvariantCulture), DateTimeKind.Utc);
}
