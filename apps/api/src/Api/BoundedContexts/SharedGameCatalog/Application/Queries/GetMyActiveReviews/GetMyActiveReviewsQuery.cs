using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetMyActiveReviews;

/// <summary>
/// Query to retrieve all active reviews for a specific admin.
/// </summary>
internal sealed record GetMyActiveReviewsQuery(
    Guid AdminId) : IQuery<IReadOnlyCollection<ActiveReviewDto>>;
