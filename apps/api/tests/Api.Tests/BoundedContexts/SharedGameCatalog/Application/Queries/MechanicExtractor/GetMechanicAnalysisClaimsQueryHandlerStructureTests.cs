using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GetMechanicAnalysisClaimsQueryHandlerStructureTests
{
    [Fact]
    public async Task Handle_EntityWithNullOverridesAndTrigger_MapsStructureWithEmptyOverrides()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var analysisId = Guid.NewGuid();
        db.MechanicAnalyses.Add(new MechanicAnalysisEntity { Id = analysisId, PromptVersion = "v1.2.0" });
        db.MechanicClaims.Add(new MechanicClaimEntity
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Section = (int)MechanicSection.Mechanics,
            Text = "t",
            Kind = 1,
            Priority = 2,
            Overrides = null,
            Trigger = null
        });
        await db.SaveChangesAsync();
        var handler = new GetMechanicAnalysisClaimsQueryHandler(db);

        var result = await handler.Handle(new GetMechanicAnalysisClaimsQuery(analysisId), CancellationToken.None);

        var dto = result.Should().ContainSingle().Subject;
        dto.Kind.Should().Be(MechanicClaimKind.Exception);
        dto.Priority.Should().Be(MechanicRulePriority.Card);
        dto.Overrides.Should().BeEmpty();
        dto.Trigger.Should().BeNull();
    }
}
