using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;

/// <summary>
/// Query to get user's private game proposals (NewGameProposal ShareRequests).
/// Issue #3665: Phase 4 - Proposal System.
/// </summary>
/// <param name="UserId">ID of the user requesting their proposals</param>
/// <param name="StatusFilter">Optional status filter (Pending, InReview, etc.)</param>
/// <param name="PageNumber">Page number for pagination (default 1)</param>
/// <param name="PageSize">Page size for pagination (default 20)</param>
internal record GetMyProposalsQuery(
    Guid UserId,
    ShareRequestStatus? StatusFilter = null,
    int PageNumber = 1,
    int PageSize = 20
) : IQuery<PagedResult<UserShareRequestDto>>;
