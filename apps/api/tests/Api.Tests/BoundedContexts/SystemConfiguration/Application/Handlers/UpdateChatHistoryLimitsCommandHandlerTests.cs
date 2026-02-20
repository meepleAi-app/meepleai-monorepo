using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;
using Api.Tests.Constants;
using SystemConfigurationEntity = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Unit tests for UpdateChatHistoryLimitsCommandHandler.
/// Issue #4918: Admin system config — chat history tier limits.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class UpdateChatHistoryLimitsCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly UpdateChatHistoryLimitsCommandHandler _handler;

    public UpdateChatHistoryLimitsCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _handler = new UpdateChatHistoryLimitsCommandHandler(
            _mockMediator.Object,
            _mockConfigRepository.Object
        );
    }

    [Fact]
    public async Task Handle_WhenNoExistingConfigs_CreatesAllThreeLimits()
    {
        // Arrange: no configs in DB → creates new ones
        _mockConfigRepository.Setup(r => r.GetByKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationEntity?)null);

        _mockMediator.Setup(m => m.Send(
                It.IsAny<IRequest<object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());

        var adminId = Guid.NewGuid();
        var command = new UpdateChatHistoryLimitsCommand(
            FreeTierLimit: 15,
            NormalTierLimit: 150,
            PremiumTierLimit: 1500,
            UpdatedByUserId: adminId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FreeTierLimit.Should().Be(15);
        result.NormalTierLimit.Should().Be(150);
        result.PremiumTierLimit.Should().Be(1500);

        // Verify CreateConfigurationCommand was sent 3 times
        _mockMediator.Verify(
            m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        _mockMediator.Verify(
            m => m.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAllExistingConfigs_UpdatesAllThreeLimits()
    {
        // Arrange: all configs exist in DB → updates existing ones
        _mockConfigRepository.Setup(r => r.GetByKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigEntity("10"));

        _mockMediator.Setup(m => m.Send(
                It.IsAny<IRequest<object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());

        var adminId = Guid.NewGuid();
        var command = new UpdateChatHistoryLimitsCommand(
            FreeTierLimit: 20,
            NormalTierLimit: 200,
            PremiumTierLimit: 2000,
            UpdatedByUserId: adminId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FreeTierLimit.Should().Be(20);
        result.NormalTierLimit.Should().Be(200);
        result.PremiumTierLimit.Should().Be(2000);

        // Verify UpdateConfigValueCommand was sent 3 times
        _mockMediator.Verify(
            m => m.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        _mockMediator.Verify(
            m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMixedExistence_CreatesAndUpdatesCorrectly()
    {
        // Arrange: only Free tier exists → create Normal+Premium, update Free
        _mockConfigRepository.Setup(r => r.GetByKeyAsync(
                GetChatHistoryLimitsQueryHandler.FreeTierKey,
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateConfigEntity("10"));

        _mockConfigRepository.Setup(r => r.GetByKeyAsync(
                It.IsIn(
                    GetChatHistoryLimitsQueryHandler.NormalTierKey,
                    GetChatHistoryLimitsQueryHandler.PremiumTierKey),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationEntity?)null);

        _mockMediator.Setup(m => m.Send(
                It.IsAny<IRequest<object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());

        var command = new UpdateChatHistoryLimitsCommand(10, 100, 1000, Guid.NewGuid());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert: 1 update (Free) + 2 creates (Normal, Premium)
        _mockMediator.Verify(
            m => m.Send(It.IsAny<UpdateConfigValueCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMediator.Verify(
            m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ReturnsLastUpdatedAtAsUtcNow()
    {
        // Arrange
        _mockConfigRepository.Setup(r => r.GetByKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationEntity?)null);

        _mockMediator.Setup(m => m.Send(
                It.IsAny<IRequest<object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());

        var before = DateTime.UtcNow;
        var command = new UpdateChatHistoryLimitsCommand(10, 100, 1000, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        var after = DateTime.UtcNow;

        // Assert
        result.LastUpdatedAt.Should().BeOnOrAfter(before);
        result.LastUpdatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_ReturnsUpdatedByUserIdAsString()
    {
        // Arrange
        _mockConfigRepository.Setup(r => r.GetByKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationEntity?)null);

        _mockMediator.Setup(m => m.Send(
                It.IsAny<IRequest<object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());

        var adminId = Guid.NewGuid();
        var command = new UpdateChatHistoryLimitsCommand(10, 100, 1000, adminId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.LastUpdatedByUserId.Should().Be(adminId.ToString());
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static SystemConfigurationEntity CreateConfigEntity(string value)
    {
        return new SystemConfigurationEntity(
            id: Guid.NewGuid(),
            key: new ConfigKey("ChatHistory:FreeTierLimit"),
            value: value,
            valueType: "int",
            createdByUserId: Guid.NewGuid(),
            category: "ChatHistory");
    }
}
