using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Public lookup of a live session by join code, returning a narrow read-only lobby projection.
/// Code-as-capability: any valid code resolves; a null result maps to 404 at the endpoint. Issue #2590.
/// </summary>
internal record GetPublicLiveSessionByCodeQuery(string SessionCode) : IQuery<PublicLiveSessionDto?>;
