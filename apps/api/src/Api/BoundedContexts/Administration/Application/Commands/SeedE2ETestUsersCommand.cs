using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to seed E2E test users for Playwright tests.
/// Creates admin, editor, and user accounts with predictable credentials.
///
/// Users created:
/// - Admin: email from INITIAL_ADMIN_EMAIL secret, password from ADMIN_PASSWORD secret (Admin role)
/// - editor@meepleai.dev / Demo123! (Editor role)
/// - user@meepleai.dev / Demo123! (User role)
/// </summary>
public sealed record SeedE2ETestUsersCommand : ICommand;
