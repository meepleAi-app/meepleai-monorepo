using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to seed a demo test user for development/testing purposes.
/// Creates Test@meepleai.com with password from SEED_TEST_PASSWORD secret.
/// </summary>
public sealed record SeedTestUserCommand : ICommand;
