using Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8NTemplates;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.Commands;

/// <summary>
/// Tests for ImportN8NTemplateCommandHandler.
/// Tests n8n workflow template import with parameter substitution.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ImportN8nTemplateCommandHandlerTests
{
    private readonly Mock<IN8NTemplateService> _mockTemplateService;
    private readonly Mock<ILogger<ImportN8NTemplateCommandHandler>> _mockLogger;
    private readonly ImportN8NTemplateCommandHandler _handler;

    public ImportN8nTemplateCommandHandlerTests()
    {
        _mockTemplateService = new Mock<IN8NTemplateService>();
        _mockLogger = new Mock<ILogger<ImportN8NTemplateCommandHandler>>();
        _handler = new ImportN8NTemplateCommandHandler(
            _mockTemplateService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ImportsTemplateSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var templateId = "test-template";
        var workflowId = "workflow-123";
        var parameters = new Dictionary<string, string>
        {
            { "api_key", "test-key" },
            { "endpoint", "https://api.example.com" }
        };

        var command = new ImportN8NTemplateCommand
        {
            TemplateId = templateId,
            UserId = userId,
            Parameters = parameters
        };

        var expectedResponse = new ImportTemplateResponse(
            WorkflowId: workflowId,
            Message: "Template imported successfully"
        );

        _mockTemplateService
            .Setup(s => s.ImportTemplateAsync(
                templateId,
                parameters,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.WorkflowId.Should().Be(workflowId);
        result.Message.Should().Be("Template imported successfully");

        _mockTemplateService.Verify(
            s => s.ImportTemplateAsync(
                templateId,
                parameters,
                userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        ImportN8NTemplateCommand command = null!;

        // Act
        Func<Task> act = async () => await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithEmptyTemplateId_ThrowsArgumentException()
    {
        // Arrange
        var command = new ImportN8NTemplateCommand
        {
            TemplateId = string.Empty,
            UserId = "user-123",
            Parameters = new Dictionary<string, string>()
        };

        // Act
        Func<Task> act = async () => await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Template ID is required*");
    }

    [Fact]
    public async Task Handle_WithWhitespaceTemplateId_ThrowsArgumentException()
    {
        // Arrange
        var command = new ImportN8NTemplateCommand
        {
            TemplateId = "   ",
            UserId = "user-123",
            Parameters = new Dictionary<string, string>()
        };

        // Act
        Func<Task> act = async () => await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Template ID is required*");
    }

    [Fact]
    public async Task Handle_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var command = new ImportN8NTemplateCommand
        {
            TemplateId = "test-template",
            UserId = string.Empty,
            Parameters = new Dictionary<string, string>()
        };

        // Act
        Func<Task> act = async () => await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("User ID is required*");
    }

    [Fact]
    public async Task Handle_WithWhitespaceUserId_ThrowsArgumentException()
    {
        // Arrange
        var command = new ImportN8NTemplateCommand
        {
            TemplateId = "test-template",
            UserId = "   ",
            Parameters = new Dictionary<string, string>()
        };

        // Act
        Func<Task> act = async () => await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("User ID is required*");
    }

    [Fact]
    public async Task Handle_WithEmptyParameters_ImportsTemplate()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var templateId = "no-params-template";
        var workflowId = "workflow-456";
        var emptyParams = new Dictionary<string, string>();

        var command = new ImportN8NTemplateCommand
        {
            TemplateId = templateId,
            UserId = userId,
            Parameters = emptyParams
        };

        var expectedResponse = new ImportTemplateResponse(
            WorkflowId: workflowId,
            Message: "Template imported without parameters"
        );

        _mockTemplateService
            .Setup(s => s.ImportTemplateAsync(
                templateId,
                emptyParams,
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.WorkflowId.Should().Be(workflowId);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_PropagatesException()
    {
        // Arrange
        var command = new ImportN8NTemplateCommand
        {
            TemplateId = "failing-template",
            UserId = "user-789",
            Parameters = new Dictionary<string, string>()
        };

        _mockTemplateService
            .Setup(s => s.ImportTemplateAsync(
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Template not found"));

        // Act
        Func<Task> act = async () => await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Template not found");
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToService()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var templateId = "token-test-template";
        var parameters = new Dictionary<string, string> { { "key", "value" } };

        var command = new ImportN8NTemplateCommand
        {
            TemplateId = templateId,
            UserId = userId,
            Parameters = parameters
        };

        var expectedResponse = new ImportTemplateResponse(
            WorkflowId: "workflow-token",
            Message: "Success"
        );

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockTemplateService
            .Setup(s => s.ImportTemplateAsync(
                templateId,
                parameters,
                userId,
                cancellationToken))
            .ReturnsAsync(expectedResponse);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockTemplateService.Verify(
            s => s.ImportTemplateAsync(
                templateId,
                parameters,
                userId,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsImportAttempt()
    {
        // Arrange
        var userId = "user-log-test";
        var templateId = "log-template";
        var parameters = new Dictionary<string, string>
        {
            { "param1", "value1" },
            { "param2", "value2" }
        };

        var command = new ImportN8NTemplateCommand
        {
            TemplateId = templateId,
            UserId = userId,
            Parameters = parameters
        };

        var expectedResponse = new ImportTemplateResponse(
            WorkflowId: "workflow-log",
            Message: "Success"
        );

        _mockTemplateService
            .Setup(s => s.ImportTemplateAsync(
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("importing n8n template")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsSuccessfulImport()
    {
        // Arrange
        var userId = "user-success-log";
        var templateId = "success-template";
        var workflowId = "workflow-success-123";
        var parameters = new Dictionary<string, string>();

        var command = new ImportN8NTemplateCommand
        {
            TemplateId = templateId,
            UserId = userId,
            Parameters = parameters
        };

        var expectedResponse = new ImportTemplateResponse(
            WorkflowId: workflowId,
            Message: "Import successful"
        );

        _mockTemplateService
            .Setup(s => s.ImportTemplateAsync(
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("imported successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
