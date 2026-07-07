using System;
using System.Collections.Generic;
using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Participant-scoped read of a GameNight's recap photo gallery (#2724).
/// </summary>
internal record GetGameNightPhotosQuery(Guid GameNightId, Guid CallerUserId)
    : IQuery<IReadOnlyList<GameNightPhotoDto>>;
