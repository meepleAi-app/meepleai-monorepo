using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Tests for GetPdfProgressQueryHandler.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// Tests PDF processing progress retrieval.
/// NOTE: Uses DbContext directly - simplified tests due to mocking complexity.
/// ISSUE-1674: Convert to integration tests or refactor handler to use repository.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetPdfProgressQueryHandlerTests
{
    private readonly Mock<ILogger<GetPdfProgressQueryHandler>> _loggerMock;

    public GetPdfProgressQueryHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GetPdfProgressQueryHandler>>();
    }
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var dbContext = DbContextHelper.CreateInMemoryDbContext();

        // Act
        var handler = new GetPdfProgressQueryHandler(
            dbContext,
            _loggerMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () =>
            new GetPdfProgressQueryHandler(
                null!,
                _loggerMock.Object);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var dbContext = DbContextHelper.CreateInMemoryDbContext();

        // Act & Assert
        Action act = () =>
            new GetPdfProgressQueryHandler(
                dbContext,
                null!);

        act.Should().Throw<ArgumentNullException>();
    }
    [Fact]
    public void Query_HasCorrectPdfIdProperty()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        // Act
        var query = new GetPdfProgressQuery(pdfId);

        // Assert
        query.PdfId.Should().Be(pdfId);
    }
    [Fact]
    public void PdfProgressResult_ConstructsCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var progressJson = "{\"stage\":\"extraction\",\"progress\":50}";

        // Act
        var result = new PdfProgressResult(id, userId, progressJson);

        // Assert
        result.Id.Should().Be(id);
        result.UploadedByUserId.Should().Be(userId);
        result.ProcessingProgressJson.Should().Be(progressJson);
    }

    [Fact]
    public void PdfProgressResult_WithNullProgressJson_AllowsNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var result = new PdfProgressResult(id, userId, null);

        // Assert
        result.ProcessingProgressJson.Should().BeNull();
    }
    // NOTE: Full integration tests for Handle method should be in integration test suite
    // due to DbContext dependency complexity. See integration-tests.yml workflow.
}
