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
/// <c>MapToPersistence</c> omits <c>Description</c>/<c>License</c> entirely (the domain aggregate
/// has no knowledge of those entity-level marketplace columns). Because <c>UpdateAsync</c> calls
/// <c>DbContext.Update(entity)</c>, which marks EVERY scalar column Modified, any non-publish
/// update path (rename, add/remove tool, override change, …) would null them.
/// </para>
///
/// <para>
/// <c>VersionSemver</c> used to be in that list too, synthesized as <c>"0.{Version}.0"</c>, which
/// silently reset the published marketplace pointer <c>"2.3.1" → "0.1.0"</c>. Since #3670 it is
/// real aggregate state, loaded by <c>MapToDomain</c> and written back like any other column —
/// so this test now guards the round-trip rather than an exclusion, and still goes red if the
/// synthesis returns.
/// </para>
///
/// InMemory honors per-property <c>IsModified</c> flags, which is what this test asserts on. It
/// has no <c>xmin</c> column, so it says nothing about the Postgres concurrency token — that is
/// covered by <c>GameToolkitRepositoryPostgresConcurrencyTests</c> (#3670), added after this
/// provider gap turned out to be hiding a real DbUpdateConcurrencyException on every write.
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
