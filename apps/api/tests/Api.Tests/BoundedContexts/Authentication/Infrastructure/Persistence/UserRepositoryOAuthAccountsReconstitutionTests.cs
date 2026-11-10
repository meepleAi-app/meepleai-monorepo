using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// #4613: gli account OAuth che <see cref="UserRepository"/> ricostruisce dentro l'utente.
/// </summary>
/// <remarks>
/// <c>MapToDomain</c> ricostruiva ogni collegamento con il costruttore pubblico di <c>OAuthAccount</c>: data di
/// creazione e di aggiornamento ad "adesso" invece di quelle salvate, e un <c>OAuthAccountLinkedEvent</c> sul
/// figlio. L'evento non partiva mai (<c>CollectDomainEvents</c> guarda l'utente, non i figli, e i collegamenti si
/// salvano da <c>OAuthAccountRepository</c>), ma il gate <c>RepositoryReconstitutionArchitectureTests</c> lo vede, e le
/// date erano sbagliate.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4613")]
public sealed class UserRepositoryOAuthAccountsReconstitutionTests : IDisposable
{
    private readonly MeepleAiDbContext _db;

    public UserRepositoryOAuthAccountsReconstitutionTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"UserRepoOAuth_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetByIdAsync_RebuildsTheLinkedAccountsAsTheyWereStored_WithNoPendingEvent()
    {
        var user = new UserEntity { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@example.test" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();
        var seeded = await ModelDrivenRow.SeedEveryColumnAsync<OAuthAccountEntity>(_db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(OAuthAccountEntity.UserId)] = user.Id,
            [nameof(OAuthAccountEntity.Provider)] = "discord",
        });
        var repository = new UserRepository(_db, new DomainEventCollector());

        var loaded = await repository.GetByIdAsync(user.Id, TestContext.Current.CancellationToken);

        var account = loaded!.OAuthAccounts.Should().ContainSingle().Subject;
        account.DomainEvents.Should().BeEmpty("a link that is read was made long ago");
        account.CreatedAt.Should().Be(seeded.CreatedAt, "the constructor would have said now");
        account.UpdatedAt.Should().Be(seeded.UpdatedAt, "the constructor would have said now");
        account.Provider.Should().Be("discord");
        account.ProviderUserId.Should().Be(seeded.ProviderUserId);
    }
}
