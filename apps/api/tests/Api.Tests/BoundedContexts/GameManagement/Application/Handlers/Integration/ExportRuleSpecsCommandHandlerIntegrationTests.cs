using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.Integration;

/// <summary>
/// Issue #3175 - ExportRuleSpecsCommandHandler threw InvalidOperationException("No rule specs
/// found ...") when no rule specs matched the requested game IDs. The message does NOT contain
/// the substring "not found", so the middleware fell through to 500 instead of 404. The handler
/// must surface NotFoundException (404).
///
/// Integration test (real Postgres): the handler's latest-version query
/// (GroupBy + MaxBy) is not translatable by the EF InMemory provider, so a real provider
/// is required to exercise the count == 0 branch.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
public sealed class ExportRuleSpecsCommandHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private ExportRuleSpecsCommandHandler _handler = null!;

    public ExportRuleSpecsCommandHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var dbName = $"test_export_rulespecs_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(dbName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            TestDbContextFactory.CreateMockMediator().Object,
            TestDbContextFactory.CreateMockEventCollector().Object);
        await _dbContext.Database.MigrateAsync();

        _handler = new ExportRuleSpecsCommandHandler(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Handle_NoRuleSpecsForProvidedGameIds_ThrowsNotFoundException()
    {
        // The database has no rule specs for this random game id, so the handler must
        // surface NotFoundException (404), not InvalidOperationException (500).
        var command = new ExportRuleSpecsCommand(new List<Guid> { Guid.NewGuid() });

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*rule specs*");
    }
}
