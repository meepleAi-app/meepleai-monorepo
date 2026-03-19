using Api.BoundedContexts.WorkflowIntegration.Application.Queries.N8NTemplates;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.Queries;

/// <summary>
/// Tests for ValidateN8NTemplateQueryHandler.
/// Tests n8n workflow template JSON validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ValidateN8nTemplateQueryHandlerTests
{
    private readonly Mock<IN8NTemplateService> _mockTemplateService;
    private readonly Mock<ILogger<ValidateN8NTemplateQueryHandler>> _mockLogger;
    private readonly ValidateN8NTemplateQueryHandler _handler;

    public ValidateN8nTemplateQueryHandlerTests()
    {
        _mockTemplateService = new Mock<IN8NTemplateService>();
        _mockLogger = new Mock<ILogger<ValidateN8NTemplateQueryHandler>>();
        _handler = new ValidateN8NTemplateQueryHandler(
            _mockTemplateService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidTemplateJson_ReturnsValidResponse()
    {
        // Arrange
        var validJson = @"{
            ""id"": ""test-template"",
            ""name"": ""Test Template"",
            ""version"": ""1.0.0"",
            ""description"": ""A valid template"",
            ""category"": ""Test"",
            ""author"": ""MeepleAI"",
            ""tags"": [""test""],
            ""icon"": ""icon.svg"",
            ""parameters"": [],
            ""workflow"": {
                ""nodes"": [
                    {""type"": ""n8n-nodes-base.start"", ""name"": ""Start""}
                ],
                ""connections"": {}
            }
        }";

        var query = new ValidateN8NTemplateQuery { TemplateJson = validJson };

        var expectedResponse = new ValidateTemplateResponse(
            IsValid: true,
            Errors: null
        );

        _mockTemplateService
            .Setup(s => s.ValidateTemplate(validJson))
            .Returns(expectedResponse);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeNull();

        _mockTemplateService.Verify(
            s => s.ValidateTemplate(validJson),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidJson_ReturnsInvalidResponseWithErrors()
    {
        // Arrange
        var invalidJson = @"{
            ""id"": ""invalid-template"",
            ""name"": ""Invalid Template""
        }";

        var query = new ValidateN8NTemplateQuery { TemplateJson = invalidJson };

        var expectedResponse = new ValidateTemplateResponse(
            IsValid: false,
            Errors: new List<string>
            {
                "Missing required field: version",
                "Missing required field: workflow",
                "Missing required field: parameters"
            }
        );

        _mockTemplateService
            .Setup(s => s.ValidateTemplate(invalidJson))
            .Returns(expectedResponse);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain("Missing required field: version");

        _mockTemplateService.Verify(
            s => s.ValidateTemplate(invalidJson),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        ValidateN8NTemplateQuery query = null!;

        // Act
        Func<Task> act = async () => await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_WithEmptyTemplateJson_ReturnsInvalidResponse()
    {
        // Arrange
        var query = new ValidateN8NTemplateQuery { TemplateJson = string.Empty };

        var expectedResponse = new ValidateTemplateResponse(
            IsValid: false,
            Errors: new List<string> { "Template JSON is required" }
        );

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().Contain("Template JSON is required");

        _mockTemplateService.Verify(
            s => s.ValidateTemplate(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceTemplateJson_ReturnsInvalidResponse()
    {
        // Arrange
        var query = new ValidateN8NTemplateQuery { TemplateJson = "   " };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().Contain("Template JSON is required");
    }

    [Fact]
    public async Task Handle_WithMalformedJson_ReturnsInvalidResponse()
    {
        // Arrange
        var malformedJson = @"{
            ""id"": ""malformed"",
            ""name"": ""Test"",
            ""invalid-syntax
        }";

        var query = new ValidateN8NTemplateQuery { TemplateJson = malformedJson };

        var expectedResponse = new ValidateTemplateResponse(
            IsValid: false,
            Errors: new List<string> { "Invalid JSON format" }
        );

        _mockTemplateService
            .Setup(s => s.ValidateTemplate(malformedJson))
            .Returns(expectedResponse);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().Contain("Invalid JSON format");
    }

    [Fact]
    public async Task Handle_WithMissingWorkflowNodes_ReturnsInvalidResponse()
    {
        // Arrange
        var jsonWithoutNodes = @"{
            ""id"": ""no-nodes"",
            ""name"": ""No Nodes Template"",
            ""version"": ""1.0.0"",
            ""workflow"": {
                ""connections"": {}
            }
        }";

        var query = new ValidateN8NTemplateQuery { TemplateJson = jsonWithoutNodes };

        var expectedResponse = new ValidateTemplateResponse(
            IsValid: false,
            Errors: new List<string> { "Workflow must contain at least one node" }
        );

        _mockTemplateService
            .Setup(s => s.ValidateTemplate(jsonWithoutNodes))
            .Returns(expectedResponse);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Workflow must contain at least one node");
    }

    [Fact]
    public async Task Handle_WithCircularDependencies_ReturnsInvalidResponse()
    {
        // Arrange
        var jsonWithCircular = @"{
            ""id"": ""circular"",
            ""name"": ""Circular Template"",
            ""version"": ""1.0.0"",
            ""workflow"": {
                ""nodes"": [
                    {""id"": ""1"", ""type"": ""start""},
                    {""id"": ""2"", ""type"": ""http""}
                ],
                ""connections"": {
                    ""1"": { ""main"": [[{""node"": ""2""}]] },
                    ""2"": { ""main"": [[{""node"": ""1""}]] }
                }
            }
        }";

        var query = new ValidateN8NTemplateQuery { TemplateJson = jsonWithCircular };

        var expectedResponse = new ValidateTemplateResponse(
            IsValid: false,
            Errors: new List<string> { "Circular dependency detected in workflow" }
        );

        _mockTemplateService
            .Setup(s => s.ValidateTemplate(jsonWithCircular))
            .Returns(expectedResponse);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Circular dependency detected in workflow");
    }

    [Fact]
    public async Task Handle_LogsValidationAttempt()
    {
        // Arrange
        var templateJson = @"{""id"": ""log-test""}";
        var query = new ValidateN8NTemplateQuery { TemplateJson = templateJson };

        var response = new ValidateTemplateResponse(IsValid: true, Errors: null);

        _mockTemplateService
            .Setup(s => s.ValidateTemplate(templateJson))
            .Returns(response);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Validating n8n template JSON")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsValidationResult()
    {
        // Arrange
        var templateJson = @"{""id"": ""result-log-test""}";
        var query = new ValidateN8NTemplateQuery { TemplateJson = templateJson };

        var response = new ValidateTemplateResponse(
            IsValid: false,
            Errors: new List<string> { "Error 1", "Error 2" }
        );

        _mockTemplateService
            .Setup(s => s.ValidateTemplate(templateJson))
            .Returns(response);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Template validation result")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyJson_LogsWarning()
    {
        // Arrange
        var query = new ValidateN8NTemplateQuery { TemplateJson = string.Empty };

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Template JSON is required")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
