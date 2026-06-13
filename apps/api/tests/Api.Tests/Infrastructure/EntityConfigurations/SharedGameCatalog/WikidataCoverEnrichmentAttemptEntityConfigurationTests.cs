using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// Issue #2254+#2255 Phase F bundle — pins the EF Core configuration for the
/// new <c>acknowledged_at</c> / <c>acknowledged_by</c> (F5 bulk-acknowledge)
/// and <c>triggered_by_admin_user_id</c> (F6 attribution) columns plus the
/// partial index that powers the default "hide acknowledged" list view.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class WikidataCoverEnrichmentAttemptEntityConfigurationTests
{
    [Fact]
    public void EntityModel_Has_PhaseF_ColumnsAndIndex()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"phasef-{Guid.NewGuid()}")
            .Options;

        var mediator = new Mock<IMediator>();
        var collector = new Mock<IDomainEventCollector>();

        using var ctx = new MeepleAiDbContext(options, mediator.Object, collector.Object);

        var entityType = ctx.Model.FindEntityType(typeof(WikidataCoverEnrichmentAttemptEntity))!;
        var props = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();

        props.Should().Contain(new[] { "acknowledged_at", "acknowledged_by", "triggered_by_admin_user_id" });

        var idx = entityType.GetIndexes().FirstOrDefault(i =>
            i.GetDatabaseName() == "ix_wikidata_cover_attempts_acknowledged_at");
        idx.Should().NotBeNull("partial index required for fast 'exclude acked' default list");
        idx!.GetFilter().Should().Be("acknowledged_at IS NOT NULL");
    }
}
