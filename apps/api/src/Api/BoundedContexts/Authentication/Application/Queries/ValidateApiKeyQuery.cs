using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to validate an API key and retrieve associated user information.
/// </summary>
public record ValidateApiKeyQuery(
    string PlaintextKey
) : IQuery<UserDto?>;
