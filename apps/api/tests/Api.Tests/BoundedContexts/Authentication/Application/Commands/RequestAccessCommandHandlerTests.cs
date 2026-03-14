using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Moq;
using Xunit;
using AuthAccessRequest = Api.BoundedContexts.Authentication.Domain.Entities.AccessRequest;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class RequestAccessCommandHandlerTests
{
    private readonly Mock<IAccessRequestRepository> _accessRequestRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly RequestAccessCommandHandler _handler;

    public RequestAccessCommandHandlerTests()
    {
        _accessRequestRepoMock = new Mock<IAccessRequestRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _handler = new RequestAccessCommandHandler(
            _accessRequestRepoMock.Object,
            _userRepoMock.Object);
    }

    [Fact]
    public async Task Handle_NewEmail_CreatesAccessRequest()
    {
        _accessRequestRepoMock
            .Setup(x => x.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthAccessRequest?)null);
        _userRepoMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new RequestAccessCommand("new@example.com");
        await _handler.Handle(command, CancellationToken.None);

        _accessRequestRepoMock.Verify(
            x => x.AddAsync(It.Is<AuthAccessRequest>(r => r.Email == "new@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingAccount_DoesNotCreateRequest()
    {
        var existingUser = new User(
            Guid.NewGuid(),
            new Email("existing@example.com"),
            "Existing User",
            PasswordHash.Create("Password123!"),
            Role.User);

        _userRepoMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        _accessRequestRepoMock
            .Setup(x => x.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthAccessRequest?)null);

        var command = new RequestAccessCommand("existing@example.com");
        await _handler.Handle(command, CancellationToken.None);

        _accessRequestRepoMock.Verify(
            x => x.AddAsync(It.IsAny<AuthAccessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyPending_DoesNotCreateDuplicate()
    {
        var pendingRequest = AuthAccessRequest.Create("pending@example.com");
        _accessRequestRepoMock
            .Setup(x => x.GetPendingByEmailAsync("pending@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingRequest);
        _userRepoMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new RequestAccessCommand("pending@example.com");
        await _handler.Handle(command, CancellationToken.None);

        _accessRequestRepoMock.Verify(
            x => x.AddAsync(It.IsAny<AuthAccessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NormalizesEmail()
    {
        _accessRequestRepoMock
            .Setup(x => x.GetPendingByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthAccessRequest?)null);
        _userRepoMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new RequestAccessCommand("TEST@Example.COM");
        await _handler.Handle(command, CancellationToken.None);

        _accessRequestRepoMock.Verify(
            x => x.GetPendingByEmailAsync("test@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }
}
