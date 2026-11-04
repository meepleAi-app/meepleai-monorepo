using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// #3839 — due letture del blocco utenti rispondevano 500 per ragioni che solo un database vero
/// mette in luce.
///
/// <list type="number">
/// <item>
/// <c>GetUserLibraryStatsQueryHandler</c> interrogava <c>Set&lt;UserLibraryEntry&gt;()</c>, cioe'
/// l'aggregato di dominio, che <c>MeepleAiDbContext</c> dichiara <c>Ignore&lt;&gt;()</c>: EF
/// solleva "Cannot create a DbSet for 'UserLibraryEntry' because this type is not included in the
/// model". Il tipo mappato e' <c>UserLibraryEntryEntity</c>.
/// </item>
/// <item>
/// <c>GetUserDetailedAiUsageQueryHandler</c> proiettava un <c>GroupBy</c> direttamente nel
/// costruttore di un DTO e poi ordinava su una proprieta' del DTO: EF non sa ritradurre quella
/// proprieta' nell'aggregato e la query intera diventa non traducibile.
/// </item>
/// </list>
///
/// Nessuno dei due si vede con un provider InMemory, che accetta qualunque LINQ e non ha un
/// modello relazionale da violare. Per questo il test gira su Postgres.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3839")]
public sealed class UserReadModelsAgainstRealDbTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private readonly Guid _userId = Guid.NewGuid();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UserReadModelsAgainstRealDbTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_user_read_models_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IMediator>(new Mock<IMediator>().Object);
        services.AddSingleton<IDomainEventCollector>(new Mock<IDomainEventCollector>().Object);
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        _dbContext = services.BuildServiceProvider().GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        // Una voce di libreria basta: senza, l'handler esce alla prima query e le altre quattro —
        // fra cui la SelectMany sulle sessioni — non verrebbero mai eseguite.
        //
        // Utente e gioco condiviso sono obbligatori, non contorno: user_library_entries ha una FK
        // verso entrambi e un CHECK (CK_UserLibraryEntry_GameSource) che pretende esattamente una
        // fra shared_game_id e private_game_id.
        var gameId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _userId,
            Email = $"lettura-{_userId:N}@meepleai.test",
            Tier = "free",
            Role = "user",
        });
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Gioco di prova",
        });
        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SharedGameId = gameId,
            AddedAt = DateTime.UtcNow.AddDays(-3),
            IsFavorite = true,
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task StatisticheLibreria_InterroganoIlTipoMappato()
    {
        var handler = new GetUserLibraryStatsQueryHandler(_dbContext!);

        var stats = await handler.Handle(new GetUserLibraryStatsQuery(_userId), TestCancellationToken);

        stats.Should().NotBeNull("l'utente ha una voce in libreria");
        stats!.TotalGames.Should().Be(1);
        stats.FavoriteGames.Should().Be(1);
        // Zero sessioni e' il valore giusto qui, ma il fatto che si arrivi a leggerlo prova che la
        // SelectMany sulla navigazione Sessions e' stata tradotta: e' la query che falliva.
        stats.SessionsPlayed.Should().Be(0);
        stats.OldestAddedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UsoAiDettagliato_SiTraduceInSql()
    {
        var handler = new GetUserDetailedAiUsageQueryHandler(
            _dbContext!,
            NullLogger<GetUserDetailedAiUsageQueryHandler>.Instance);

        var oggi = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = new GetUserDetailedAiUsageQuery(_userId, oggi.AddDays(-30), oggi);

        var azione = async () => await handler.Handle(query, TestCancellationToken);

        // Il difetto era in traduzione, non nei dati: si manifesta anche senza una sola riga di log.
        await azione.Should().NotThrowAsync(
            "il raggruppamento deve tradursi in SQL: proiettare nel costruttore del DTO e poi " +
            "ordinare su una sua proprieta' rende la query non traducibile (#3839)");
    }
}
