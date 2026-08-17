using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameToolkit;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.ToolkitMarketplace;

/// <summary>
/// Issue #3670. Exercises <c>GameToolkitRepository.UpdateAsync</c> against a real Postgres.
/// </summary>
/// <remarks>
/// Every other test of this repository runs on the EF InMemory provider, which has no
/// <c>xmin</c> system column — so it cannot observe what the concurrency token does to the
/// UPDATE statement. <c>GameToolkitEntityConfiguration</c> declares <c>Xmin</c> as a
/// concurrency token, and <c>UpdateAsync</c> attaches a freshly mapped (detached) entity whose
/// <c>Xmin</c> is never assigned. If EF emitted that zero as the original value, the statement
/// would read <c>WHERE "Id" = @p AND xmin = 0</c>, match no live tuple, and every update in
/// this bounded context would raise <c>DbUpdateConcurrencyException</c>.
///
/// This test exists to answer that empirically rather than by reading EF's source: it is the
/// difference between "publish is fine" and "renaming a toolkit has been 500ing in production".
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameToolkit")]
public sealed class GameToolkitRepositoryPostgresConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;

    private static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-000000003670");

    public GameToolkitRepositoryPostgresConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"toolkit_xmin_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private async Task<Guid> SeedToolkitAsync(string versionSemver)
    {
        var toolkitId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

#pragma warning disable CS0618 // legacy int Version — seeding the paired column
        db.GameToolkits.Add(new GameToolkitEntity
        {
            Id = toolkitId,
            Name = "Original Name",
            CreatedByUserId = OwnerId,
            Version = 1,
            VersionSemver = versionSemver,
            IsPublished = true,
            TemplateStatus = (int)TemplateStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
#pragma warning restore CS0618

        await db.SaveChangesAsync();
        return toolkitId;
    }

    [Fact]
    public async Task UpdateAsync_OnPostgres_SucceedsDespiteDetachedXmin()
    {
        var toolkitId = await SeedToolkitAsync("2.3.1");

        // Fresh scope = the change tracker a real request starts with.
        using (var scope = _factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IGameToolkitRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var toolkit = await repository.GetByIdAsync(toolkitId);
            toolkit.Should().NotBeNull();

            toolkit!.UpdateDetails("Renamed Toolkit");
            await repository.UpdateAsync(toolkit);

            // The assertion that matters: no DbUpdateConcurrencyException.
            await unitOfWork.SaveChangesAsync();
        }

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var reloaded = await db.GameToolkits.AsNoTracking()
                .FirstAsync(t => t.Id == toolkitId);

            reloaded.Name.Should().Be("Renamed Toolkit");
            reloaded.VersionSemver.Should().Be("2.3.1",
                "a non-publish update must not disturb the published marketplace pointer");
        }
    }

    [Fact]
    public async Task PublishMarketplaceVersion_ThroughRepository_PersistsOnPostgres()
    {
        var toolkitId = await SeedToolkitAsync("1.0.0");
        var publishedAt = new DateTime(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);

        using (var scope = _factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IGameToolkitRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var toolkit = await repository.GetByIdAsync(toolkitId);
            toolkit!.PublishMarketplaceVersion("1.1.0", publishedAt);
            await repository.UpdateAsync(toolkit);
            await unitOfWork.SaveChangesAsync();
        }

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var reloaded = await db.GameToolkits.AsNoTracking()
                .FirstAsync(t => t.Id == toolkitId);

            reloaded.VersionSemver.Should().Be("1.1.0");
            reloaded.IsPublished.Should().BeTrue();
        }
    }
}
