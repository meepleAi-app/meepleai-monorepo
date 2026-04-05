using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class ChangeUserRoleCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly ChangeUserRoleCommandHandler _handler;

    public ChangeUserRoleCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new ChangeUserRoleCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_AdminAssignsEditor_Succeeds()
    {
        // Arrange — Admin (level 3) assigns Editor (level 2): allowed
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId, "user@test.com", "user");

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ChangeUserRoleCommand(userId.ToString(), "Editor", "Promotion", AdminRole: "admin");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AdminAssignsAdmin_ThrowsForbiddenException()
    {
        // Arrange — Admin (level 3) tries to assign Admin (level 3): blocked (same level)
        var userId = Guid.NewGuid();

        var command = new ChangeUserRoleCommand(userId.ToString(), "Admin", null, AdminRole: "admin");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*privilege level*");
    }

    [Fact]
    public async Task Handle_EditorAssignsEditor_ThrowsForbiddenException()
    {
        // Arrange — Editor (level 2) tries to assign Editor (level 2): blocked (same level)
        var userId = Guid.NewGuid();

        var command = new ChangeUserRoleCommand(userId.ToString(), "Editor", null, AdminRole: "editor");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_DemoteLastSuperAdmin_ThrowsForbiddenException()
    {
        // Arrange — Demoting the only SuperAdmin to Admin: blocked
        var userId = Guid.NewGuid();
        var superAdminUser = CreateTestUser(userId, "superadmin@test.com", "superadmin");

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(superAdminUser);
        _mockUserRepository.Setup(r => r.CountByRoleAsync("superadmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new ChangeUserRoleCommand(userId.ToString(), "Admin", null, AdminRole: "superadmin");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*last SuperAdmin*");
    }

    [Fact]
    public async Task Handle_DemoteOneSuperAdminWhenMultipleExist_PassesLastSuperAdminGuard()
    {
        // Arrange — When 2 SuperAdmins exist, the last-SuperAdmin ForbiddenException is NOT thrown.
        // The domain entity's own immutability guard fires instead (DomainException, not ForbiddenException).
        var userId = Guid.NewGuid();
        var superAdminUser = CreateTestUser(userId, "superadmin2@test.com", "superadmin");

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(superAdminUser);
        _mockUserRepository.Setup(r => r.CountByRoleAsync("superadmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var command = new ChangeUserRoleCommand(userId.ToString(), "Admin", null, AdminRole: "superadmin");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert — passes the last-SuperAdmin guard (no ForbiddenException about "last SuperAdmin"),
        // but domain entity itself prevents demotion with a DomainException
        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.DomainException>()
            .WithMessage("*SuperAdmin role is immutable*");
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
