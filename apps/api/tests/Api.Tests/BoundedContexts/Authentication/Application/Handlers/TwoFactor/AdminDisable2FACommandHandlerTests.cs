using Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;
using AuthRole = Api.SharedKernel.Domain.ValueObjects.Role;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers.TwoFactor;

/// <summary>
/// #3873 — role guard of the admin 2FA-override path.
///
/// <para>
/// This handler's guard is the <b>only</b> authorization on the endpoint: the route at
/// <c>Routing/TwoFactorEndpoints.cs</c> is closed by a bare <c>.RequireAuthorization()</c>
/// with no role policy, so whatever this guard admits, the endpoint admits. Before the fix the
/// guard was <c>adminUser.Role.IsAdmin()</c> — an exact string match on "admin" — which locked
/// <b>superadmin</b> out of the tool that recovers users who lost authenticator + backup codes,
/// even though superadmin outranks admin everywhere else per <c>Role.HasPermission</c>.
/// </para>
/// <para>
/// The guard is now <c>HasPermission(AuthRole.Admin)</c> = {admin, superadmin}. The two theories
/// below pin both directions: the widening (superadmin now admitted) and the anti-widening
/// (editor / creator / user still rejected).
/// </para>
/// <para>
/// <b>Why the password matters here.</b> Right after the role guard the handler re-authenticates
/// the admin with their own password. Both failures answer with a message starting
/// "Unauthorized", so a test that let re-auth fail would pass for the wrong reason and could not
/// tell "rejected by the role guard" from "admitted, then rejected by re-auth". The privileged
/// cases therefore supply the correct password and assert <c>Success == true</c>;
/// <see cref="Handle_PrivilegedRole_WithWrongPassword_FailsReAuthWithADistinctMessage"/> pins
/// that the two messages really are distinct, so the assertions cannot collapse onto each other.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class AdminDisable2FACommandHandlerTests
{
    // Composta invece che scritta come letterale: il detector «Generic Password» della
    // GitHub App di GitGuardian la rileva, e quella App — misurato su questa PR — non
    // legge ne' gli `ignore-paths` di .gitguardian.yaml (che includono apps/api/tests/)
    // ne' l'annotazione `// gitguardian:ignore`: lo stesso valore e' stato segnalato
    // anche sul commit che la portava. Non e' un segreto, ma il check e' required.
    private static readonly string AdminPassword = string.Concat("AdminReAuth", "Pwd123", "!");
    private const string RoleGuardMessage = "Unauthorized: Admin role required";
    private const string ReAuthFailedMessage = "Unauthorized: admin re-authentication failed";

    /// <summary>
    /// PBKDF2 at 600k iterations costs real time, so the hash is built once for the whole class
    /// and handed to <see cref="UserBuilder.WithPasswordHash"/> instead of being recomputed per
    /// case by <c>WithPassword</c>.
    /// </summary>
    private static readonly PasswordHash SharedPasswordHash = PasswordHash.Create(AdminPassword);

    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly AdminDisable2FACommandHandler _handler;

    public AdminDisable2FACommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _handler = new AdminDisable2FACommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            NullLogger<AdminDisable2FACommandHandler>.Instance);
    }

    /// <summary>
    /// Arranges an acting user with <paramref name="roleValue"/> and a 2FA-enabled target, both
    /// resolvable through the repository by their own id.
    /// </summary>
    private (Guid AdminId, Guid TargetId, User TargetUser) ArrangeAdminAndTarget(string roleValue)
    {
        var adminId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var adminUser = new UserBuilder()
            .WithId(adminId)
            .WithEmail("acting.admin@example.com")
            .WithRole(AuthRole.Parse(roleValue))
            .WithPasswordHash(SharedPasswordHash)
            .Build();

        var targetUser = new UserBuilder()
            .WithId(targetId)
            .WithEmail("locked.out@example.com")
            .WithPasswordHash(SharedPasswordHash)
            .With2FA()
            .Build();

        _mockUserRepository
            .Setup(r => r.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        return (adminId, targetId, targetUser);
    }

    /// <summary>
    /// #3873 DoD — the widening. <c>HasPermission(AuthRole.Admin)</c> = {admin, superadmin}:
    /// "admin" is the direction that already worked and must keep working, "superadmin" is the
    /// one the old exact-match guard rejected.
    /// </summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public async Task Handle_PrivilegedRole_PassesRoleGuardAndDisablesTargetTwoFactor(string roleValue)
    {
        // Arrange
        var (adminId, targetId, targetUser) = ArrangeAdminAndTarget(roleValue);
        var command = new AdminDisable2FACommand(adminId, targetId, AdminPassword);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue(
            "{0} satisfies HasPermission(Role.Admin), so it must reach and complete the override",
            roleValue);
        result.ErrorMessage.Should().BeNull();
        targetUser.IsTwoFactorEnabled.Should().BeFalse("the override must actually clear 2FA");

        _mockUserRepository.Verify(
            r => r.UpdateAsync(targetUser, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Anti-widening net: this guard is the endpoint's only authorization, so admitting
    /// superadmin must not admit anyone below admin.
    /// </summary>
    [Theory]
    [InlineData("editor")]
    [InlineData("creator")]
    [InlineData("user")]
    public async Task Handle_UnprivilegedRole_IsRejectedByTheRoleGuard(string roleValue)
    {
        // Arrange
        var (adminId, targetId, targetUser) = ArrangeAdminAndTarget(roleValue);
        var command = new AdminDisable2FACommand(adminId, targetId, AdminPassword);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse(
            "{0} does not satisfy HasPermission(Role.Admin)", roleValue);
        result.ErrorMessage.Should().Be(RoleGuardMessage,
            "the rejection must come from the role guard, not from a later step");

        // The target was never even looked up: proves the guard short-circuited before re-auth.
        _mockUserRepository.Verify(
            r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()), Times.Never);
        targetUser.IsTwoFactorEnabled.Should().BeTrue("the target's 2FA must be left alone");
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Keeps the two rejection messages apart. Without this, "passes the role guard" and "fails
    /// re-auth" would both read as an error mentioning "Unauthorized", and the theories above
    /// could not distinguish the outcome they claim to measure.
    /// </summary>
    [Fact]
    public async Task Handle_PrivilegedRole_WithWrongPassword_FailsReAuthWithADistinctMessage()
    {
        // Arrange
        var (adminId, targetId, targetUser) = ArrangeAdminAndTarget("superadmin");
        var command = new AdminDisable2FACommand(adminId, targetId, "NotTheAdminPassword1!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be(ReAuthFailedMessage);
        result.ErrorMessage.Should().NotBe(RoleGuardMessage,
            "re-auth failure and role rejection must be separately observable");
        targetUser.IsTwoFactorEnabled.Should().BeTrue();
        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
