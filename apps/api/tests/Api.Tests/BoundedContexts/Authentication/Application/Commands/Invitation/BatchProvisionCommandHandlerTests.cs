using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Middleware.Exceptions;
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
public class BatchProvisionCommandHandlerTests
{
    private readonly Mock<ISender> _senderMock = new();
    private readonly Mock<ILogger<BatchProvisionCommandHandler>> _loggerMock = new();
    private readonly BatchProvisionCommandHandler _handler;

    public BatchProvisionCommandHandlerTests()
    {
        _handler = new BatchProvisionCommandHandler(_senderMock.Object, _loggerMock.Object);
    }

    private static BatchInvitationItemDto CreateItem(
        string email = "user@example.com",
        string displayName = "Test User",
        string role = "User",
        string tier = "Free",
        string? customMessage = null,
        int expiresInDays = 7,
        List<GameSuggestionDto>? gameSuggestions = null)
    {
        return new BatchInvitationItemDto(
            email, displayName, role, tier,
            customMessage, expiresInDays,
            gameSuggestions ?? new List<GameSuggestionDto>());
    }

    private static InvitationDto CreateSuccessDto(string email, Guid invitedBy)
    {
        return new InvitationDto(
            Id: Guid.NewGuid(),
            Email: email,
            Role: "User",
            Status: "Pending",
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            CreatedAt: DateTime.UtcNow,
            AcceptedAt: null,
            InvitedByUserId: invitedBy,
            EmailSent: true);
    }

    [Fact]
    public async Task Handle_AllSucceed_ReturnsAllInSucceeded()
    {
        // Arrange
        var invitedBy = Guid.NewGuid();
        var items = new List<BatchInvitationItemDto>
        {
            CreateItem(email: "user1@example.com", displayName: "User One"),
            CreateItem(email: "user2@example.com", displayName: "User Two"),
            CreateItem(email: "user3@example.com", displayName: "User Three")
        };
        var command = new BatchProvisionCommand(items, invitedBy);

        _senderMock
            .Setup(s => s.Send(It.IsAny<ProvisionAndInviteUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProvisionAndInviteUserCommand cmd, CancellationToken _) =>
                CreateSuccessDto(cmd.Email, invitedBy));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().HaveCount(3);
        result.Failed.Should().BeEmpty();
        _senderMock.Verify(
            s => s.Send(It.IsAny<ProvisionAndInviteUserCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_PartialFailure_DuplicateEmail_ContinuesProcessing()
    {
        // Arrange
        var invitedBy = Guid.NewGuid();
        var items = new List<BatchInvitationItemDto>
        {
            CreateItem(email: "good1@example.com", displayName: "Good One"),
            CreateItem(email: "duplicate@example.com", displayName: "Duplicate"),
            CreateItem(email: "good2@example.com", displayName: "Good Two")
        };
        var command = new BatchProvisionCommand(items, invitedBy);

        _senderMock
            .Setup(s => s.Send(
                It.Is<ProvisionAndInviteUserCommand>(c => c.Email != "duplicate@example.com"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProvisionAndInviteUserCommand cmd, CancellationToken _) =>
                CreateSuccessDto(cmd.Email, invitedBy));

        _senderMock
            .Setup(s => s.Send(
                It.Is<ProvisionAndInviteUserCommand>(c => c.Email == "duplicate@example.com"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("A user with email 'duplicate@example.com' already exists"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().HaveCount(2);
        result.Failed.Should().HaveCount(1);
        result.Failed[0].Email.Should().Be("duplicate@example.com");
        result.Failed[0].Error.Should().Contain("pending invitation or user already exists");

        // All 3 items should be attempted — no early abort
        _senderMock.Verify(
            s => s.Send(It.IsAny<ProvisionAndInviteUserCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_AllFail_ReturnsAllInFailed()
    {
        // Arrange
        var invitedBy = Guid.NewGuid();
        var items = new List<BatchInvitationItemDto>
        {
            CreateItem(email: "dup1@example.com"),
            CreateItem(email: "dup2@example.com"),
            CreateItem(email: "dup3@example.com")
        };
        var command = new BatchProvisionCommand(items, invitedBy);

        _senderMock
            .Setup(s => s.Send(It.IsAny<ProvisionAndInviteUserCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("User already exists"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeEmpty();
        result.Failed.Should().HaveCount(3);
        result.Failed.Should().AllSatisfy(f =>
            f.Error.Should().Contain("pending invitation or user already exists"));
    }

    [Fact]
    public async Task Handle_TracksEmailSentPerItem()
    {
        // Arrange
        var invitedBy = Guid.NewGuid();
        var items = new List<BatchInvitationItemDto>
        {
            CreateItem(email: "sent@example.com", displayName: "Email Sent"),
            CreateItem(email: "notsent@example.com", displayName: "Email Failed")
        };
        var command = new BatchProvisionCommand(items, invitedBy);

        _senderMock
            .Setup(s => s.Send(
                It.Is<ProvisionAndInviteUserCommand>(c => c.Email == "sent@example.com"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvitationDto(
                Guid.NewGuid(), "sent@example.com", "User", "Pending",
                DateTime.UtcNow.AddDays(7), DateTime.UtcNow, null, invitedBy,
                EmailSent: true));

        _senderMock
            .Setup(s => s.Send(
                It.Is<ProvisionAndInviteUserCommand>(c => c.Email == "notsent@example.com"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvitationDto(
                Guid.NewGuid(), "notsent@example.com", "User", "Pending",
                DateTime.UtcNow.AddDays(7), DateTime.UtcNow, null, invitedBy,
                EmailSent: false));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().HaveCount(2);
        result.Succeeded.Should().ContainSingle(d => d.Email == "sent@example.com" && d.EmailSent);
        result.Succeeded.Should().ContainSingle(d => d.Email == "notsent@example.com" && !d.EmailSent);
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsEmptyResults()
    {
        // Arrange
        var command = new BatchProvisionCommand(new List<BatchInvitationItemDto>(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeEmpty();
        result.Failed.Should().BeEmpty();
        _senderMock.Verify(
            s => s.Send(It.IsAny<ProvisionAndInviteUserCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ExceedsMaxItems_ThrowsValidationException()
    {
        // Arrange
        var items = Enumerable.Range(0, 101)
            .Select(i => CreateItem(email: $"user{i}@example.com"))
            .ToList();
        var command = new BatchProvisionCommand(items, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PassesCorrectDataToProvisionCommand()
    {
        // Arrange
        var invitedBy = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var item = CreateItem(
            email: "test@example.com",
            displayName: "Test User",
            role: "Editor",
            tier: "Premium",
            customMessage: "Welcome!",
            expiresInDays: 14,
            gameSuggestions: new List<GameSuggestionDto> { new(gameId, "PreAdded") });

        var command = new BatchProvisionCommand(new List<BatchInvitationItemDto> { item }, invitedBy);

        ProvisionAndInviteUserCommand? capturedCommand = null;
        _senderMock
            .Setup(s => s.Send(It.IsAny<ProvisionAndInviteUserCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<InvitationDto>, CancellationToken>((cmd, _) =>
                capturedCommand = cmd as ProvisionAndInviteUserCommand)
            .ReturnsAsync(CreateSuccessDto("test@example.com", invitedBy));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Email.Should().Be("test@example.com");
        capturedCommand.DisplayName.Should().Be("Test User");
        capturedCommand.Role.Should().Be("Editor");
        capturedCommand.Tier.Should().Be("Premium");
        capturedCommand.CustomMessage.Should().Be("Welcome!");
        capturedCommand.ExpiresInDays.Should().Be(14);
        capturedCommand.GameSuggestions.Should().HaveCount(1);
        capturedCommand.GameSuggestions[0].GameId.Should().Be(gameId);
        capturedCommand.InvitedByUserId.Should().Be(invitedBy);
    }

    [Fact]
    public async Task Handle_UnexpectedException_MapsToGenericError()
    {
        // Arrange
        var invitedBy = Guid.NewGuid();
        var items = new List<BatchInvitationItemDto>
        {
            CreateItem(email: "crash@example.com")
        };
        var command = new BatchProvisionCommand(items, invitedBy);

        _senderMock
            .Setup(s => s.Send(It.IsAny<ProvisionAndInviteUserCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection lost"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeEmpty();
        result.Failed.Should().HaveCount(1);
        result.Failed[0].Email.Should().Be("crash@example.com");
        result.Failed[0].Error.Should().Be("Failed to provision invitation");
    }
}
