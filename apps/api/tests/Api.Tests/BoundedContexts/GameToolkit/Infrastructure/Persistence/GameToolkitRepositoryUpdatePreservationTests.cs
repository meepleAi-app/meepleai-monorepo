using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Infrastructure.Persistence;
using Api.Infrastructure.Entities.GameToolkit;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Infrastructure.Persistence;

/// <summary>
/// Regression tests for <see cref="GameToolkitRepository.UpdateAsync"/> (issue #1458 footgun).
///
/// <para>
/// <c>MapToPersistence</c> synthesizes <c>VersionSemver = "0.{Version}.0"</c> and omits
/// <c>Description</c>/<c>License</c> entirely (the domain aggregate has no knowledge of these
/// three entity-level marketplace columns). Because <c>UpdateAsync</c> calls
/// <c>DbContext.Update(entity)</c>, which marks EVERY scalar column Modified, any non-publish
/// update path (rename, add/remove tool, override change, …) silently overwrote the published
/// marketplace pointer: <c>VersionSemver "2.3.1" → "0.1.0"</c> and nulled <c>Description</c>/<c>License</c>.
/// </para>
///
/// InMemory is the correct vehicle: it honors per-property <c>IsModified</c> flags but ignores
/// the Postgres <c>xmin</c> concurrency token (which would otherwise mask this bug behind a
/// <c>DbUpdateConcurrencyException</c> on a real database).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
[Trait("Issue", "1458")]
public class GameToolkitRepositoryUpdatePreservationTests
{
    private static readonly Guid OwnerId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly DateTime Now = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateAsync_RenamingToolkit_PreservesPublishedVersionSemverDescriptionAndLicense()
    {
        var collector = TestDbContextFactory.CreateMockEventCollector();
        var dbName = Guid.NewGuid().ToString();
        var toolkitId = Guid.NewGuid();

        // ── Arrange: seed a published toolkit with a real semver + marketplace metadata ──
        using (var seedContext = TestDbContextFactory.CreateInMemoryDbContextWithCollector(collector, dbName))
        {
#pragma warning disable CS0618 // legacy int Version setter — required to seed the paired column
            seedContext.GameToolkits.Add(new GameToolkitEntity
            {
                Id = toolkitId,
                Name = "Original Name",
                CreatedByUserId = OwnerId,
                Version = 1,
                VersionSemver = "2.3.1",
                Description = "A great community toolkit",
                License = "CC BY-SA 4.0",
                IsPublished = true,
                TemplateStatus = (int)TemplateStatus.Approved,
                CreatedAt = Now,
                UpdatedAt = Now,
            });
#pragma warning restore CS0618
            await seedContext.SaveChangesAsync();
        }

        // ── Act: rename through the real repository + handler (separate context) ──
        using (var actContext = TestDbContextFactory.CreateInMemoryDbContextWithCollector(collector, dbName))
        {
            var repository = new GameToolkitRepository(actContext, collector.Object);
            var unitOfWork = new EfCoreUnitOfWork(actContext);
            var handler = new UpdateToolkitCommandHandler(repository, unitOfWork);

            await handler.Handle(new UpdateToolkitCommand(toolkitId, "Renamed Toolkit"), default);
        }

        // ── Assert: rename applied, but the published marketplace columns are untouched ──
        using (var assertContext = TestDbContextFactory.CreateInMemoryDbContextWithCollector(collector, dbName))
        {
            var reloaded = await assertContext.GameToolkits.FindAsync(toolkitId);

            reloaded.Should().NotBeNull();
            reloaded!.Name.Should().Be("Renamed Toolkit", "the rename must still persist");
            reloaded.VersionSemver.Should().Be("2.3.1", "a rename must not regress the published marketplace version pointer");
            reloaded.Description.Should().Be("A great community toolkit", "a rename must not null the marketplace description");
            reloaded.License.Should().Be("CC BY-SA 4.0", "a rename must not null the marketplace license");
        }
    }
}
