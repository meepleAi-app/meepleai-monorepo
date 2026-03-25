using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Query to retrieve monthly/daily active user counts and retention rate.
/// Uses UserSession activity (LastSeenAt) as the activity signal.
/// </summary>
internal record GetMauQuery : IQuery<MauDto>;
