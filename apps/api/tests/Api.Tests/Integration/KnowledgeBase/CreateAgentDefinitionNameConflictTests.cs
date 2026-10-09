using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Unique-name collisions on <c>CreateAgentDefinitionCommand</c> — the admin creation path.
/// <para>
/// Issue #3234 covered this on <c>CreateUserAgentCommandHandler</c>, retired by #4138 with the
/// user-facing creation routes. The translation it carried does NOT belong to that flow — it
/// belongs to the unique index, which is still here:
/// </para>
/// <list type="bullet">
///   <item><c>AgentDefinitionConfiguration</c>: <c>HasIndex(a =&gt; a.Name).IsUnique()</c> — NOT
///   filtered by <c>is_deleted</c>.</item>
///   <item>the same configuration: <c>HasQueryFilter(a =&gt; !a.IsDeleted)</c>, which
///   <c>AgentDefinitionRepository.ExistsAsync</c> inherits.</item>
/// </list>
/// <para>
/// So the handler's <c>ExistsAsync</c> pre-check cannot see a soft-deleted collider while the
/// index still rejects it: Postgres answers <c>23505</c> at <c>SaveChangesAsync</c>, which leaked
/// as an unhandled <c>DbUpdateException</c> (HTTP 500, cf. #2568) until #4138 ported the
/// translation across.
/// </para>
/// <para>
/// ⚠️ The #3234 test used an ACTIVE collider, which was sound there — that handler had no
/// pre-check, so 23505 was the only path. Ported verbatim it would have proved nothing here: the
/// admin handler's pre-check catches an active collider and never reaches the database. The
/// soft-deleted collider is what exercises the index. Both cases are asserted below, because they
/// are two different mechanisms reaching the same status code.
/// </para>
/// <para>
/// Exercises the real index against Testcontainers Postgres; a mocked repository cannot reproduce
/// a 23505.
/// </para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4138")]
public sealed class CreateAgentDefinitionNameConflictTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private IServiceProvider? _serviceProvider;

    public CreateAgentDefinitionNameConflictTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider Sp => _serviceProvider ?? throw new InvalidOperationException("SP not initialized");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"agent_def_name_conflict_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(Ct);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_connectionString);

        // Real repository so AddAsync stages against the real DB and ExistsAsync runs the real
        // filtered query (CreateBase defaults it to a mock).
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();

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

    // 'deepseek-chat' rather than a bare cloud id: the validator rejects unroutable models
    // (#4102), so a 'gpt-4' here would fail for the wrong reason.
    private static CreateAgentDefinitionCommand Command(string name) => new(
        Name: name,
        Description: "conflict probe",
        Type: "RAG",
        Model: "deepseek-chat",
        MaxTokens: 2048,
        Temperature: 0.7f);

    private async Task<AgentDefinition> SeedAgentAsync(string name, bool softDeleted)
    {
        var agent = AgentDefinition.Create(
            name,
            "already here",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default());

        if (softDeleted)
            agent.SoftDelete();

        _dbContext.Set<AgentDefinition>().Add(agent);
        await _dbContext.SaveChangesAsync(Ct);
        return agent;
    }

    /// <summary>
    /// The case the pre-check cannot reach. Without the <c>DbUpdateException</c> translation this
    /// fails with a raw <c>DbUpdateException</c>, not a <c>ConflictException</c> — that is the
    /// perturbation that proves the test is load-bearing.
    /// </summary>
    [Fact(DisplayName = "A name colliding with a SOFT-DELETED agent gives ConflictException (409), not a leaked DbUpdateException (500)")]
    public async Task Handle_NameCollidesWithSoftDeletedAgent_ThrowsConflictException()
    {
        const string collidingName = "Soft Deleted Collider";
        var buried = await SeedAgentAsync(collidingName, softDeleted: true);

        // The collider is invisible to the handler's pre-check...
        using var scope = Sp.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAgentDefinitionRepository>();
        (await repository.ExistsAsync(collidingName, Ct))
            .Should().BeFalse("the !IsDeleted query filter hides it — this is why the pre-check cannot cover this case");

        // ...but not to the unique index.
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = async () => await mediator.Send(Command(collidingName), Ct);

        (await act.Should().ThrowAsync<ConflictException>())
            .WithMessage("*already exists*")
            .And.InnerException.Should().BeOfType<DbUpdateException>(
                "the 409 must come from translating the DB violation, not from re-running the pre-check");

        buried.IsDeleted.Should().BeTrue("the probe must not have resurrected it");
    }

    /// <summary>
    /// The pre-check's own case. Asserted so the translation above cannot be mistaken for the
    /// whole guard: if someone removed <c>ExistsAsync</c> and relied on 23505 alone, this test
    /// would still pass — but the inner exception assertion is what tells the two apart.
    /// </summary>
    [Fact(DisplayName = "A name colliding with an ACTIVE agent gives ConflictException (409) from the pre-check, before any insert")]
    public async Task Handle_NameCollidesWithActiveAgent_ThrowsConflictExceptionFromPreCheck()
    {
        const string collidingName = "Active Collider";
        await SeedAgentAsync(collidingName, softDeleted: false);

        using var scope = Sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var act = async () => await mediator.Send(Command(collidingName), Ct);

        (await act.Should().ThrowAsync<ConflictException>())
            .WithMessage("*already exists*")
            .And.InnerException.Should().BeNull("the pre-check short-circuits before SaveChangesAsync");
    }
}
