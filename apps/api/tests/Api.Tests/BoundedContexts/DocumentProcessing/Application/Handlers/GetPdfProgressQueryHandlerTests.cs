using Api.BoundedContexts.DocumentProcessing.Application.Handlers;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Tests for GetPdfProgressQueryHandler.
/// Tests PDF processing progress retrieval.
/// NOTE: Uses DbContext directly - simplified tests due to mocking complexity.
/// TODO: Convert to integration tests or refactor handler to use repository.
/// </summary>
public class GetPdfProgressQueryHandlerTests
{
    private readonly Mock<ILogger<GetPdfProgressQueryHandler>> _loggerMock;

    public GetPdfProgressQueryHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GetPdfProgressQueryHandler>>();
    }

    #region Construction Tests

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
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetPdfProgressQueryHandler(
                null!,
                _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var dbContext = DbContextHelper.CreateInMemoryDbContext();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetPdfProgressQueryHandler(
                dbContext,
                null!));
    }

    #endregion

    #region Query Tests

    [Fact]
    public void Query_HasCorrectPdfIdProperty()
    {
        // Arrange
        var pdfId = Guid.NewGuid();

        // Act
        var query = new GetPdfProgressQuery(pdfId);

        // Assert
        Assert.Equal(pdfId, query.PdfId);
    }

    #endregion

    #region Result Tests

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
        Assert.Equal(id, result.Id);
        Assert.Equal(userId, result.UploadedByUserId);
        Assert.Equal(progressJson, result.ProcessingProgressJson);
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
        Assert.Null(result.ProcessingProgressJson);
    }

    #endregion

    // NOTE: Full integration tests for Handle method should be in integration test suite
    // due to DbContext dependency complexity. See integration-tests.yml workflow.
}
