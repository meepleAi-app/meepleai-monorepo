using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Administration.Handlers;

/// <summary>
/// Unit tests for UpdateUserAiConsentCommandHandler (Issue #5512)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class UpdateUserAiConsentCommandHandlerTests
{
    private readonly Mock<IUserAiConsentRepository> _consentRepoMock;
    private readonly Mock<ILogger<UpdateUserAiConsentCommandHandler>> _loggerMock;
    private readonly UpdateUserAiConsentCommandHandler _handler;

    public UpdateUserAiConsentCommandHandlerTests()
    {
        _consentRepoMock = new Mock<IUserAiConsentRepository>();
        _loggerMock = new Mock<ILogger<UpdateUserAiConsentCommandHandler>>();
        _handler = new UpdateUserAiConsentCommandHandler(_consentRepoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_NewConsent_ShouldCreateRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new UpdateUserAiConsentCommand(userId, true, true, "1.0.0");
        _consentRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAiConsent?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _consentRepoMock.Verify(r => r.AddAsync(
            It.Is<UserAiConsent>(c =>
                c.UserId == userId &&
                c.ConsentedToAiProcessing &&
                c.ConsentedToExternalProviders &&
                c.ConsentVersion == "1.0.0"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingConsent_ShouldUpdateRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existing = UserAiConsent.Create(userId, false, false, "0.9.0");
        var command = new UpdateUserAiConsentCommand(userId, true, true, "1.0.0");
        _consentRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _consentRepoMock.Verify(r => r.UpdateAsync(
            It.Is<UserAiConsent>(c =>
                c.ConsentedToAiProcessing &&
                c.ConsentedToExternalProviders &&
                c.ConsentVersion == "1.0.0"),
            It.IsAny<CancellationToken>()), Times.Once);
        _consentRepoMock.Verify(r => r.AddAsync(
            It.IsAny<UserAiConsent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithdrawConsent_ShouldUpdateToFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existing = UserAiConsent.Create(userId, true, true, "1.0.0");
        var command = new UpdateUserAiConsentCommand(userId, false, false, "1.0.0");
        _consentRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _consentRepoMock.Verify(r => r.UpdateAsync(
            It.Is<UserAiConsent>(c =>
                !c.ConsentedToAiProcessing &&
                !c.ConsentedToExternalProviders),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
