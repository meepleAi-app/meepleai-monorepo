using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class BulkPasswordResetSecurityTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<BulkPasswordResetCommandHandler>> _mockLogger;
    private readonly BulkPasswordResetCommandHandler _handler;

    public BulkPasswordResetSecurityTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<BulkPasswordResetCommandHandler>>();
        _handler = new BulkPasswordResetCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_AdminExceeds100UserLimit_ThrowsForbiddenException()
    {
        // Admin tries to reset 101 passwords: blocked (limit is 100)
        var requesterId = Guid.NewGuid();
        var requester = CreateTestUser(requesterId, "admin@test.com", "admin");
        var userIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);

        var command = new BulkPasswordResetCommand(userIds, "SecurePassword123!", requesterId);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*role limit*");
    }

    [Fact]
    public async Task Handle_AdminResets100Users_Succeeds()
    {
        // Admin resets exactly 100: allowed
        var requesterId = Guid.NewGuid();
        var requester = CreateTestUser(requesterId, "admin@test.com", "admin");
        var userIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);
        _mockUserRepository.Setup(r => r.GetByIdAsync(It.Is<Guid>(id => id != requesterId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => CreateTestUser(id, $"{id}@test.com", "user"));
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);
        _mockUserRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new BulkPasswordResetCommand(userIds, "SecurePassword123!", requesterId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.TotalRequested.Should().Be(100);
    }

    [Fact]
    public async Task Handle_SuperAdminResets1000Users_Succeeds()
    {
        // SuperAdmin resets exactly 1000: allowed
        var requesterId = Guid.NewGuid();
        var requester = CreateTestUser(requesterId, "superadmin@test.com", "superadmin");
        var userIds = Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid()).ToList();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);
        _mockUserRepository.Setup(r => r.GetByIdAsync(It.Is<Guid>(id => id != requesterId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => CreateTestUser(id, $"{id}@test.com", "user"));
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000);
        _mockUserRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new BulkPasswordResetCommand(userIds, "SecurePassword123!", requesterId);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.TotalRequested.Should().Be(1000);
    }

    [Fact]
    public async Task Handle_SuperAdminExceeds1000UserLimit_ThrowsDomainException()
    {
        // SuperAdmin tries to reset 1001 passwords: blocked by global MaxBulkSize
        var requesterId = Guid.NewGuid();
        var userIds = Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid()).ToList();

        var command = new BulkPasswordResetCommand(userIds, "SecurePassword123!", requesterId);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.DomainException>()
            .WithMessage("*maximum limit*");
    }

    private static User CreateTestUser(Guid id, string email, string role = "user")
    {
        var userRole = role.ToLowerInvariant() switch
        {
            "admin" => Role.Admin,
            "superadmin" => Role.SuperAdmin,
            "editor" => Role.Editor,
            "creator" => Role.Creator,
            _ => Role.User
        };

        return new User(
            id: id,
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: userRole);
    }
}
