using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Unit tests for <see cref="GetCommentsForLineQueryHandler"/>.
/// Issue #3115: a non-positive LineNumber is invalid input and must map to
/// 400 Bad Request (BadRequestException), not 500 (InvalidOperationException).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GetCommentsForLineQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetCommentsForLineQueryHandler _sut;

    public GetCommentsForLineQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"GetCommentsForLineTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _sut = new GetCommentsForLineQueryHandler(
            _dbContext,
            NullLogger<GetCommentsForLineQueryHandler>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Handle_NonPositiveLineNumber_ThrowsBadRequestException(int lineNumber)
    {
        // Arrange
        var query = new GetCommentsForLineQuery(
            GameId: Guid.NewGuid().ToString(),
            Version: "v1",
            LineNumber: lineNumber);

        // Act & Assert
        var act = () => _sut.Handle(query, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Line number must be positive*");
    }
}
