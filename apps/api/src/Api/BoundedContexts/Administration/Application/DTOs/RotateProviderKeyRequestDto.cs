namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Request payload for <c>POST /api/v1/admin/providers/{name}/rotate-key</c>.
/// <c>ConfirmedProviderName</c> is a typo guard: the FE form requires the operator to type the
/// provider name a second time and the validator enforces equality with the route parameter.
/// Issue #1859.
/// </summary>
public sealed record RotateProviderKeyRequestDto(
    string NewApiKey,
    string ConfirmedProviderName);
