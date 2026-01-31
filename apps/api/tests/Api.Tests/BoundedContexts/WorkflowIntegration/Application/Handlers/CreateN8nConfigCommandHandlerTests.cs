using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using N8NConfiguration = Api.BoundedContexts.WorkflowIntegration.Domain.Entities.N8NConfiguration;
using Api.BoundedContexts.WorkflowIntegration.Application.Handlers;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.Handlers;

/// <summary>
/// Tests for CreateN8NConfigCommandHandler.
/// Tests creation of n8n workflow configurations.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateN8NConfigCommandHandlerTests
{
    private readonly Mock<IN8NConfigurationRepository> _mockConfigRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateN8NConfigCommandHandler _handler;

    public CreateN8NConfigCommandHandlerTests()
    {
        _mockConfigRepository = new Mock<IN8NConfigurationRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new CreateN8NConfigCommandHandler(
            _mockConfigRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesN8NConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateN8NConfigCommand(
            Name: "Production N8N",
            BaseUrl: "https://n8n.example.com",
            ApiKeyEncrypted: "encrypted_api_key_12345",
            CreatedByUserId: userId,
            WebhookUrl: "https://webhook.example.com/n8n"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Production N8N", result.Name);
        Assert.Equal("https://n8n.example.com", result.BaseUrl);
        Assert.Equal("https://webhook.example.com/n8n", result.WebhookUrl);
        Assert.True(result.IsActive);
        Assert.Null(result.LastTestedAt);

        _mockConfigRepository.Verify(
            r => r.AddAsync(
                It.IsAny<N8NConfiguration>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutWebhookUrl_CreatesConfigWithNullWebhook()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateN8NConfigCommand(
            Name: "Development N8N",
            BaseUrl: "http://localhost:5678",
            ApiKeyEncrypted: "dev_api_key",
            CreatedByUserId: userId,
            WebhookUrl: null
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Development N8N", result.Name);
        Assert.Equal("http://localhost:5678", result.BaseUrl);
        Assert.Null(result.WebhookUrl);
    }

    [Fact]
    public async Task Handle_GeneratesNewId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateN8NConfigCommand(
            Name: "Test Config",
            BaseUrl: "https://test.n8n.com",
            ApiKeyEncrypted: "test_key",
            CreatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task Handle_SetsActiveByDefault()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateN8NConfigCommand(
            Name: "Active Config",
            BaseUrl: "https://active.n8n.com",
            ApiKeyEncrypted: "active_key",
            CreatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Handle_WithHttpsUrl_CreatesConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateN8NConfigCommand(
            Name: "Secure N8N",
            BaseUrl: "https://secure.n8n.com:443",
            ApiKeyEncrypted: "secure_key",
            CreatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://secure.n8n.com:443", result.BaseUrl);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateN8NConfigCommand(
            Name: "Test",
            BaseUrl: "https://test.com",
            ApiKeyEncrypted: "key",
            CreatedByUserId: userId
        );
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockConfigRepository.Verify(
            r => r.AddAsync(
                It.IsAny<N8NConfiguration>(),
                cancellationToken),
            Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SetsCreatedAtTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var beforeCreate = DateTime.UtcNow;
        var command = new CreateN8NConfigCommand(
            Name: "Timestamp Test",
            BaseUrl: "https://timestamp.n8n.com",
            ApiKeyEncrypted: "timestamp_key",
            CreatedByUserId: userId
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.CreatedAt);
        Assert.True(result.CreatedAt >= beforeCreate);
        Assert.True(result.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }
}

