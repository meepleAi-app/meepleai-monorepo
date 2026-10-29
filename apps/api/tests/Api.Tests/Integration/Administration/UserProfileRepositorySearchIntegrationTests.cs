using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// #4727: the user search that any signed-in user can call. Against a real PostgreSQL, because the thing under
/// test is the ILIKE the in-memory provider cannot run: a partial email must match nobody, a whole email
/// matches that one account whatever its case, and <c>%</c> / <c>_</c> typed by the caller are characters,
/// not wildcards (a two-character <c>__</c> used to match every account).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4727")]
public sealed class UserProfileRepositorySearchIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IUserRepository? _users;
    private IUnitOfWork? _unitOfWork;
    private UserProfileRepository? _sut;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public UserProfileRepositorySearchIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_usersearch_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<IUserRepository, UserRepository>();
        var provider = services.BuildServiceProvider();

        _dbContext = provider.GetRequiredService<MeepleAiDbContext>();
        _users = provider.GetRequiredService<IUserRepository>();
        _unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(Ct);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, Ct);
            }
        }

        // UserProfile reads the view vw_user_profiles, which Program.cs creates at startup and no migration does.
        await _dbContext.Database.ExecuteSqlRawAsync(
            "CREATE OR REPLACE VIEW vw_user_profiles AS SELECT * FROM users;", Ct);

        _sut = new UserProfileRepository(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task SeedAsync(params (string Email, string DisplayName)[] people)
    {
        foreach (var (email, displayName) in people)
        {
            await _users!.AddAsync(
                new User(
                    id: Guid.NewGuid(),
                    email: new Email(email),
                    displayName: displayName,
                    passwordHash: PasswordHash.Create("UniqueT3stPwd!"),
                    role: Role.User,
                    tier: UserTier.Free),
                Ct);
        }

        await _unitOfWork!.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task SearchByDisplayName_MatchesPartiallyAndIgnoresCase()
    {
        await SeedAsync(("carla@example.com", "Carla Bianchi"), ("dario@example.com", "Dario Verdi"));

        var result = await _sut!.SearchByDisplayNameAsync("CARL", 10, Ct);

        result.Select(u => u.DisplayName).Should().Equal("Carla Bianchi");
    }

    [Fact]
    public async Task SearchByDisplayName_NeverMatchesTheEmailColumn()
    {
        await SeedAsync(("mario.rossi@gmail.com", "Mario"), ("anna@gmail.com", "Anna"));

        (await _sut!.SearchByDisplayNameAsync("gmail", 10, Ct)).Should().BeEmpty();
        (await _sut.SearchByDisplayNameAsync("rossi", 10, Ct)).Should().BeEmpty();
        (await _sut.SearchByDisplayNameAsync("mario.rossi@gmail.com", 10, Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByDisplayName_WildcardsTypedByTheCallerAreLiteralCharacters()
    {
        await SeedAsync(
            ("anna@example.com", "Anna"),
            ("bruno@example.com", "Bruno"),
            ("sicuro@example.com", "100%Sicuro"),
            ("trattino@example.com", "Sotto_linea"));

        // Two characters that used to be two wildcards, and so matched every name.
        (await _sut!.SearchByDisplayNameAsync("__", 10, Ct)).Should().BeEmpty();
        (await _sut.SearchByDisplayNameAsync("%%", 10, Ct)).Should().BeEmpty();

        // The same characters still find the names that really contain them.
        (await _sut.SearchByDisplayNameAsync("100%", 10, Ct)).Select(u => u.DisplayName).Should().Equal("100%Sicuro");
        (await _sut.SearchByDisplayNameAsync("o_l", 10, Ct)).Select(u => u.DisplayName).Should().Equal("Sotto_linea");
    }

    [Fact]
    public async Task SearchByDisplayName_AppliesTheLimitInNameOrder()
    {
        await SeedAsync(("c@example.com", "Utente C"), ("a@example.com", "Utente A"), ("b@example.com", "Utente B"));

        var result = await _sut!.SearchByDisplayNameAsync("utente", 2, Ct);

        result.Select(u => u.DisplayName).Should().Equal("Utente A", "Utente B");
    }

    [Fact]
    public async Task GetByEmail_FindsTheWholeAddressWhateverItsCase()
    {
        await SeedAsync(("carla@example.com", "Carla"), ("dario@example.com", "Dario"));

        var found = await _sut!.GetByEmailAsync("CARLA@Example.COM", Ct);

        found.Should().NotBeNull();
        found!.DisplayName.Should().Be("Carla");
    }

    [Fact]
    public async Task GetByEmail_AFragmentOrAPatternMatchesNobody()
    {
        await SeedAsync(("carla@example.com", "Carla"), ("dario@example.com", "Dario"));

        (await _sut!.GetByEmailAsync("carla@example", Ct)).Should().BeNull();
        (await _sut.GetByEmailAsync("carla@example.co", Ct)).Should().BeNull();
        (await _sut.GetByEmailAsync("carla%", Ct)).Should().BeNull();
        (await _sut.GetByEmailAsync("c_rla@example.com", Ct)).Should().BeNull();
        (await _sut.GetByEmailAsync("%@example.com", Ct)).Should().BeNull();
    }
}
