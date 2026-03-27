using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to seed a staging demo user for external testing/demos.
/// Only executes when ASPNETCORE_ENVIRONMENT=Staging.
/// Email from STAGING_DEMO_EMAIL, password from STAGING_DEMO_PASSWORD secret.
/// </summary>
public sealed record SeedStagingDemoUserCommand : ICommand;
