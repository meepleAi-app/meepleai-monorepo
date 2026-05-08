using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to seed the project owner / Nanolith demo account.
/// Creates badsworm@gmail.com (role=SuperAdmin) with password from
/// SEED_BADSWORM_PASSWORD secret. Runs in all environments (Dev, Staging, Prod)
/// when the secret is present.
///
/// Aligned with the staging allowlist (DevOps wave 1) — this is the email
/// pre-populated in STAGING_ALLOWED_EMAILS on the staging VPS.
/// </summary>
public sealed record SeedBadswormUserCommand : ICommand;
