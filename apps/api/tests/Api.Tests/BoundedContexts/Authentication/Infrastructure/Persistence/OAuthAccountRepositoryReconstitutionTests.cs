using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// #4613: gli eventi che <see cref="OAuthAccountRepository"/> consegna al collector.
/// </summary>
/// <remarks>
/// <para>
/// <c>MapToDomain</c> ricostruiva la riga con il costruttore pubblico di <c>OAuthAccount</c>, che solleva
/// <c>OAuthAccountLinkedEvent</c>: ogni aggregato letto portava già un «collegato», e <c>UpdateAsync</c> lo
/// consegnava al collector insieme all'evento vero (rinnovo dei token).
/// </para>
/// <para>
/// I test di lettura prendono le colonne dal modello EF (<see cref="ModelDrivenRow"/>): una colonna aggiunta domani
/// all'entità senza mapping li fa fallire con il suo nome.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4613")]
public sealed class OAuthAccountRepositoryReconstitutionTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, (Func<OAuthAccountEntity, object?> Stored, Func<OAuthAccount, object?> Rebuilt)> NoViews =
        new Dictionary<string, (Func<OAuthAccountEntity, object?>, Func<OAuthAccount, object?>)>(StringComparer.Ordinal);

    private readonly MeepleAiDbContext _db;

    public OAuthAccountRepositoryReconstitutionTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"OAuthAccountRepo_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private (OAuthAccountRepository Repository, DomainEventCollector Collector) CreateRepository()
    {
        var collector = new DomainEventCollector();
        return (new OAuthAccountRepository(_db, collector, NullLogger<OAuthAccountRepository>.Instance), collector);
    }

    // Il provider deve essere uno di quelli ammessi, in minuscolo come lo scrive il costruttore. Serve anche
    // l'utente: le letture fanno Include(User), e per una relazione obbligatoria il provider InMemory fa un inner join.
    private async Task<OAuthAccountEntity> SeedEveryColumnAsync()
    {
        var user = new UserEntity { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@example.test" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        return await ModelDrivenRow.SeedEveryColumnAsync<OAuthAccountEntity>(_db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(OAuthAccountEntity.UserId)] = user.Id,
            [nameof(OAuthAccountEntity.Provider)] = "github",
        });
    }

    public static TheoryData<string> ReadPaths => new()
    {
        nameof(OAuthAccountRepository.GetByIdAsync),
        nameof(OAuthAccountRepository.GetByUserIdAndProviderAsync),
        nameof(OAuthAccountRepository.GetByUserIdAsync),
        nameof(OAuthAccountRepository.GetByProviderUserIdAsync),
        nameof(OAuthAccountRepository.GetAllAsync),
    };

    private static async Task<OAuthAccount> ReadAsync(OAuthAccountRepository repository, string readPath, OAuthAccountEntity seeded) => readPath switch
    {
        nameof(OAuthAccountRepository.GetByIdAsync) => (await repository.GetByIdAsync(seeded.Id))!,
        nameof(OAuthAccountRepository.GetByUserIdAndProviderAsync) => (await repository.GetByUserIdAndProviderAsync(seeded.UserId, seeded.Provider))!,
        nameof(OAuthAccountRepository.GetByUserIdAsync) => (await repository.GetByUserIdAsync(seeded.UserId)).Single(),
        nameof(OAuthAccountRepository.GetByProviderUserIdAsync) => (await repository.GetByProviderUserIdAsync(seeded.Provider, seeded.ProviderUserId))!,
        nameof(OAuthAccountRepository.GetAllAsync) => (await repository.GetAllAsync()).Single(),
        _ => throw new ArgumentOutOfRangeException(nameof(readPath), readPath, null),
    };

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReconstitutesEveryColumnOfTheRow(string readPath)
    {
        var seeded = await SeedEveryColumnAsync();

        var account = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        var lost = ModelDrivenRow.LostColumns(_db, seeded, account, NoViews, columnsToSkip: []);
        string.Join(Environment.NewLine, lost).Should().BeEmpty(
            "every column of the row must come back on the aggregate as it was stored");
    }

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task EveryReadPath_ReturnsAnAggregateWithNoPendingEvents(string readPath)
    {
        var seeded = await SeedEveryColumnAsync();

        var account = await ReadAsync(CreateRepository().Repository, readPath, seeded);

        account.DomainEvents.Should().BeEmpty(
            "a row that is read was linked long ago: rebuilding it must not raise OAuthAccountLinkedEvent, " +
            "or UpdateAsync hands it to the collector next to the event of the real change");
    }

    [Fact]
    public async Task UpdateAsync_AfterUpdateTokens_CollectsOnlyTheTokensRefreshedEvent()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var account = await repository.GetByIdAsync(seeded.Id);

        account!.UpdateTokens("token-nuovo", "refresh-nuovo", ModelDrivenRow.SentinelDate.AddYears(1));
        await repository.UpdateAsync(account);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<OAuthTokensRefreshedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_CollectsNothing()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, collector) = CreateRepository();
        var account = await repository.GetByIdAsync(seeded.Id);

        await repository.UpdateAsync(account!);

        collector.PeekEvents().Should().BeEmpty("nothing happened to the account, so there is nothing to record");
    }

    [Fact]
    public async Task UpdateAsync_WithNoChange_LeavesEveryColumnAsItWas()
    {
        var seeded = await SeedEveryColumnAsync();
        var (repository, _) = CreateRepository();
        var account = await repository.GetByIdAsync(seeded.Id);

        await repository.UpdateAsync(account!);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var after = await _db.OAuthAccounts.AsNoTracking().SingleAsync(a => a.Id == seeded.Id);
        var changed = ModelDrivenRow.Columns<OAuthAccountEntity>(_db)
            .Select(c => (Column: c.Name, Before: c.PropertyInfo!.GetValue(seeded), After: c.PropertyInfo.GetValue(after)))
            .Where(x => !Equals(x.Before, x.After))
            .Select(x => $"{x.Column}: «{x.Before}» → «{x.After}»");
        string.Join(Environment.NewLine, changed).Should().BeEmpty("an account read and saved unchanged must not lose a column");
    }

    [Fact]
    public async Task AddAsync_NewAccount_CollectsTheLinkedEvent()
    {
        var (repository, collector) = CreateRepository();
        var account = new OAuthAccount(Guid.NewGuid(), Guid.NewGuid(), "google", "google-user-1", "token");

        await repository.AddAsync(account);

        collector.PeekEvents().Should().ContainSingle().Which.Should().BeOfType<OAuthAccountLinkedEvent>();
    }
}
