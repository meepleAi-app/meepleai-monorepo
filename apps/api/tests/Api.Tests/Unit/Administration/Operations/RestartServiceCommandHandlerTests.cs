using Api.BoundedContexts.Administration.Application.Commands.Operations;
using Api.BoundedContexts.Administration.Application.Commands.Operations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Administration.Operations;

/// <summary>
/// Unit tests for RestartServiceCommandHandler.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public sealed class RestartServiceCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepository;
    private readonly Mock<IHostApplicationLifetime> _mockLifetime;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<RestartServiceCommandHandler>> _mockLogger;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly RestartServiceCommandHandler _handler;

    public RestartServiceCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockAuditLogRepository = new Mock<IAuditLogRepository>();
        _mockLifetime = new Mock<IHostApplicationLifetime>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<RestartServiceCommandHandler>>();
        _mockTimeProvider = new Mock<TimeProvider>();

        var testTime = new DateTime(2026, 2, 7, 12, 0, 0, DateTimeKind.Utc);
        _mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(testTime));

        _handler = new RestartServiceCommandHandler(
            _mockUserRepository.Object,
            _mockAuditLogRepository.Object,
            _mockLifetime.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object,
            _mockTimeProvider.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_ValidSuperAdmin_InitiatesRestart()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var command = new RestartServiceCommand("API", adminId);

        var superAdmin = CreateSuperAdminUser(adminId);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(superAdmin);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Service restart initiated. Application shutting down gracefully.", result.Message);
        Assert.Equal("30-60 seconds", result.EstimatedDowntime);

        _mockAuditLogRepository.Verify(
            x => x.AddAsync(It.IsAny<Api.BoundedContexts.Administration.Domain.Entities.AuditLog>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockUnitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_NonSuperAdmin_ThrowsConflictException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var command = new RestartServiceCommand("API", adminId);

        var regularAdmin = CreateAdminUser(adminId);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(regularAdmin);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Equal("Only SuperAdmin can restart services", exception.Message);

        _mockAuditLogRepository.Verify(
            x => x.AddAsync(It.IsAny<Api.BoundedContexts.Administration.Domain.Entities.AuditLog>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_AdminNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var command = new RestartServiceCommand("API", adminId);

        _mockUserRepository
            .Setup(x => x.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(adminId.ToString(), exception.Message, StringComparison.Ordinal);
    }

    private static User CreateSuperAdminUser(Guid id)
    {
        return new User(
            id: id,
            email: Email.Parse("superadmin@test.com"),
            displayName: "Super Admin",
            passwordHash: PasswordHash.Create("hashedPassword"),
            role: Role.SuperAdmin,
            tier: UserTier.Premium
        );
    }

    private static User CreateAdminUser(Guid id)
    {
        return new User(
            id: id,
            email: Email.Parse("admin@test.com"),
            displayName: "Regular Admin",
            passwordHash: PasswordHash.Create("hashedPassword"),
            role: Role.Admin,
            tier: UserTier.Premium
        );
    }
}
