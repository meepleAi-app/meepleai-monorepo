using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.RuleConflictFAQs;

/// <summary>
/// Unit tests for DeleteRuleConflictFaqCommandHandler.
/// Issue #3966: Handler tests for FAQ deletion.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class DeleteRuleConflictFaqCommandHandlerTests
{
    private readonly Mock<IRuleConflictFaqRepository> _faqRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DeleteRuleConflictFaqCommandHandler _handler;

    public DeleteRuleConflictFaqCommandHandlerTests()
    {
        _faqRepositoryMock = new Mock<IRuleConflictFaqRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new DeleteRuleConflictFaqCommandHandler(
            _faqRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidId_DeletesFaq()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var command = new DeleteRuleConflictFaqCommand(faqId);

        _faqRepositoryMock.Setup(r => r.DeleteAsync(faqId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _faqRepositoryMock.Verify(r => r.DeleteAsync(faqId, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var faqId = Guid.NewGuid();
        var command = new DeleteRuleConflictFaqCommand(faqId);

        _faqRepositoryMock.Setup(r => r.DeleteAsync(faqId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("RuleConflictFAQ", faqId.ToString()));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
