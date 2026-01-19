using Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8NTemplates;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.Handlers;

/// <summary>
/// Tests for GetN8NTemplatesQueryHandler.
/// Tests retrieval of all n8n workflow templates with optional category filtering.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetN8nTemplatesQueryHandlerTests
{
    private readonly Mock<IN8NTemplateService> _mockTemplateService;
    private readonly Mock<ILogger<GetN8NTemplatesQueryHandler>> _mockLogger;
    private readonly GetN8NTemplatesQueryHandler _handler;

    public GetN8nTemplatesQueryHandlerTests()
    {
        _mockTemplateService = new Mock<IN8NTemplateService>();
        _mockLogger = new Mock<ILogger<GetN8NTemplatesQueryHandler>>();
        _handler = new GetN8NTemplatesQueryHandler(
            _mockTemplateService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithNoFilter_ReturnsAllTemplates()
    {
        // Arrange
        var query = new GetN8NTemplatesQuery { Category = null };

        var expectedTemplates = new List<WorkflowTemplateDto>
        {
            new WorkflowTemplateDto(
                Id: "template-1",
                Name: "Data Sync",
                Version: "1.0.0",
                Description: "Sync data between systems",
                Category: "Integration",
                Author: "MeepleAI",
                Tags: new List<string> { "sync", "data" },
                Icon: "sync-icon",
                Screenshot: "screenshot1.png",
                Documentation: "docs1.md",
                Parameters: new List<TemplateParameterDto>()
            ),
            new WorkflowTemplateDto(
                Id: "template-2",
                Name: "Email Notification",
                Version: "2.1.0",
                Description: "Send automated emails",
                Category: "Notification",
                Author: "MeepleAI",
                Tags: new List<string> { "email", "alert" },
                Icon: "email-icon",
                Screenshot: null,
                Documentation: null,
                Parameters: new List<TemplateParameterDto>()
            )
        };

        _mockTemplateService
            .Setup(s => s.GetTemplatesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTemplates);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Data Sync");
        result[1].Name.Should().Be("Email Notification");

        _mockTemplateService.Verify(
            s => s.GetTemplatesAsync(null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCategoryFilter_ReturnsFilteredTemplates()
    {
        // Arrange
        var category = "Integration";
        var query = new GetN8NTemplatesQuery { Category = category };

        var filteredTemplates = new List<WorkflowTemplateDto>
        {
            new WorkflowTemplateDto(
                Id: "template-integration-1",
                Name: "API Integration",
                Version: "1.5.0",
                Description: "Integrate with external APIs",
                Category: "Integration",
                Author: "MeepleAI",
                Tags: new List<string> { "api", "integration" },
                Icon: "api-icon",
                Screenshot: "api-screenshot.png",
                Documentation: "api-docs.md",
                Parameters: new List<TemplateParameterDto>
                {
                    new TemplateParameterDto(
                        Name: "api_endpoint",
                        Type: "string",
                        Label: "API Endpoint",
                        Description: "The API endpoint URL",
                        Required: true,
                        Default: null,
                        Options: null,
                        Sensitive: false
                    )
                }
            )
        };

        _mockTemplateService
            .Setup(s => s.GetTemplatesAsync(category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(filteredTemplates);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Category.Should().Be("Integration");
        result[0].Name.Should().Be("API Integration");

        _mockTemplateService.Verify(
            s => s.GetTemplatesAsync(category, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoTemplatesAvailable_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetN8NTemplatesQuery { Category = null };

        _mockTemplateService
            .Setup(s => s.GetTemplatesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowTemplateDto>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNonExistentCategory_ReturnsEmptyList()
    {
        // Arrange
        var category = "NonExistentCategory";
        var query = new GetN8NTemplatesQuery { Category = category };

        _mockTemplateService
            .Setup(s => s.GetTemplatesAsync(category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowTemplateDto>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockTemplateService.Verify(
            s => s.GetTemplatesAsync(category, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToService()
    {
        // Arrange
        var query = new GetN8NTemplatesQuery { Category = null };

        var templates = new List<WorkflowTemplateDto>
        {
            new WorkflowTemplateDto(
                Id: "token-template",
                Name: "Token Test",
                Version: "1.0.0",
                Description: "Test",
                Category: "Test",
                Author: "Test",
                Tags: new List<string>(),
                Icon: "icon",
                Screenshot: null,
                Documentation: null,
                Parameters: new List<TemplateParameterDto>()
            )
        };

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockTemplateService
            .Setup(s => s.GetTemplatesAsync(null, cancellationToken))
            .ReturnsAsync(templates);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        _mockTemplateService.Verify(
            s => s.GetTemplatesAsync(null, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_PropagatesException()
    {
        // Arrange
        var query = new GetN8NTemplatesQuery { Category = null };

        _mockTemplateService
            .Setup(s => s.GetTemplatesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Templates directory not found"));

        // Act
        Func<Task> act = async () => await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Templates directory not found");
    }

    [Fact]
    public async Task Handle_LogsRetrievalAttempt()
    {
        // Arrange
        var query = new GetN8NTemplatesQuery { Category = null };

        _mockTemplateService
            .Setup(s => s.GetTemplatesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowTemplateDto>());

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieving n8n templates")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsRetrievalResult()
    {
        // Arrange
        var query = new GetN8NTemplatesQuery { Category = null };

        var templates = new List<WorkflowTemplateDto>
        {
            new WorkflowTemplateDto(
                Id: "log-test-1",
                Name: "Log Test 1",
                Version: "1.0.0",
                Description: "Test",
                Category: "Test",
                Author: "Test",
                Tags: new List<string>(),
                Icon: "icon",
                Screenshot: null,
                Documentation: null,
                Parameters: new List<TemplateParameterDto>()
            ),
            new WorkflowTemplateDto(
                Id: "log-test-2",
                Name: "Log Test 2",
                Version: "1.0.0",
                Description: "Test",
                Category: "Test",
                Author: "Test",
                Tags: new List<string>(),
                Icon: "icon",
                Screenshot: null,
                Documentation: null,
                Parameters: new List<TemplateParameterDto>()
            )
        };

        _mockTemplateService
            .Setup(s => s.GetTemplatesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retrieved") && v.ToString()!.Contains("n8n templates")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsWithCategoryFilter()
    {
        // Arrange
        var category = "Automation";
        var query = new GetN8NTemplatesQuery { Category = category };

        _mockTemplateService
            .Setup(s => s.GetTemplatesAsync(category, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowTemplateDto>());

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("for category")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
