using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
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
/// Unit tests for CreateRuleConflictFaqCommandHandler.
/// Issue #3966: Handler tests for FAQ creation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class CreateRuleConflictFaqCommandHandlerTests
{
    private readonly Mock<IRuleConflictFaqRepository> _faqRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly CreateRuleConflictFaqCommandHandler _handler;

    public CreateRuleConflictFaqCommandHandlerTests()
    {
        _faqRepositoryMock = new Mock<IRuleConflictFaqRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _timeProviderMock = new Mock<TimeProvider>();

        _timeProviderMock.Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        _handler = new CreateRuleConflictFaqCommandHandler(
            _faqRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesAndReturnsFaqId()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateRuleConflictFaqCommand(
            gameId,
            ConflictType.Contradiction,
            "setup_vs_turn_order",
            "Setup actions occur first",
            5
        );

        _faqRepositoryMock.Setup(r => r.FindByPatternAsync(
                gameId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RuleConflictFAQ?)null); // No duplicate

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBe(Guid.Empty);
        _faqRepositoryMock.Verify(r => r.AddAsync(
            It.Is<RuleConflictFAQ>(f =>
                f.GameId == gameId &&
                f.Pattern == "setup_vs_turn_order" &&
                f.Priority == 5),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicatePattern_ThrowsConflictException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateRuleConflictFaqCommand(
            gameId,
            ConflictType.Contradiction,
            "existing_pattern",
            "Test resolution",
            5
        );

        var existingFaq = RuleConflictFAQ.Create(
            Guid.NewGuid(),
            gameId,
            ConflictType.Ambiguity,
            "existing_pattern",
            "Existing resolution",
            3,
            _timeProviderMock.Object);

        _faqRepositoryMock.Setup(r => r.FindByPatternAsync(
                gameId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFaq);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<ConflictException>()).Which;

        exception.Message.Should().Contain("already exists");
        _faqRepositoryMock.Verify(r => r.AddAsync(
            It.IsAny<RuleConflictFAQ>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
            new CreateRuleConflictFaqCommandHandler(
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
            new CreateRuleConflictFaqCommandHandler(
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
            new CreateRuleConflictFaqCommandHandler(
                _faqRepositoryMock.Object,
                _unitOfWorkMock.Object,
                null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
