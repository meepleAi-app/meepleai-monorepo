using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Guards;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.BoundedContexts.SecurityAudit.Application.Services;
using System.Text.Json;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal class ChangeUserRoleCommandHandler : ICommandHandler<ChangeUserRoleCommand, UserDto>
{
    private static readonly string[] AllowedRoles = { "Admin", "Editor", "Creator", "User" };

    // ADM-002: Role hierarchy — higher level = more privileged
    private static readonly Dictionary<string, int> RoleLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        { "user", 0 }, { "creator", 1 }, { "editor", 2 }, { "admin", 3 }, { "superadmin", 4 }
    };

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public ChangeUserRoleCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<UserDto> Handle(ChangeUserRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate input before domain operations
        Guard.AgainstNullOrWhiteSpace(command.UserId, nameof(command.UserId));
        if (!Guid.TryParse(command.UserId, out var userId))
            throw new ValidationException($"Invalid UserId format: {command.UserId}");
        Guard.AgainstNullOrWhiteSpace(command.NewRole, nameof(command.NewRole));
        Guard.AgainstInvalidValue(command.NewRole, AllowedRoles, nameof(command.NewRole));

        // ADM-002: Privilege escalation check — cannot assign role >= caller's own level
        var adminLevel = RoleLevels.GetValueOrDefault(command.AdminRole, 0);
        var targetLevel = RoleLevels.GetValueOrDefault(command.NewRole, 0);

        if (targetLevel >= adminLevel)
            throw new ForbiddenException(
                $"Cannot assign role '{command.NewRole}': you can only assign roles below your own privilege level");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new DomainException($"User {command.UserId} not found");

        // ADM-002: Minimum SuperAdmin guard — prevent demoting the last SuperAdmin
        if (user.Role.Value.Equals("superadmin", StringComparison.OrdinalIgnoreCase) &&
            !command.NewRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase))
        {
            var superAdminCount = await _userRepository.CountByRoleAsync(
                "superadmin", cancellationToken).ConfigureAwait(false);
            if (superAdminCount <= 1)
                throw new ForbiddenException(
                    "Cannot demote the last SuperAdmin. The system requires at least one SuperAdmin.");
        }

        var newRole = Role.Parse(command.NewRole);
        var oldRole = user.Role.Value;
        user.UpdateRole(newRole);

        // Persist updates - required because repository uses AsNoTracking
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #3838
        await _auditLogger.LogAsync(
            AuditEventType.RoleChanged,
            actorUserId: command.AdminId,
            targetUserId: user.Id,
            metadata: JsonSerializer.Serialize(new { oldRole, newRole = user.Role.Value, reason = command.Reason }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new UserDto(
            Id: user.Id.ToString(),
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            LastSeenAt: null
        );
    }
}
