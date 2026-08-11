using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for <see cref="GetTestResultsQueryHandler"/>.
/// Issue #3100: totalCount must reflect the real total count, not the current page size.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3100")]
public sealed class GetTestResultsQueryHandlerTests
{
    private readonly Mock<IAgentTestResultRepository> _repositoryMock;
    private readonly GetTestResultsQueryHandler _handler;

    // The mocked page always returns fewer rows than the true total,
    // so a handler that (wrongly) uses results.Count would report `PageSize`, not `TrueTotal`.
    private const int PageSize = 2;
    private const int TrueTotal = 25;

    public GetTestResultsQueryHandlerTests()
    {
        _repositoryMock = new Mock<IAgentTestResultRepository>();
        _handler = new GetTestResultsQueryHandler(
            _repositoryMock.Object,
            new Mock<ILogger<GetTestResultsQueryHandler>>().Object);
    }

    private static List<AgentTestResult> Page(int count)
    {
        var list = new List<AgentTestResult>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(AgentTestResult.Create(
                typologyId: Guid.NewGuid(),
                query: $"q{i}",
                response: $"r{i}",
                modelUsed: "gpt-test",
                confidenceScore: 0.9,
                tokensUsed: 100,
                costEstimate: 0.01m,
                latencyMs: 50,
                executedBy: Guid.NewGuid()));
        }

        return list;
    }

    [Fact]
    public async Task Handle_SavedOnly_TotalCountReflectsRepositoryCountNotPageSize()
    {
        // Arrange
        var executedBy = Guid.NewGuid();
        var query = new GetTestResultsQuery(SavedOnly: true, ExecutedBy: executedBy, Skip: 0, Take: PageSize);

        _repositoryMock
            .Setup(r => r.GetSavedAsync(executedBy, 0, PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(PageSize));
        _repositoryMock
            .Setup(r => r.GetSavedCountAsync(executedBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrueTotal);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Results.Should().HaveCount(PageSize);
        result.TotalCount.Should().Be(TrueTotal);
        _repositoryMock.Verify(r => r.GetSavedCountAsync(executedBy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExecutedBy_TotalCountReflectsRepositoryCountNotPageSize()
    {
        // Arrange
        var executedBy = Guid.NewGuid();
        var query = new GetTestResultsQuery(ExecutedBy: executedBy, Skip: 0, Take: PageSize);

        _repositoryMock
            .Setup(r => r.GetByExecutedByAsync(executedBy, 0, PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(PageSize));
        _repositoryMock
            .Setup(r => r.GetCountByExecutedByAsync(executedBy, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrueTotal);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Results.Should().HaveCount(PageSize);
        result.TotalCount.Should().Be(TrueTotal);
        _repositoryMock.Verify(r => r.GetCountByExecutedByAsync(executedBy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DateRange_TotalCountReflectsRepositoryCountNotPageSize()
    {
        // Arrange
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var query = new GetTestResultsQuery(From: from, To: to, Skip: 0, Take: PageSize);

        _repositoryMock
            .Setup(r => r.GetByDateRangeAsync(from, to, 0, PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(PageSize));
        _repositoryMock
            .Setup(r => r.GetCountByDateRangeAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrueTotal);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Results.Should().HaveCount(PageSize);
        result.TotalCount.Should().Be(TrueTotal);
        _repositoryMock.Verify(r => r.GetCountByDateRangeAsync(from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TypologyId_TotalCountReflectsRepositoryCount()
    {
        // Arrange (regression guard: this branch already used the count method)
        var typologyId = Guid.NewGuid();
        var query = new GetTestResultsQuery(TypologyId: typologyId, Skip: 0, Take: PageSize);

        _repositoryMock
            .Setup(r => r.GetByTypologyIdAsync(typologyId, 0, PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(PageSize));
        _repositoryMock
            .Setup(r => r.GetCountByTypologyIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TrueTotal);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(TrueTotal);
    }
}
