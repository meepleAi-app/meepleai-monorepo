using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles GetGameFAQsQuery.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal class GetGameFAQsQueryHandler : IQueryHandler<GetGameFAQsQuery, GetGameFAQsQueryResult>
{
    private readonly IGameFAQRepository _faqRepository;

    public GetGameFAQsQueryHandler(IGameFAQRepository faqRepository)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
    }

    public async Task<GetGameFAQsQueryResult> Handle(GetGameFAQsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // Optimized single query (prevents N+1 problem)
        var (faqs, totalCount) = await _faqRepository.GetByGameIdWithCountAsync(
            query.GameId,
            query.Limit,
            query.Offset,
            cancellationToken).ConfigureAwait(false);

        var faqDtos = faqs.Select(MapToDto).ToList();

        return new GetGameFAQsQueryResult(faqDtos, totalCount);
    }

    private static GameFAQDto MapToDto(GameFAQ faq)
    {
        return new GameFAQDto(
            Id: faq.Id,
            GameId: faq.GameId,
            Question: faq.Question.Value,
            Answer: faq.Answer.Value,
            Upvotes: faq.Upvotes,
            CreatedAt: faq.CreatedAt,
            UpdatedAt: faq.UpdatedAt
        );
    }
}
