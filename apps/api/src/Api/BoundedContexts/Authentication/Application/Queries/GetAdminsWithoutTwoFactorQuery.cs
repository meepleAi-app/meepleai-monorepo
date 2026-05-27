using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query for the strict-2FA cutover compliance sweep (SP5 Admin Security S3 — T6, D-S3-5).
/// Returns every privileged account (Admin or SuperAdmin) that has NOT enrolled in 2FA, so ops can
/// drive enrollment before flipping <c>TwoFactorStrictMode</c> in an environment.
/// </summary>
internal sealed record GetAdminsWithoutTwoFactorQuery() : IQuery<IReadOnlyList<UserDto>>;
