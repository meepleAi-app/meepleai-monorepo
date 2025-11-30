using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using N8nConfiguration = Api.BoundedContexts.WorkflowIntegration.Domain.Entities.N8nConfiguration;
using Api.BoundedContexts.WorkflowIntegration.Application.Handlers;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.WorkflowIntegration.Application.Handlers;

/// <summary>
/// Tests for CreateN8nConfigCommandHandler.
/// Tests creation of n8n workflow configurations.
/// </summary>
public class CreateN8nConfigCommandHandlerTests
{
    private readonly Mock<IN8nConfigurationRepository> _mockConfigRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateN8nConfigCommandHandler _handler;

    public CreateN8nConfigCommandHandlerTests()
    {
        _mockConfigRepository = new Mock<IN8nConfigurationRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new CreateN8nConfigCommandHandler(
            _mockConfigRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesN8nConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateN8nConfigCommand(
            Name: "Production N8n",
            BaseUrl: "https://n8n.example.com",
            ApiKeyEncrypted: "encrypted_api_key_12345",
            CreatedByUserId: userId,
            WebhookUrl: "https://webhook.example.com/n8n"
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Production N8n", result.Name);
        Assert.Equal("https://n8n.example.com", result.BaseUrl);
        Assert.Equal("https://webhook.example.com/n8n", result.WebhookUrl);
        Assert.True(result.IsActive);
        Assert.Null(result.LastTestedAt);

        _mockConfigRepository.Verify(
            r => r.AddAsync(
                It.IsAny<N8nConfiguration>(, TestContext.Current.CancellationToken),
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
        var command = new CreateN8nConfigCommand(
            Name: "Development N8n",
            BaseUrl: "http://localhost:5678",
            ApiKeyEncrypted: "dev_api_key",
            CreatedByUserId: userId,
            WebhookUrl: null
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Development N8n", result.Name);
        Assert.Equal("http://localhost:5678", result.BaseUrl);
        Assert.Null(result.WebhookUrl);
    }

    [Fact]
    public async Task Handle_GeneratesNewId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateN8nConfigCommand(
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
        var command = new CreateN8nConfigCommand(
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
        var command = new CreateN8nConfigCommand(
            Name: "Secure N8n",
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
        var command = new CreateN8nConfigCommand(
            Name: "Test",
            BaseUrl: "https://test.com",
            ApiKeyEncrypted: "key",
            CreatedByUserId: userId
        );
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockConfigRepository.Verify(
            r => r.AddAsync(
                It.IsAny<N8nConfiguration>(, TestContext.Current.CancellationToken),
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
        var command = new CreateN8nConfigCommand(
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

