using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration test for <see cref="CreateUserAgentCommandHandler"/> unique-name collisions — Issue #3234.
/// <para>
/// <c>AgentDefinition.Name</c> carries a global unique index that is NOT filtered by
/// <c>is_deleted</c> (<c>AgentDefinitionConfiguration</c>: <c>HasIndex(a =&gt; a.Name).IsUnique()</c>),
/// while a query filter hides soft-deleted rows. A colliding name therefore trips Postgres
/// <c>23505</c> at <c>SaveChangesAsync</c>. Before the fix this leaked as an unhandled
/// <c>DbUpdateException</c> (HTTP 500); the handler must translate it to a <see cref="ConflictException"/>
/// (HTTP 409). A pre-check cannot cover this on its own because the soft-deleted collider is
/// invisible through the query filter and a double-submit races the check.
/// </para>
/// This exercises the real unique index against Testcontainers Postgres — a mocked repository
/// cannot reproduce the 23505.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3234")]
public sealed class CreateUserAgentCommandHandlerConflictTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Guid _gameId = Guid.NewGuid();
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider? _serviceProvider;

    public CreateUserAgentCommandHandlerConflictTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider Sp => _serviceProvider ?? throw new InvalidOperationException("SP not initialized");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"create_user_agent_conflict_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(Ct);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_connectionString);

        // Real repository so AddAsync stages against the real DB (CreateBase defaults it to a mock).
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();

        // CreateBase does not register ISharedGameRepository; stub the name lookup so the handler
        // resolves the game name without needing a seeded SharedGame row. CanPerformAsync already
        // defaults to true in CreateBase, so the tier gate is open.
        var sharedGameMock = new Mock<ISharedGameRepository>();
        sharedGameMock
            .Setup(r => r.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [_gameId] = "Test Game" });
        services.AddScoped(_ => sharedGameMock.Object);

        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "Creating a user agent whose name collides with an existing agent throws ConflictException (409), not a leaked DbUpdateException (500)")]
    public async Task Handle_DuplicateName_ThrowsConflictException()
    {
        // Arrange: an agent with this name already exists.
        const string collidingName = "Collision Agent";
        var existing = AgentDefinition.Create(
            collidingName,
            "already here",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default());
        _dbContext.Set<AgentDefinition>().Add(existing);
        await _dbContext.SaveChangesAsync(Ct);

        var command = new CreateUserAgentCommand(
            UserId: Guid.NewGuid(),
            GameId: _gameId,
            AgentType: "rag",
            Name: collidingName);

        // Act
        using var scope = Sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = async () => await mediator.Send(command, Ct);

        // Assert: 409 conflict, not a leaked 500 (raw DbUpdateException).
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
    }
}
