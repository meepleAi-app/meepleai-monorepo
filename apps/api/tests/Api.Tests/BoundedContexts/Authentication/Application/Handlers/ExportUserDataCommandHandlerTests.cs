using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ExportUserDataCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepository;
    private readonly Mock<IChatThreadRepository> _mockChatThreadRepository;
    private readonly Mock<INotificationRepository> _mockNotificationRepository;
    private readonly Mock<IUserAiConsentRepository> _mockAiConsentRepository;
    private readonly Mock<IConversationMemoryRepository> _mockConversationMemoryRepository;
    private readonly Mock<IApiKeyRepository> _mockApiKeyRepository;
    private readonly Mock<ILogger<ExportUserDataCommandHandler>> _mockLogger;
    private readonly ExportUserDataCommandHandler _handler;

    public ExportUserDataCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLibraryRepository = new Mock<IUserLibraryRepository>();
        _mockChatThreadRepository = new Mock<IChatThreadRepository>();
        _mockNotificationRepository = new Mock<INotificationRepository>();
        _mockAiConsentRepository = new Mock<IUserAiConsentRepository>();
        _mockConversationMemoryRepository = new Mock<IConversationMemoryRepository>();
        _mockApiKeyRepository = new Mock<IApiKeyRepository>();
        _mockLogger = new Mock<ILogger<ExportUserDataCommandHandler>>();

        _handler = new ExportUserDataCommandHandler(
            _mockUserRepository.Object,
            _mockLibraryRepository.Object,
            _mockChatThreadRepository.Object,
            _mockNotificationRepository.Object,
            _mockAiConsentRepository.Object,
            _mockConversationMemoryRepository.Object,
            _mockApiKeyRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidUser_ReturnsExportResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExportUserDataCommand(userId);
        var user = new UserBuilder().WithId(userId).WithEmail("test@example.com").Build();

        SetupMocks(userId, user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Profile.Email.Should().Be("test@example.com");
        result.Profile.Id.Should().Be(userId);
        (result.ExportedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExportUserDataCommand(userId);

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ReturnsCorrectSummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExportUserDataCommand(userId);
        var user = new UserBuilder().WithId(userId).Build();

        SetupMocks(userId, user, memoryCount: 42);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Summary.TotalLibraryGames.Should().Be(0);
        result.Summary.TotalChatThreads.Should().Be(0);
        result.Summary.TotalNotifications.Should().Be(0);
        result.Summary.TotalConversationMemories.Should().Be(42);
    }

    [Fact]
    public async Task Handle_NoAiConsent_ReturnsNullConsent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExportUserDataCommand(userId);
        var user = new UserBuilder().WithId(userId).Build();

        SetupMocks(userId, user);
        _mockAiConsentRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.Administration.Domain.Entities.UserAiConsent?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.AiConsent.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExportsUserPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExportUserDataCommand(userId);
        var user = new UserBuilder().WithId(userId).Build();

        SetupMocks(userId, user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Preferences.Should().NotBeNull();
        result.Preferences.Language.Should().NotBeNull();
        result.Preferences.Theme.Should().NotBeNull();
    }

    private void SetupMocks(Guid userId, User user, int memoryCount = 0)
    {
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _mockLibraryRepository
            .Setup(r => r.GetUserGamesAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.UserLibrary.Domain.Entities.UserLibraryEntry>().AsReadOnly());

        _mockChatThreadRepository
            .Setup(r => r.FindByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread>().AsReadOnly());

        _mockNotificationRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.UserNotifications.Domain.Aggregates.Notification>().AsReadOnly());

        _mockAiConsentRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.Administration.Domain.Entities.UserAiConsent?)null);

        _mockConversationMemoryRepository
            .Setup(r => r.CountByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memoryCount);

        _mockApiKeyRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey>());
    }
}