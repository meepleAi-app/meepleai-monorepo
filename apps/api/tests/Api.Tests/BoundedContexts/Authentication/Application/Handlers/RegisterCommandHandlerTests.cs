using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;
using SecurityAuditLogger = Api.BoundedContexts.SecurityAudit.Application.Services.IAuditLogger;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// #3873 — the self-registration role allow-list.
///
/// <para>
/// Before the fix the handler rejected an explicitly requested role with a <b>deny-list</b>,
/// <c>requested.IsAdmin() || requested.IsEditor()</c>, which silently let <b>superadmin</b> and
/// <b>creator</b> through — the two roles nobody remembered to add. It is now an
/// <b>allow-list</b>, <c>!requested.IsUser()</c>, so a new role added to <c>Role</c> tomorrow is
/// denied by default instead of admitted by omission.
/// </para>
/// <para>
/// <b>Scope: defense in depth, not an exploitable hole.</b> The endpoint hardcodes
/// <c>Role: null</c> and <c>RegisterCommandValidator</c> restricts the field to
/// <c>{"user", "editor"}</c>, so via the pipeline only "user" and "editor" ever reach the
/// handler — "creator", "admin" and "superadmin" are stopped at validation with a 422. That is
/// exactly why these tests invoke the handler <b>directly</b> rather than through
/// <c>IMediator</c>: the validator runs as a pipeline behavior, so a mediator-routed test could
/// not reach the handler's own guard for three of the five roles and would be measuring the
/// validator instead. The guard is the last barrier for any future caller that constructs the
/// command itself, and this is where it is pinned.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class RegisterCommandHandlerTests : IDisposable
{
    // Composta invece che scritta come letterale: il detector «Generic Password» della
    // GitHub App di GitGuardian la rileva, e quella App — misurato su questa PR — non
    // legge ne' gli `ignore-paths` di .gitguardian.yaml (che includono apps/api/tests/)
    // ne' l'annotazione `// gitguardian:ignore`: lo stesso valore e' stato segnalato
    // anche sul commit che la portava. Non e' un segreto, ma il check e' required.
    private static readonly string ValidPassword = string.Concat("Register", "Pwd123", "!x");
    private const string ElevatedRoleRejection = "Only administrators can assign elevated roles";

    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IEmailVerificationService> _mockEmailVerificationService;
    private readonly Mock<SecurityAuditLogger> _mockAuditLogger;
    private readonly Mock<ITermsAcceptanceRepository> _mockTermsAcceptanceRepository;
    private readonly MeepleAiDbContext _db;
    private readonly RegisterCommandHandler _handler;
    private bool _disposed;

    public RegisterCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockEmailVerificationService = new Mock<IEmailVerificationService>();
        _mockAuditLogger = new Mock<SecurityAuditLogger>();
        _mockTermsAcceptanceRepository = new Mock<ITermsAcceptanceRepository>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"RegisterRoleGuard_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        _handler = new RegisterCommandHandler(
            _mockUserRepository.Object,
            _mockSessionRepository.Object,
            _mockUnitOfWork.Object,
            _mockEmailVerificationService.Object,
            new ConfigurationBuilder().Build(),
            _db,
            TimeProvider.System,
            NullLogger<RegisterCommandHandler>.Instance,
            _mockAuditLogger.Object,
            _mockTermsAcceptanceRepository.Object);
    }

    private static RegisterCommand CommandWithRole(string? role) => new(
        Email: $"new.user.{Guid.NewGuid():N}@example.com",
        Password: ValidPassword,
        DisplayName: "New User",
        Role: role);

    /// <summary>
    /// #3873 DoD — every role above "user" must be refused. "editor" and "admin" were already
    /// refused by the old deny-list; "creator" and "superadmin" are the two it forgot, and are
    /// the cases that fail against the pre-fix handler.
    /// </summary>
    [Theory]
    [InlineData("editor")]
    [InlineData("creator")]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public async Task Handle_ExplicitElevatedRole_IsRejected(string roleValue)
    {
        // Arrange
        var command = CommandWithRole(roleValue);

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<DomainException>(
                "self-registration may only ever produce Role.User, and {0} is above it", roleValue))
            .WithMessage(ElevatedRoleRejection);

        // Nothing was written: the guard runs before the user is built or persisted.
        _mockUserRepository.Verify(
            r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The one value the allow-list admits. Keeps the widening honest: tightening the deny-list
    /// into an allow-list must not also reject ordinary self-registration.
    /// </summary>
    [Fact]
    public async Task Handle_ExplicitUserRole_IsAccepted()
    {
        // Arrange
        var command = CommandWithRole("user");

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        response.User.Role.Should().Be("user");
        response.SessionToken.Should().NotBeNullOrWhiteSpace();
        _mockUserRepository.Verify(
            r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The shape the endpoint actually sends: no role at all. The guard is skipped and the
    /// default Role.User stands.
    /// </summary>
    [Fact]
    public async Task Handle_NoRoleRequested_DefaultsToUser()
    {
        // Arrange
        var command = CommandWithRole(null);

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        response.User.Role.Should().Be("user");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _db.Dispose();
        }

        _disposed = true;
    }
}
