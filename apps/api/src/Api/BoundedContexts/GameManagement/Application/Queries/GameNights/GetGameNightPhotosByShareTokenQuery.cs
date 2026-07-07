using System.Collections.Generic;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Anonymous shared-token read of a GameNight's recap photo gallery (#2724).
/// The token is a cryptographic secret — possession authorises the read.
/// </summary>
internal record GetGameNightPhotosByShareTokenQuery(string ShareToken)
    : IQuery<IReadOnlyList<GameNightPhotoDto>>;
