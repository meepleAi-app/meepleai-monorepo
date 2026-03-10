using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for DeleteUserLlmDataCommandHandler — GDPR Art. 17 right to erasure.
/// Issue #5509: Verifies all LLM data stores are cleared for a user.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class DeleteUserLlmDataCommandHandlerTests
{
    private readonly Mock<ILlmRequestLogRepository> _llmRepoMock;
    private readonly Mock<IConversationMemoryRepository> _memoryRepoMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDatabaseMock;
    private readonly DeleteUserLlmDataCommandHandler _handler;

    public DeleteUserLlmDataCommandHandlerTests()
    {
        _llmRepoMock = new Mock<ILlmRequestLogRepository>();
        _memoryRepoMock = new Mock<IConversationMemoryRepository>();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDatabaseMock = new Mock<IDatabase>();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_redisDatabaseMock.Object);

        _handler = new DeleteUserLlmDataCommandHandler(
            _llmRepoMock.Object,
            _memoryRepoMock.Object,
            _redisMock.Object,
            Mock.Of<ILogger<DeleteUserLlmDataCommandHandler>>());
    }

    [Fact]
    public async Task Handle_DeletesFromAllThreeStores()
    {
        var userId = Guid.NewGuid();
        var command = new DeleteUserLlmDataCommand(userId, Guid.NewGuid(), IsAdminRequest: false);

        _llmRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _memoryRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        _redisDatabaseMock
            .Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.LlmRequestLogsDeleted.Should().Be(5);
        result.ConversationMemoriesDeleted.Should().Be(3);
        result.RedisKeysCleared.Should().BeTrue();
        result.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _llmRepoMock.Verify(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _memoryRepoMock.Verify(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ClearsRedisSessionKeys()
    {
        var userId = Guid.NewGuid();
        var command = new DeleteUserLlmDataCommand(userId, Guid.NewGuid(), IsAdminRequest: true);
        var sessionKeys = new RedisValue[] { "session:abc", "session:def" };

        _llmRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _memoryRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _redisDatabaseMock
            .Setup(d => d.SetMembersAsync(
                (RedisKey)$"user_sessions:{userId}",
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(sessionKeys);

        _redisDatabaseMock
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(2);

        _redisDatabaseMock
            .Setup(d => d.KeyDeleteAsync(
                (RedisKey)$"user_sessions:{userId}",
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.RedisKeysCleared.Should().BeTrue();

        // Verify both session keys and the set key itself are deleted
        _redisDatabaseMock.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(keys => keys.Length == 2),
            It.IsAny<CommandFlags>()), Times.Once);

        _redisDatabaseMock.Verify(d => d.KeyDeleteAsync(
            (RedisKey)$"user_sessions:{userId}",
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RedisFailure_ReturnsFalseForRedisKeysCleared()
    {
        var userId = Guid.NewGuid();
        var command = new DeleteUserLlmDataCommand(userId, Guid.NewGuid(), IsAdminRequest: false);

        _llmRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _memoryRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _redisDatabaseMock
            .Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Connection refused"));

        var result = await _handler.Handle(command, CancellationToken.None);

        // DB deletes should still succeed even if Redis fails
        result.LlmRequestLogsDeleted.Should().Be(2);
        result.ConversationMemoriesDeleted.Should().Be(1);
        result.RedisKeysCleared.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoDataToDelete_ReturnsZeroCounts()
    {
        var userId = Guid.NewGuid();
        var command = new DeleteUserLlmDataCommand(userId, Guid.NewGuid(), IsAdminRequest: false);

        _llmRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _memoryRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _redisDatabaseMock
            .Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.LlmRequestLogsDeleted.Should().Be(0);
        result.ConversationMemoriesDeleted.Should().Be(0);
        result.RedisKeysCleared.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ThrowsOnNullRequest()
    {
        var act = () => _handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullLlmRepository()
    {
        var act = () => new DeleteUserLlmDataCommandHandler(
            null!,
            _memoryRepoMock.Object,
            _redisMock.Object,
            Mock.Of<ILogger<DeleteUserLlmDataCommandHandler>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullConversationMemoryRepository()
    {
        var act = () => new DeleteUserLlmDataCommandHandler(
            _llmRepoMock.Object,
            null!,
            _redisMock.Object,
            Mock.Of<ILogger<DeleteUserLlmDataCommandHandler>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullRedis()
    {
        var act = () => new DeleteUserLlmDataCommandHandler(
            _llmRepoMock.Object,
            _memoryRepoMock.Object,
            null!,
            Mock.Of<ILogger<DeleteUserLlmDataCommandHandler>>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        var act = () => new DeleteUserLlmDataCommandHandler(
            _llmRepoMock.Object,
            _memoryRepoMock.Object,
            _redisMock.Object,
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_AdminRequest_SetsCorrectFlags()
    {
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var command = new DeleteUserLlmDataCommand(userId, adminId, IsAdminRequest: true);

        _llmRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _memoryRepoMock
            .Setup(r => r.DeleteByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _redisDatabaseMock
            .Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.LlmRequestLogsDeleted.Should().Be(1);
        result.ConversationMemoriesDeleted.Should().Be(1);
    }

    [Fact]
    public void DeleteUserLlmDataCommand_PropertiesAreSet()
    {
        var userId = Guid.NewGuid();
        var requestedBy = Guid.NewGuid();

        var command = new DeleteUserLlmDataCommand(userId, requestedBy, IsAdminRequest: true);

        command.UserId.Should().Be(userId);
        command.RequestedByUserId.Should().Be(requestedBy);
        command.IsAdminRequest.Should().BeTrue();
    }

    [Fact]
    public void DeleteUserLlmDataResult_PropertiesAreSet()
    {
        var deletedAt = DateTime.UtcNow;
        var result = new DeleteUserLlmDataResult(
            LlmRequestLogsDeleted: 10,
            ConversationMemoriesDeleted: 5,
            RedisKeysCleared: true,
            DeletedAt: deletedAt);

        result.LlmRequestLogsDeleted.Should().Be(10);
        result.ConversationMemoriesDeleted.Should().Be(5);
        result.RedisKeysCleared.Should().BeTrue();
        result.DeletedAt.Should().Be(deletedAt);
    }
}
