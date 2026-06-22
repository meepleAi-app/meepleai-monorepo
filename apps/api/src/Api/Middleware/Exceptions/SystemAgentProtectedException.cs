namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when an operation attempts to modify or delete a system-defined agent.
/// System agents (IsSystemDefined = true) are seeded by the platform and must not be altered by users.
/// Maps to HTTP 403 Forbidden with error code "SYSTEM_AGENT_PROTECTED".
///
/// Issue #904: SG3 — Agent CRUD lifecycle + soft-delete cascade.
/// Used by SoftDeleteUserAgentCommandHandler to guard system agents.
/// </summary>
public class SystemAgentProtectedException : HttpException
{
    /// <summary>
    /// Gets the ID of the protected system agent.
    /// </summary>
    public Guid AgentId { get; }

    [SetsRequiredMembers]
    public SystemAgentProtectedException(Guid agentId)
        : base(StatusCodes.Status403Forbidden, "SYSTEM_AGENT_PROTECTED",
            $"Agent '{agentId}' is system-defined and cannot be modified or deleted.")
    {
        AgentId = agentId;
    }

    [SetsRequiredMembers]
    public SystemAgentProtectedException(Guid agentId, string message)
        : base(StatusCodes.Status403Forbidden, "SYSTEM_AGENT_PROTECTED", message)
    {
        AgentId = agentId;
    }
}
