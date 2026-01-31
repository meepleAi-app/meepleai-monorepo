using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to seed the initial admin user from secrets configuration.
/// Executed automatically during application startup if no admin user exists.
/// </summary>
public sealed record SeedAdminUserCommand : ICommand;
