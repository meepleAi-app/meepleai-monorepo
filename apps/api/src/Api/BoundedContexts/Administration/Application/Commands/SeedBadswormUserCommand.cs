using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to seed the badsworm demo user account.
/// Creates badsworm@alice.it with password from SEED_BADSWORM_PASSWORD secret.
/// Runs in all environments (Dev, Staging, Prod) when the secret is present.
/// </summary>
public sealed record SeedBadswormUserCommand : ICommand;
