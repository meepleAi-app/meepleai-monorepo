using Api.BoundedContexts.Administration.Application.Queries.StagingAllowlist;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.StagingAllowlist;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class GetStagingAllowlistQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsDtosOrderedByAddedAtDesc()
    {
        var older = StagingAllowlistEntry.Reconstitute(
            Guid.NewGuid(), "a@x.y", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-5), "old", false, null, null);
        var newer = StagingAllowlistEntry.Reconstitute(
            Guid.NewGuid(), "b@x.y", Guid.NewGuid(), DateTimeOffset.UtcNow, "new", false, null, null);

        var repo = new Mock<IStagingAllowlistRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StagingAllowlistEntry> { older, newer });

        var handler = new GetStagingAllowlistQueryHandler(repo.Object);
        var result = await handler.Handle(new GetStagingAllowlistQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Email.Should().Be("b@x.y", "newest first");
        result[1].Email.Should().Be("a@x.y");
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyList()
    {
        var repo = new Mock<IStagingAllowlistRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StagingAllowlistEntry>());

        var handler = new GetStagingAllowlistQueryHandler(repo.Object);
        var result = await handler.Handle(new GetStagingAllowlistQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
