using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// #2638 / SI-7: <see cref="GetGamebookGlossaryQueryHandler"/> must project each
/// entry's multi-context list into the DTO.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "2638")]
public sealed class GetGamebookGlossaryQueryHandlerTests
{
    private static readonly Guid CampaignId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Handle_ProjectsContextsIntoDto()
    {
        var campaign = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), OwnerId, "Test campaign");
        var campaigns = new Mock<IGamebookCampaignSessionRepository>();
        campaigns.Setup(r => r.GetByIdAsync(CampaignId, It.IsAny<CancellationToken>())).ReturnsAsync(campaign);

        var bookA = Guid.NewGuid();
        var bookB = Guid.NewGuid();
        var entry = GamebookGlossaryEntry.Create(
            CampaignId, "sentinel", "soldato", GlossarySource.Manual, OwnerId,
            contexts: new[]
            {
                GlossaryContext.Create(bookA, "§147", null),
                GlossaryContext.Create(bookB, "§63", "definizione"),
            });

        var glossary = new Mock<IGamebookGlossaryRepository>();
        glossary.Setup(r => r.ListByCampaignAsync(CampaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });

        var handler = new GetGamebookGlossaryQueryHandler(campaigns.Object, glossary.Object);

        var result = await handler.Handle(new GetGamebookGlossaryQuery(CampaignId, OwnerId), CancellationToken.None);

        result.Should().ContainSingle();
        var dto = result[0];
        dto.Contexts.Should().HaveCount(2);
        dto.Contexts.Should().ContainSingle(c => c.BookId == bookA && c.ParagraphRef == "§147" && c.Definition == null);
        dto.Contexts.Should().ContainSingle(c => c.BookId == bookB && c.ParagraphRef == "§63" && c.Definition == "definizione");
    }
}
