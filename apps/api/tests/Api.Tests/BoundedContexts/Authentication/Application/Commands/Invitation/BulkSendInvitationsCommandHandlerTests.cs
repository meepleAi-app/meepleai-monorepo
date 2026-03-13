using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.Invitation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class BulkSendInvitationsCommandHandlerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ILogger<BulkSendInvitationsCommandHandler>> _loggerMock = new();
    private readonly BulkSendInvitationsCommandHandler _handler;

    public BulkSendInvitationsCommandHandlerTests()
    {
        _handler = new BulkSendInvitationsCommandHandler(_senderMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCsv_DispatchesSendForEachRow()
    {
        // Arrange
        var csv = "email,role\nuser1@test.com,User\nuser2@test.com,Editor";
        var invitedBy = Guid.NewGuid();
        var command = new BulkSendInvitationsCommand(csv, invitedBy);

        _senderMock
            .Setup(s => s.Send(It.IsAny<SendInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendInvitationCommand cmd, CancellationToken _) =>
                new InvitationDto(
                    Guid.NewGuid(), cmd.Email, cmd.Role, "Pending",
                    DateTime.UtcNow.AddDays(7), DateTime.UtcNow, null, invitedBy));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Successful.Should().HaveCount(2);
        result.Failed.Should().BeEmpty();
        _senderMock.Verify(s => s.Send(It.IsAny<SendInvitationCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ExceedsMaxLimit_ThrowsValidationException()
    {
        // Arrange — 101 rows
        var lines = new List<string> { "email,role" };
        for (int i = 0; i < 101; i++)
            lines.Add($"user{i}@test.com,User");

        var csv = string.Join("\n", lines);
        var command = new BulkSendInvitationsCommand(csv, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidRowsCollectedAsFailures()
    {
        // Arrange
        var csv = "email,role\ninvalid-email,User\nvalid@test.com,User";
        var invitedBy = Guid.NewGuid();
        var command = new BulkSendInvitationsCommand(csv, invitedBy);

        _senderMock
            .Setup(s => s.Send(It.IsAny<SendInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendInvitationCommand cmd, CancellationToken _) =>
                new InvitationDto(
                    Guid.NewGuid(), cmd.Email, cmd.Role, "Pending",
                    DateTime.UtcNow.AddDays(7), DateTime.UtcNow, null, invitedBy));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Successful.Should().HaveCount(1);
        result.Failed.Should().HaveCount(1);
        result.Failed[0].Email.Should().Be("invalid-email");
    }

    [Fact]
    public async Task Handle_EmptyCsv_ReturnsEmptyResults()
    {
        // Arrange
        var command = new BulkSendInvitationsCommand("", Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Successful.Should().BeEmpty();
        result.Failed.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HeaderRowSkipped()
    {
        // Arrange
        var csv = "email,role\nuser@test.com,Admin";
        var invitedBy = Guid.NewGuid();
        var command = new BulkSendInvitationsCommand(csv, invitedBy);

        _senderMock
            .Setup(s => s.Send(It.IsAny<SendInvitationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SendInvitationCommand cmd, CancellationToken _) =>
                new InvitationDto(
                    Guid.NewGuid(), cmd.Email, cmd.Role, "Pending",
                    DateTime.UtcNow.AddDays(7), DateTime.UtcNow, null, invitedBy));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — only 1 data row, header skipped
        result.Successful.Should().HaveCount(1);
        _senderMock.Verify(s => s.Send(
            It.Is<SendInvitationCommand>(c => c.Email == "user@test.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
