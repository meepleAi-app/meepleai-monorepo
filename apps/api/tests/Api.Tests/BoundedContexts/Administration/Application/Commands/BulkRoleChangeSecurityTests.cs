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
public class BulkRoleChangeSecurityTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<BulkRoleChangeCommandHandler>> _mockLogger;
    private readonly BulkRoleChangeCommandHandler _handler;

    public BulkRoleChangeSecurityTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<BulkRoleChangeCommandHandler>>();
        _handler = new BulkRoleChangeCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_AdminExceedsAdminSizeLimit_ThrowsForbiddenException()
    {
        // Admin tries to bulk change 101 users: blocked (Admin limit is 100)
        var requesterId = Guid.NewGuid();
        var requester = CreateTestUser(requesterId, "admin@test.com", "admin");
        var userIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);

        var command = new BulkRoleChangeCommand(userIds, "editor", requesterId);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*role limit*");
    }

    [Fact]
    public async Task Handle_AdminAssignsAdminRole_ThrowsForbiddenException()
    {
        // Admin (level 3) tries to bulk assign Admin (level 3): blocked (same level)
        var requesterId = Guid.NewGuid();
        var requester = CreateTestUser(requesterId, "admin@test.com", "admin");
        var userIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);

        var command = new BulkRoleChangeCommand(userIds, "admin", requesterId);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*privilege level*");
    }

    [Fact]
    public async Task Handle_AdminAssignsSuperAdminRole_ThrowsForbiddenException()
    {
        // Admin (level 3) tries to bulk assign SuperAdmin (level 4): blocked
        var requesterId = Guid.NewGuid();
        var requester = CreateTestUser(requesterId, "admin@test.com", "admin");
        var userIds = new List<Guid> { Guid.NewGuid() };

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);

        var command = new BulkRoleChangeCommand(userIds, "superadmin", requesterId);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*privilege level*");
    }

    [Fact]
    public async Task Handle_BulkDemotesLastSuperAdmin_ThrowsForbiddenException()
    {
        // Admin bulk-changes a batch that includes the only SuperAdmin: blocked.
        // System state: 1 SuperAdmin total (the target). Requester is Admin (not SuperAdmin),
        // so CountByRoleAsync(1) accurately reflects the real system count.
        // Guard: superAdminCount(1) - superAdminsInBatch(1) = 0 < 1 → ForbiddenException.
        var requesterId = Guid.NewGuid();
        var requester = CreateTestUser(requesterId, "admin1@test.com", "admin");
        var targetSuperAdminId = Guid.NewGuid();
        var targetSuperAdmin = CreateTestUser(targetSuperAdminId, "superadmin1@test.com", "superadmin");

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);
        _mockUserRepository.Setup(r => r.GetByIdAsync(targetSuperAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetSuperAdmin);
        // 1 total SuperAdmin in the system — the target being demoted
        _mockUserRepository.Setup(r => r.CountByRoleAsync("superadmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new BulkRoleChangeCommand(new List<Guid> { targetSuperAdminId }, "editor", requesterId);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*SuperAdmin*");
    }

    [Fact]
    public async Task Handle_RequesterNotFound_ThrowsDomainException()
    {
        // Requester ID does not exist in the repository
        var requesterId = Guid.NewGuid();
        var userIds = new List<Guid> { Guid.NewGuid() };

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new BulkRoleChangeCommand(userIds, "editor", requesterId);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<Api.SharedKernel.Domain.Exceptions.DomainException>()
            .WithMessage("*not found*");
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
