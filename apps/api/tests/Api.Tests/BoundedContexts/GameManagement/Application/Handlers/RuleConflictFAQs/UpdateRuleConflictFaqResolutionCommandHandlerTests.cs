using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Application.Handlers.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.RuleConflictFAQs;

/// <summary>
/// Unit tests for UpdateRuleConflictFaqResolutionCommandHandler.
/// Issue #3966: Handler tests for FAQ resolution updates.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class UpdateRuleConflictFaqResolutionCommandHandlerTests
{
    private readonly Mock<IRuleConflictFaqRepository> _faqRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly UpdateRuleConflictFaqResolutionCommandHandler _handler;

    public UpdateRuleConflictFaqResolutionCommandHandlerTests()
    {
        _faqRepositoryMock = new Mock<IRuleConflictFaqRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _timeProviderMock = new Mock<TimeProvider>();

        _timeProviderMock.Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        _handler = new UpdateRuleConflictFaqResolutionCommandHandler(
            _faqRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesResolution()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new UpdateRuleConflictFaqResolutionCommand(
            faqId,
            "Updated resolution text"
        );

        var existingFaq = RuleConflictFAQ.Create(
            faqId,
            gameId,
            ConflictType.Contradiction,
            "test_pattern",
            "Original resolution",
            5,
            _timeProviderMock.Object);

        _faqRepositoryMock.Setup(r => r.GetByIdAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFaq);

        _faqRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<RuleConflictFAQ>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Updated resolution text", existingFaq.Resolution);
        _faqRepositoryMock.Verify(r => r.GetByIdAsync(faqId, It.IsAny<CancellationToken>()), Times.Once);
        _faqRepositoryMock.Verify(r => r.UpdateAsync(
            It.Is<RuleConflictFAQ>(f => f.Resolution == "Updated resolution text"),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var command = new UpdateRuleConflictFaqResolutionCommand(
            faqId,
            "New resolution"
        );

        _faqRepositoryMock.Setup(r => r.GetByIdAsync(faqId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RuleConflictFAQ?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));

        Assert.Contains("RuleConflictFAQ", exception.Message);
        Assert.Contains(faqId.ToString(), exception.Message);
        _faqRepositoryMock.Verify(r => r.UpdateAsync(
            It.IsAny<RuleConflictFAQ>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateRuleConflictFaqResolutionCommandHandler(
                null!,
                _unitOfWorkMock.Object,
                _timeProviderMock.Object));
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateRuleConflictFaqResolutionCommandHandler(
                _faqRepositoryMock.Object,
                null!,
                _timeProviderMock.Object));
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UpdateRuleConflictFaqResolutionCommandHandler(
                _faqRepositoryMock.Object,
                _unitOfWorkMock.Object,
                null!));
    }
}
