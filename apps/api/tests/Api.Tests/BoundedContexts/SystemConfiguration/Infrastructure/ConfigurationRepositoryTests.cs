using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;
using SystemConfigurationAggregate = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Infrastructure;

/// <summary>
/// #4608: gli eventi che <see cref="ConfigurationRepository"/> consegna al collector.
/// </summary>
/// <remarks>
/// <para>
/// <c>MapToDomain</c> ricostruiva la riga con il costruttore pubblico di <c>SystemConfiguration</c>, che
/// solleva <c>ConfigurationCreatedEvent</c>: ogni aggregato letto portava già un «creata», e
/// <c>UpdateAsync</c> lo consegnava insieme all'evento vero (update, activate, deactivate, rollback).
/// <c>DeleteAsync</c>, al contrario, non consegnava niente: il <c>ConfigurationDeletedEvent</c> di
/// <c>MarkAsDeleted()</c> restava sull'aggregato.
/// </para>
/// <para>
/// I test di lettura prendono le colonne dal modello EF, come
/// <c>AlertConfigurationRepositoryTests</c> (#4552): una colonna aggiunta domani all'entità senza mapping li fa
/// fallire con il suo nome. Ogni colonna riceve una sentinella diversa dalle altre dello stesso tipo e dal
/// valore che il costruttore darebbe di default.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4608")]
public sealed class ConfigurationRepositoryTests : IDisposable
{
    // Nel passato, e diversa da ogni "adesso": una data ricostruita con UtcNow si vede.
    private static readonly DateTime SentinelDate = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    // Sentinelle che devono differire dal valore che il costruttore darebbe di default: un IsActive letto
    // come true e un RequiresRestart letto come false passerebbero inosservati.
    private static readonly Dictionary<string, object?> LegalSentinels = new(StringComparer.Ordinal)
    {
        [nameof(SystemConfigurationEntity.IsActive)] = false,
        [nameof(SystemConfigurationEntity.RequiresRestart)] = true,
    };

    // Colonne della riga che l'aggregato non porta: le scrive solo RegisterCommandHandler (claim del bootstrap
    // admin, con ExecuteUpdate). Non fanno parte di ciò che la lettura ricostruisce, e UpdateAsync non deve
    // toccarle (#4612): la lista è quella del repository, non una copia, così il test non si può scollare.
    private static readonly IReadOnlyList<string> NotOwnedByTheAggregate =
        ConfigurationRepository.ColumnsNotOwnedByTheAggregate;

    private readonly MeepleAiDbContext _db;

    public ConfigurationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ConfigurationRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private (ConfigurationRepository Repository, DomainEventCollector Collector) CreateRepository()
    {
        var collector = new DomainEventCollector();
        return (new ConfigurationRepository(_db, collector), collector);
    }

    private IReadOnlyList<IProperty> OwnedColumns() =>
        _db.Model.FindEntityType(typeof(SystemConfigurationEntity))!
            .GetProperties()
            .Where(p => p.PropertyInfo is not null)
            .Where(p => !NotOwnedByTheAggregate.Contains(p.Name, StringComparer.Ordinal))
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
            // «sentinella-<colonna>» rispetta il formato di ConfigKey (lettere, cifre, trattino) e i limiti di lunghezza.
            _ when type == typeof(string) => $"sentinella-{property.Name}",
            _ when type == typeof(int) => 7 + index,
            _ when type == typeof(bool) => true,
            _ when type == typeof(DateTime) => SentinelDate.AddDays(index),
            _ when type == typeof(Guid) => Guid.NewGuid(),
            _ => throw new InvalidOperationException(
                $"Colonna {property.Name} di tipo {property.ClrType.Name}: aggiungi una sentinella per questo tipo."),
        };
    }

    private async Task<SystemConfigurationEntity> SeedEveryColumnAsync()
    {
        var seeded = new SystemConfigurationEntity();
        var columns = OwnedColumns();
        for (var i = 0; i < columns.Count; i++)
        {
            columns[i].PropertyInfo!.SetValue(seeded, SentinelFor(columns[i], i));
        }

        _db.Set<SystemConfigurationEntity>().Add(seeded);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return seeded;
    }

    // L'aggregato porta la chiave come ConfigKey, la riga come stringa.
    private static object? AsColumnValue(object? domainValue) =>
        domainValue is ConfigKey key ? key.Value : domainValue;

    // Manca GetActiveConfigurationsAsync: filtra su IsActive = true, e la sentinella di IsActive è false.
    public static TheoryData<string> ReadPaths => new()
    {
        nameof(ConfigurationRepository.GetByIdAsync),
        nameof(ConfigurationRepository.GetByKeyAsync),
        nameof(ConfigurationRepository.GetByIdsAsync),
        nameof(ConfigurationRepository.GetByKeysAsync),
        nameof(ConfigurationRepository.GetByCategoryAsync),
        nameof(ConfigurationRepository.GetAllAsync),
    };

    private static async Task<SystemConfigurationAggregate?> ReadAsync(
        ConfigurationRepository repository, string readPath, SystemConfigurationEntity seeded) => readPath switch
        {
            nameof(ConfigurationRepository.GetByIdAsync) => await repository.GetByIdAsync(seeded.Id),
            nameof(ConfigurationRepository.GetByKeyAsync) =>
                await repository.GetByKeyAsync(seeded.Key, environment: null, activeOnly: false),
            nameof(ConfigurationRepository.GetByIdsAsync) => (await repository.GetByIdsAsync([seeded.Id])).Single(),
            nameof(ConfigurationRepository.GetByKeysAsync) => (await repository.GetByKeysAsync([seeded.Key])).Single(),
            nameof(ConfigurationRepository.GetByCategoryAsync) => (await repository.GetByCategoryAsync(seeded.Category)).Single(),
            nameof(ConfigurationRepository.GetAllAsync) => (await repository.GetAllAsync()).Single(),
            _ => throw new ArgumentOutOfRangeException(nameof(readPath), readPath, null),
        };

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReconstitutesEveryColumnOfTheRow(string readPath)
    {
        var seeded = await SeedEveryColumnAsync();

        var config = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        config.Should().NotBeNull();
        var lost = OwnedColumns()
            .Select(c => (Column: c.Name, Expected: c.PropertyInfo!.GetValue(seeded), Domain: typeof(SystemConfigurationAggregate).GetProperty(c.Name)))
            .Select(x => (x.Column, x.Expected, Actual: x.Domain is null ? "<assente dall'aggregato>" : AsColumnValue(x.Domain.GetValue(config))))
            .Where(x => !Equals(x.Expected, x.Actual))
            .Select(x => $"{x.Column}: «{x.Expected}» → «{x.Actual}»");
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored");
    }

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReturnsAnAggregateWithNoPendingEvents(string readPath)
    {
        var seeded = await SeedEveryColumnAsync();

        var config = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        config!.DomainEvents.Should().BeEmpty(
            "a row that is read was created long ago: rebuilding it must not raise ConfigurationCreatedEvent, " +
            "or UpdateAsync hands it to the collector next to the event of the real change");
    }

    [Fact]
    public async Task UpdateAsync_AfterUpdateValue_CollectsOnlyTheUpdatedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var config = await repository.GetByIdAsync(seeded.Id);

        config!.UpdateValue("valore-nuovo", Guid.NewGuid());
        await repository.UpdateAsync(config);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<ConfigurationUpdatedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_CollectsNothing()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var config = await repository.GetByIdAsync(seeded.Id);

        await repository.UpdateAsync(config!);

        collector.PeekEvents().Should().BeEmpty("nothing happened to the configuration, so there is nothing to record");
    }

    [Fact]
    public async Task UpdateAsync_AfterActivate_CollectsOnlyTheToggledEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        seeded.IsActive.Should().BeFalse("setup: the sentinel row is inactive, so Activate is the change");
        var (repository, collector) = CreateRepository();
        var config = await repository.GetByIdAsync(seeded.Id);

        config!.Activate();
        await repository.UpdateAsync(config);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<ConfigurationToggledEvent>();
    }

    [Fact]
    public async Task DeleteAsync_AfterMarkAsDeleted_CollectsTheDeletedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var config = await repository.GetByIdAsync(seeded.Id);

        config!.MarkAsDeleted();
        await repository.DeleteAsync(config);

        var deleted = collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<ConfigurationDeletedEvent>().Subject;
        deleted.ConfigurationId.Should().Be(seeded.Id);
    }

    // ----- #4612: le colonne che l'aggregato non porta -----

    // Sentinelle diverse dal default dell'entità (false / NULL): un valore riportato al default si vede.
    private static readonly DateTime BootstrapClaimedAt = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    private async Task<SystemConfigurationEntity> SeedClaimedBootstrapRowAsync()
    {
        var seeded = await SeedEveryColumnAsync();
        seeded.BootstrapAdminCreated = true;
        seeded.BootstrapAdminCreatedAt = BootstrapClaimedAt;
        _db.Set<SystemConfigurationEntity>().Update(seeded);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return seeded;
    }

    public static TheoryData<string> AggregateChanges => new()
    {
        "update-value",
        "activate",
        "rollback",
    };

    private static void Change(SystemConfigurationAggregate config, string change)
    {
        switch (change)
        {
            case "update-value":
                config.UpdateValue("valore-nuovo", Guid.NewGuid());
                break;
            case "activate":
                config.Activate();
                break;
            case "rollback":
                config.Rollback(Guid.NewGuid());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change), change, null);
        }
    }

    [Theory]
    [MemberData(nameof(AggregateChanges))]
    public async Task UpdateAsync_AfterAnyAggregateChange_LeavesTheColumnsTheAggregateDoesNotOwnAsTheyWere(string change)
    {
        var seeded = await SeedClaimedBootstrapRowAsync();
        var (repository, _) = CreateRepository();
        var config = await repository.GetByIdAsync(seeded.Id);

        Change(config!, change);
        await repository.UpdateAsync(config!);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.Set<SystemConfigurationEntity>().AsNoTracking().SingleAsync(c => c.Id == seeded.Id);
        after.UpdatedAt.Should().BeAfter(seeded.UpdatedAt, "setup: the change reached the row, so UpdateAsync did write");
        var reset = NotOwnedByTheAggregate
            .Select(name => (Name: name, Expected: Column(seeded, name), Actual: Column(after, name)))
            .Where(x => !Equals(x.Expected, x.Actual))
            .Select(x => $"{x.Name}: «{x.Expected}» → «{x.Actual}»");
        string.Join(Environment.NewLine, reset).Should().BeEmpty(
            "the bootstrap flag closes the first-admin claim for good: an update of the row must not reopen it");
    }

    [Fact]
    public async Task UpdateAsync_MarksNoColumnNotOwnedByTheAggregateAsModified()
    {
        var seeded = await SeedClaimedBootstrapRowAsync();
        var (repository, _) = CreateRepository();
        var config = await repository.GetByIdAsync(seeded.Id);
        config!.UpdateValue("valore-nuovo", Guid.NewGuid());

        await repository.UpdateAsync(config);

        var entry = _db.ChangeTracker.Entries<SystemConfigurationEntity>().Single();
        entry.Property(nameof(SystemConfigurationEntity.Value)).IsModified.Should().BeTrue(
            "control: DbSet.Update marks every column, so a column left unmarked was unmarked on purpose");
        var marked = NotOwnedByTheAggregate.Where(name => entry.Property(name).IsModified).ToList();
        marked.Should().BeEmpty("these columns belong to RegisterCommandHandler, not to the aggregate");
    }

    [Fact]
    public void ColumnsNotOwnedByTheAggregate_AreNotPropertiesOfTheAggregate()
    {
        // Una colonna che l'aggregato porta e che il mapper scrive non va esclusa: le sue modifiche andrebbero
        // perse in silenzio.
        var ownedByTheAggregate = NotOwnedByTheAggregate
            .Where(name => typeof(SystemConfigurationAggregate).GetProperty(name) is not null)
            .ToList();

        ownedByTheAggregate.Should().BeEmpty();
    }

    [Fact]
    public void ColumnsNotOwnedByTheAggregate_AreColumnsOfTheRow()
    {
        var columns = _db.Model.FindEntityType(typeof(SystemConfigurationEntity))!
            .GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        NotOwnedByTheAggregate.Should().OnlyContain(name => columns.Contains(name));
    }

    private static object? Column(SystemConfigurationEntity entity, string name) =>
        typeof(SystemConfigurationEntity).GetProperty(name)!.GetValue(entity);

    [Fact]
    public async Task AddAsync_NewConfiguration_CollectsTheCreatedEvent()
    {
        var (repository, collector) = CreateRepository();
        var config = new SystemConfigurationAggregate(
            Guid.NewGuid(), new ConfigKey("test.created"), "v", "string", Guid.NewGuid());

        await repository.AddAsync(config);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<ConfigurationCreatedEvent>();
    }
}
