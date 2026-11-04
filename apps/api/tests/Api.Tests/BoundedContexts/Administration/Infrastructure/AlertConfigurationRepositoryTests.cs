using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using Api.BoundedContexts.Administration.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// #4552: <see cref="AlertConfigurationRepository"/> ricostruiva la riga con <see cref="AlertConfiguration.Create"/>,
/// la factory di una configurazione nuova: id nuovo, <c>UpdatedAt</c> ad "adesso", <c>IsEncrypted</c> a
/// <c>false</c>. L'aggregato letto aveva quindi un id che nel database non esiste, e
/// <see cref="AlertConfigurationRepository.UpdateAsync"/>, che cerca la riga per id, lanciava
/// <see cref="InvalidOperationException"/>: la modifica di una configurazione esistente falliva sempre.
/// <para>
/// I test non elencano le colonne: le prendono dal modello EF. Una colonna aggiunta domani all'entità senza
/// mapping in entrambe le direzioni li fa fallire con il suo nome. Ogni colonna riceve una sentinella
/// <b>diversa</b> da quella delle altre colonne dello stesso tipo, così anche uno scambio fra due colonne nel
/// mapper diventa rosso.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4552")]
public sealed class AlertConfigurationRepositoryTests : IDisposable
{
    // Nel passato, e diversa da ogni "adesso": una data ricostruita con UtcNow si vede.
    private static readonly DateTime SentinelDate = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    // Valori che l'aggregato legge come tipo di dominio: la sentinella deve essere ammessa, e diversa dal
    // primo valore dell'enum, così una colonna riportata al default si vede.
    private static readonly Dictionary<string, object?> LegalSentinels = new(StringComparer.Ordinal)
    {
        [nameof(AlertConfigurationEntity.Category)] = ConfigCategory.PagerDuty.ToDisplayString(),
    };

    private readonly MeepleAiDbContext _db;

    public AlertConfigurationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"AlertConfigurationRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private AlertConfigurationRepository CreateRepository() =>
        new(_db, new Mock<IDomainEventCollector>().Object);

    private IReadOnlyList<IProperty> Columns() =>
        _db.Model.FindEntityType(typeof(AlertConfigurationEntity))!
            .GetProperties()
            .Where(p => p.PropertyInfo is not null)
            .ToList();

    private static object? SentinelFor(IProperty property, int index)
    {
        if (LegalSentinels.TryGetValue(property.Name, out var legal))
        {
            return legal;
        }

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

    private async Task<AlertConfigurationEntity> SeedEveryColumnAsync()
    {
        var seeded = new AlertConfigurationEntity { ConfigKey = "", ConfigValue = "", Category = "", UpdatedBy = "" };
        var columns = Columns();
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].PropertyInfo!.SetValue(seeded, SentinelFor(columns[i], i));
        }

        _db.AlertConfigurations.Add(seeded);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return seeded;
    }

    // L'aggregato porta la categoria come enum, la riga come stringa.
    private static object? AsColumnValue(object? domainValue) =>
        domainValue is ConfigCategory category ? category.ToDisplayString() : domainValue;

    public static TheoryData<string> ReadPaths => new()
    {
        nameof(AlertConfigurationRepository.GetByIdAsync),
        nameof(AlertConfigurationRepository.GetByKeyAsync),
        nameof(AlertConfigurationRepository.GetByCategoryAsync),
        nameof(AlertConfigurationRepository.GetAllAsync),
    };

    private static async Task<AlertConfiguration?> ReadAsync(
        AlertConfigurationRepository repository, string readPath, AlertConfigurationEntity seeded) => readPath switch
        {
            nameof(AlertConfigurationRepository.GetByIdAsync) => await repository.GetByIdAsync(seeded.Id),
            nameof(AlertConfigurationRepository.GetByKeyAsync) => await repository.GetByKeyAsync(seeded.ConfigKey),
            nameof(AlertConfigurationRepository.GetByCategoryAsync) =>
                (await repository.GetByCategoryAsync(ConfigCategoryExtensions.FromString(seeded.Category))).Single(),
            nameof(AlertConfigurationRepository.GetAllAsync) => (await repository.GetAllAsync()).Single(),
            _ => throw new ArgumentOutOfRangeException(nameof(readPath), readPath, null),
        };

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReconstitutesEveryColumnOfTheRow(string readPath)
    {
        var seeded = await SeedEveryColumnAsync();

        var config = await ReadAsync(CreateRepository(), readPath, seeded);

        config.Should().NotBeNull();
        var lost = Columns()
            .Select(c => (Column: c.Name, Expected: c.PropertyInfo!.GetValue(seeded), Domain: typeof(AlertConfiguration).GetProperty(c.Name)))
            .Select(x => (x.Column, x.Expected, Actual: x.Domain is null ? "<assente dall'aggregato>" : AsColumnValue(x.Domain.GetValue(config))))
            .Where(x => !Equals(x.Expected, x.Actual))
            .Select(x => $"{x.Column}: «{x.Expected}» → «{x.Actual}»");
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored: an aggregate built by " +
            "the factory of a NEW configuration has a new id, the current time and IsEncrypted = false");
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_LeavesEveryColumnAsItWas()
    {
        var seeded = await SeedEveryColumnAsync();
        var expected = Columns().ToDictionary(c => c.Name, c => c.PropertyInfo!.GetValue(seeded), StringComparer.Ordinal);

        var repository = CreateRepository();
        var config = await repository.GetByKeyAsync(seeded.ConfigKey);
        await repository.UpdateAsync(config!);
        _db.ChangeTracker.Clear();

        var after = await _db.AlertConfigurations.AsNoTracking().SingleAsync(c => c.ConfigKey == seeded.ConfigKey);
        var changed = Columns()
            .Where(c => !Equals(expected[c.Name], c.PropertyInfo!.GetValue(after)))
            .Select(c => $"{c.Name}: «{expected[c.Name]}» → «{c.PropertyInfo!.GetValue(after)}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty(
            "reading a configuration and saving it back unchanged must change nothing. Before #4552 the read " +
            "aggregate had a new id, so UpdateAsync did not find the row and threw");
    }

    [Fact]
    public async Task UpdateValue_ThenUpdateAsync_WritesValueTimeAuthorAndClearsTheEncryptionFlag()
    {
        var seeded = await SeedEveryColumnAsync();
        var expected = Columns().ToDictionary(c => c.Name, c => c.PropertyInfo!.GetValue(seeded), StringComparer.Ordinal);

        var repository = CreateRepository();
        var config = await repository.GetByKeyAsync(seeded.ConfigKey);
        config!.UpdateValue("valore-nuovo", "autore-nuovo");
        await repository.UpdateAsync(config);
        _db.ChangeTracker.Clear();

        var after = await _db.AlertConfigurations.AsNoTracking().SingleAsync(c => c.ConfigKey == seeded.ConfigKey);
        after.ConfigValue.Should().Be("valore-nuovo");
        after.UpdatedBy.Should().Be("autore-nuovo");
        after.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        after.IsEncrypted.Should().BeFalse("the new value is plain text: the stored row was encrypted, the new one is not");

        // UpdateValue cambia valore, flag di cifratura, data e autore. Tutto il resto il repository lo riscrive
        // com'era: è l'aggregato a decidere cosa cambia, non la factory usata per leggerlo.
        var untouched = new[]
        {
            nameof(AlertConfigurationEntity.ConfigValue),
            nameof(AlertConfigurationEntity.IsEncrypted),
            nameof(AlertConfigurationEntity.UpdatedBy),
            nameof(AlertConfigurationEntity.UpdatedAt),
        };
        var changed = Columns()
            .Where(c => !untouched.Contains(c.Name, StringComparer.Ordinal))
            .Where(c => !Equals(expected[c.Name], c.PropertyInfo!.GetValue(after)))
            .Select(c => $"{c.Name}: «{expected[c.Name]}» → «{c.PropertyInfo!.GetValue(after)}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty();
    }
}
