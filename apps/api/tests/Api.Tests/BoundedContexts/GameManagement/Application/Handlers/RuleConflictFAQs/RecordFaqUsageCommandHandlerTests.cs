using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.RuleConflictFAQs;

/// <summary>
/// Unit tests for RecordFaqUsageCommandHandler.
/// Issue #3966: Handler tests for FAQ usage tracking.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class RecordFaqUsageCommandHandlerTests
{
    private readonly Mock<IRuleConflictFaqRepository> _faqRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly RecordFaqUsageCommandHandler _handler;

    public RecordFaqUsageCommandHandlerTests()
    {
        _faqRepositoryMock = new Mock<IRuleConflictFaqRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _timeProviderMock = new Mock<TimeProvider>();

        _timeProviderMock.Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        _handler = new RecordFaqUsageCommandHandler(
            _faqRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidId_IncrementsUsageCount()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new RecordFaqUsageCommand(faqId);

        var existingFaq = RuleConflictFAQ.Create(
            faqId,
            gameId,
            ConflictType.Ambiguity,
            "test_pattern",
            "Test resolution",
            3,
            _timeProviderMock.Object);

        var initialUsageCount = existingFaq.UsageCount;

        _faqRepositoryMock.Setup(r => r.GetByIdAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFaq);

        _faqRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<RuleConflictFAQ>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        existingFaq.UsageCount.Should().Be(initialUsageCount + 1);
        _faqRepositoryMock.Verify(r => r.GetByIdAsync(faqId, It.IsAny<CancellationToken>()), Times.Once);
        _faqRepositoryMock.Verify(r => r.UpdateAsync(
            It.Is<RuleConflictFAQ>(f => f.UsageCount == initialUsageCount + 1),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var command = new RecordFaqUsageCommand(faqId);

        _faqRepositoryMock.Setup(r => r.GetByIdAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RuleConflictFAQ?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;

        exception.Message.Should().Contain("RuleConflictFAQ");
        exception.Message.Should().Contain(faqId.ToString());
        _faqRepositoryMock.Verify(r => r.UpdateAsync(
            It.IsAny<RuleConflictFAQ>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmitsRuleConflictFAQUsedEvent()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new RecordFaqUsageCommand(faqId);

        var existingFaq = RuleConflictFAQ.Create(
            faqId,
            gameId,
            ConflictType.Contradiction,
            "conflict_pattern",
            "Resolution text",
            7,
            _timeProviderMock.Object);

        // Clear creation event to verify usage event
        existingFaq.ClearDomainEvents();

        _faqRepositoryMock.Setup(r => r.GetByIdAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFaq);

        _faqRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<RuleConflictFAQ>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var domainEvents = existingFaq.DomainEvents;
        domainEvents.Should().ContainSingle();
        domainEvents.First().Should().BeOfType<RuleConflictFAQUsedEvent>();
        var usedEvent = (RuleConflictFAQUsedEvent)domainEvents.First();
        usedEvent.FAQId.Should().Be(faqId);
        usedEvent.GameId.Should().Be(gameId);
        usedEvent.TotalUsageCount.Should().Be(1); // First usage after creation
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new RecordFaqUsageCommandHandler(
                null!,
                _unitOfWorkMock.Object,
                _timeProviderMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new RecordFaqUsageCommandHandler(
                _faqRepositoryMock.Object,
                null!,
                _timeProviderMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new RecordFaqUsageCommandHandler(
                _faqRepositoryMock.Object,
                _unitOfWorkMock.Object,
                null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
