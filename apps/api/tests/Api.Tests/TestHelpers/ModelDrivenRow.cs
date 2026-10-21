using System.Reflection;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Una riga con ogni colonna valorizzata da una sentinella, prese dal modello EF e non da un elenco scritto a mano
/// (#4613, stessa idea di <c>ConfigurationRepositoryTests</c> e <c>AlertConfigurationRepositoryTests</c>).
/// </summary>
/// <remarks>
/// Una colonna aggiunta domani all'entità, senza mapping, fa fallire i test che la usano con il suo nome. Ogni
/// colonna riceve una sentinella <b>diversa</b> da quella delle altre dello stesso tipo, così anche uno scambio fra
/// due colonne nel mapper diventa rosso, e diversa dal valore che il costruttore pubblico darebbe di default
/// (l'ora è nel passato, una <c>Guid</c> è nuova, un <c>bool</c> è <c>true</c>). Dove la colonna ha un vincolo di
/// formato (JSON, stato, provider) o la sentinella coinciderebbe con il default, il test passa un valore suo.
/// </remarks>
internal static class ModelDrivenRow
{
    /// <summary>Nel passato, e diversa da ogni "adesso": una data ricostruita con UtcNow si vede.</summary>
    public static readonly DateTime SentinelDate = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    /// <summary>Le colonne scalari dell'entità, dal modello EF. Le navigazioni non sono colonne.</summary>
    public static IReadOnlyList<IProperty> Columns<TEntity>(MeepleAiDbContext db) =>
        db.Model.FindEntityType(typeof(TEntity))!
            .GetProperties()
            .Where(p => p.PropertyInfo is not null)
            .ToList();

    /// <summary>
    /// Crea e salva una riga con ogni colonna valorizzata. <paramref name="values"/> sostituisce la sentinella
    /// generica di una colonna per nome.
    /// </summary>
    public static async Task<TEntity> SeedEveryColumnAsync<TEntity>(
        MeepleAiDbContext db,
        IReadOnlyDictionary<string, object?> values)
        where TEntity : class
    {
        // Activator, non new(): OAuthAccountEntity ha membri `required`, che il vincolo new() non ammette.
        var seeded = (TEntity)Activator.CreateInstance(typeof(TEntity))!;
        var columns = Columns<TEntity>(db);
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].PropertyInfo!.SetValue(seeded, values.TryGetValue(columns[i].Name, out var own) ? own : SentinelFor(columns[i], i));
        }

        db.Set<TEntity>().Add(seeded);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return seeded;
    }

    /// <summary>
    /// Le colonne che non tornano sull'aggregato come erano state salvate, una per riga, con il nome.
    /// <paramref name="columnsToSkip"/> esclude quelle che il percorso di lettura non ricostruisce per scelta.
    /// <paramref name="views"/> dà, per una colonna che l'aggregato porta in altra forma (un JSON, un value object),
    /// i due lati da confrontare.
    /// </summary>
    public static IEnumerable<string> LostColumns<TEntity, TAggregate>(
        MeepleAiDbContext db,
        TEntity seeded,
        TAggregate aggregate,
        IReadOnlyDictionary<string, (Func<TEntity, object?> Stored, Func<TAggregate, object?> Rebuilt)> views,
        IReadOnlyCollection<string> columnsToSkip)
        where TEntity : class
    {
        foreach (var column in Columns<TEntity>(db).Where(c => !columnsToSkip.Contains(c.Name, StringComparer.Ordinal)))
        {
            object? expected;
            object? actual;
            if (views.TryGetValue(column.Name, out var view))
            {
                expected = view.Stored(seeded);
                actual = view.Rebuilt(aggregate);
            }
            else
            {
                expected = column.PropertyInfo!.GetValue(seeded);
                var property = typeof(TAggregate).GetProperty(column.Name, BindingFlags.Instance | BindingFlags.Public);
                actual = property is null ? "<assente dall'aggregato>" : property.GetValue(aggregate);
            }

            if (!Equals(expected, actual))
            {
                yield return $"{column.Name}: «{expected}» → «{actual}»";
            }
        }
    }

    private static object? SentinelFor(IProperty property, int index)
    {
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        return type switch
        {
            _ when type == typeof(string) => $"sentinella-{property.Name}",
            _ when type == typeof(int) => 7 + index,
            _ when type == typeof(bool) => true,
            _ when type == typeof(DateTime) => SentinelDate.AddDays(index),
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ => throw new InvalidOperationException(
                $"Colonna {property.Name} di tipo {property.ClrType.Name}: aggiungi una sentinella per questo tipo."),
        };
    }
}
