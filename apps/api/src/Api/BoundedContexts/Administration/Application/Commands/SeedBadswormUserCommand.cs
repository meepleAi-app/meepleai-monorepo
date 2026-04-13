using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to seed the badsworm demo user for development purposes.
/// Creates badsworm@alice.it with password from SEED_BADSWORM_PASSWORD secret.
/// </summary>
public sealed record SeedBadswormUserCommand : ICommand;
