using Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8NTemplates;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.Handlers;

/// <summary>
/// Tests for GetN8NTemplateByIdQueryHandler.
/// Tests retrieval of a specific n8n workflow template by ID with full details.
/// </summary>
public class GetN8nTemplateByIdQueryHandlerTests
{
    private readonly Mock<IN8NTemplateService> _mockTemplateService;
    private readonly Mock<ILogger<GetN8NTemplateByIdQueryHandler>> _mockLogger;
    private readonly GetN8NTemplateByIdQueryHandler _handler;

    public GetN8nTemplateByIdQueryHandlerTests()
    {
        _mockTemplateService = new Mock<IN8NTemplateService>();
        _mockLogger = new Mock<ILogger<GetN8NTemplateByIdQueryHandler>>();
        _handler = new GetN8NTemplateByIdQueryHandler(
            _mockTemplateService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidId_ReturnsTemplateDetails()
    {
        // Arrange
        var templateId = "data-sync-template";
        var query = new GetN8NTemplateByIdQuery { TemplateId = templateId };

        var workflowObject = new
        {
            nodes = new[] {
                new { type = "n8n-nodes-base.start", name = "Start" },
                new { type = "n8n-nodes-base.http", name = "HTTP Request" }
            },
            connections = new { }
        };

        var expectedTemplate = new WorkflowTemplateDetailDto(
            Id: templateId,
            Name: "Data Sync Workflow",
            Version: "1.2.0",
            Description: "Synchronize data between two systems",
            Category: "Integration",
            Author: "MeepleAI Team",
            Tags: new List<string> { "sync", "data", "integration" },
            Icon: "sync-icon.svg",
            Screenshot: "data-sync-screenshot.png",
            Documentation: "# Data Sync\n\nThis template syncs data.",
            Parameters: new List<TemplateParameterDto>
            {
                new TemplateParameterDto(
                    Name: "source_url",
                    Type: "string",
                    Label: "Source URL",
                    Description: "The source system URL",
                    Required: true,
                    Default: null,
                    Options: null,
                    Sensitive: false
                ),
                new TemplateParameterDto(
                    Name: "api_key",
                    Type: "string",
                    Label: "API Key",
                    Description: "Authentication API key",
                    Required: true,
                    Default: null,
                    Options: null,
                    Sensitive: true
                )
            },
            Workflow: workflowObject
        );

        _mockTemplateService
            .Setup(s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTemplate);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(templateId);
        result.Name.Should().Be("Data Sync Workflow");
        result.Version.Should().Be("1.2.0");
        result.Category.Should().Be("Integration");
        result.Parameters.Should().HaveCount(2);
        result.Parameters[0].Name.Should().Be("source_url");
        result.Parameters[1].Sensitive.Should().BeTrue();
        result.Workflow.Should().NotBeNull();

        _mockTemplateService.Verify(
            s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        GetN8NTemplateByIdQuery query = null!;

        // Act
        Func<Task> act = async () => await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithEmptyTemplateId_ReturnsNull()
    {
        // Arrange
        var query = new GetN8NTemplateByIdQuery { TemplateId = string.Empty };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        _mockTemplateService.Verify(
            s => s.GetTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceTemplateId_ReturnsNull()
    {
        // Arrange
        var query = new GetN8NTemplateByIdQuery { TemplateId = "   " };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        _mockTemplateService.Verify(
            s => s.GetTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var templateId = "non-existent-template";
        var query = new GetN8NTemplateByIdQuery { TemplateId = templateId };

        _mockTemplateService
            .Setup(s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowTemplateDetailDto?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        _mockTemplateService.Verify(
            s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithTemplateWithoutScreenshot_ReturnsTemplateWithNullScreenshot()
    {
        // Arrange
        var templateId = "no-screenshot-template";
        var query = new GetN8NTemplateByIdQuery { TemplateId = templateId };

        var templateWithoutScreenshot = new WorkflowTemplateDetailDto(
            Id: templateId,
            Name: "Simple Template",
            Version: "1.0.0",
            Description: "A simple template",
            Category: "Basic",
            Author: "MeepleAI",
            Tags: new List<string> { "simple" },
            Icon: "icon.svg",
            Screenshot: null,
            Documentation: null,
            Parameters: new List<TemplateParameterDto>(),
            Workflow: new { nodes = new[] { new { type = "start" } } }
        );

        _mockTemplateService
            .Setup(s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templateWithoutScreenshot);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Screenshot.Should().BeNull();
        result.Documentation.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithComplexWorkflow_ReturnsFullWorkflowObject()
    {
        // Arrange
        var templateId = "complex-workflow-template";
        var query = new GetN8NTemplateByIdQuery { TemplateId = templateId };

        var complexWorkflow = new
        {
            nodes = new object[] {
                new { id = "1", type = "n8n-nodes-base.start", name = "Start", parameters = new { } },
                new { id = "2", type = "n8n-nodes-base.http", name = "HTTP", parameters = new { method = "GET" } },
                new { id = "3", type = "n8n-nodes-base.set", name = "Set", parameters = new { values = new[] { "key=value" } } }
            },
            connections = new
            {
                Start = new { main = new object[] { new object[] { new { node = "HTTP", type = "main", index = 0 } } } }
            }
        };

        var template = new WorkflowTemplateDetailDto(
            Id: templateId,
            Name: "Complex Workflow",
            Version: "2.0.0",
            Description: "Complex multi-node workflow",
            Category: "Advanced",
            Author: "MeepleAI",
            Tags: new List<string> { "complex", "advanced" },
            Icon: "complex-icon.svg",
            Screenshot: "complex-screenshot.png",
            Documentation: "## Complex Workflow\n\nThis is complex.",
            Parameters: new List<TemplateParameterDto>(),
            Workflow: complexWorkflow
        );

        _mockTemplateService
            .Setup(s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Workflow.Should().NotBeNull();
        result.Workflow.Should().Be(complexWorkflow);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToService()
    {
        // Arrange
        var templateId = "token-test-template";
        var query = new GetN8NTemplateByIdQuery { TemplateId = templateId };

        var template = new WorkflowTemplateDetailDto(
            Id: templateId,
            Name: "Token Test",
            Version: "1.0.0",
            Description: "Test",
            Category: "Test",
            Author: "Test",
            Tags: new List<string>(),
            Icon: "icon",
            Screenshot: null,
            Documentation: null,
            Parameters: new List<TemplateParameterDto>(),
            Workflow: new { }
        );

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockTemplateService
            .Setup(s => s.GetTemplateAsync(templateId, cancellationToken))
            .ReturnsAsync(template);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockTemplateService.Verify(
            s => s.GetTemplateAsync(templateId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_PropagatesException()
    {
        // Arrange
        var templateId = "error-template";
        var query = new GetN8NTemplateByIdQuery { TemplateId = templateId };

        _mockTemplateService
            .Setup(s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Template file is corrupted"));

        // Act
        Func<Task> act = async () => await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Template file is corrupted");
    }

    [Fact]
    public async Task Handle_LogsRetrievalAttempt()
    {
        // Arrange
        var templateId = "log-test-template";
        var query = new GetN8NTemplateByIdQuery { TemplateId = templateId };

        _mockTemplateService
            .Setup(s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowTemplateDetailDto?)null);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieving n8n template")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTemplateNotFound_LogsWarning()
    {
        // Arrange
        var templateId = "missing-template";
        var query = new GetN8NTemplateByIdQuery { TemplateId = templateId };

        _mockTemplateService
            .Setup(s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowTemplateDetailDto?)null);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTemplateFound_LogsSuccess()
    {
        // Arrange
        var templateId = "success-template";
        var templateName = "Success Template Name";
        var query = new GetN8NTemplateByIdQuery { TemplateId = templateId };

        var template = new WorkflowTemplateDetailDto(
            Id: templateId,
            Name: templateName,
            Version: "1.0.0",
            Description: "Test",
            Category: "Test",
            Author: "Test",
            Tags: new List<string>(),
            Icon: "icon",
            Screenshot: null,
            Documentation: null,
            Parameters: new List<TemplateParameterDto>(),
            Workflow: new { }
        );

        _mockTemplateService
            .Setup(s => s.GetTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieved template")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyId_LogsWarning()
    {
        // Arrange
        var query = new GetN8NTemplateByIdQuery { TemplateId = string.Empty };

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Template ID is required")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
