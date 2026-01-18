using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to seed a demo test user for development/testing purposes.
/// Creates Test@meepleai.com / Demo123! with User role.
/// </summary>
public sealed record SeedTestUserCommand : ICommand;
