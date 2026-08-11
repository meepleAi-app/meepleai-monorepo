using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Unit tests for GetGameDetailsQueryHandler. #3263 discovery: the handler must
/// short-circuit on Guid.Empty (→ null → HTTP 404) instead of letting
/// GameRef.Shared(Guid.Empty) throw ArgumentException (→ HTTP 400), matching the
/// sibling GetGameByIdQueryHandler and the endpoint's advertised 404 contract.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetGameDetailsQueryHandlerTests
{
    private readonly Mock<IGameCoreDataProvider> _gameCoreDataMock = new();
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock = new();
    private readonly GetGameDetailsQueryHandler _handler;

    public GetGameDetailsQueryHandlerTests()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetGameDetailsQueryHandler(
            _gameCoreDataMock.Object,
            _sessionRepositoryMock.Object,
            db);
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNull()
    {
        var query = new GetGameDetailsQuery(Guid.Empty);

        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.Should().BeNull();
        // The core-data provider must never be consulted for an empty id.
        _gameCoreDataMock.Verify(
            x => x.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
