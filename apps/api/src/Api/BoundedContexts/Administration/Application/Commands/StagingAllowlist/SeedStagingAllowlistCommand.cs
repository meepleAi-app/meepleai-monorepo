using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;

/// <summary>
/// Bootstraps the staging allowlist with <c>badsworm@gmail.com</c> and (legacy migration)
/// any emails in the deprecated <c>STAGING_ALLOWED_EMAILS</c> env var.
/// Idempotent: skips entries already present (no duplicate insert, no error).
/// Only runs in Staging environment.
/// </summary>
public sealed record SeedStagingAllowlistCommand : ICommand;
