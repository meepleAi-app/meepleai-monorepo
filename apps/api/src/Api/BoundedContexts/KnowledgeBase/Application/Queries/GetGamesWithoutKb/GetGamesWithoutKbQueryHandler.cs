using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGamesWithoutKb;

/// <summary>
/// Returns SharedGames where HasKnowledgeBase = false (admin RAG onboarding flow).
/// Uses the denormalized flag for O(1) filtering without joining VectorDocuments.
/// </summary>
internal sealed class GetGamesWithoutKbQueryHandler
    : IQueryHandler<GetGamesWithoutKbQuery, GamesWithoutKbPagedResponse>
{
    private const int PublishedStatus = 1;
    private const string FailedState = "Failed";

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetGamesWithoutKbQueryHandler> _logger;

    public GetGamesWithoutKbQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetGamesWithoutKbQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GamesWithoutKbPagedResponse> Handle(
        GetGamesWithoutKbQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = _dbContext.SharedGames
            .AsNoTracking()
            .Where(sg => !sg.HasKnowledgeBase && sg.Status == PublishedStatus);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            baseQuery = baseQuery.Where(sg => EF.Functions.ILike(sg.Title, pattern));
        }

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var games = await baseQuery
            .OrderBy(sg => sg.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sg => new
            {
                sg.Id,
                sg.Title,
                sg.ImageUrl,
                sg.MinPlayers,
                sg.MaxPlayers,
                Publishers = sg.Publishers.Select(p => p.Name).ToList(),
                PdfCount = _dbContext.PdfDocuments
                    .Count(pd => pd.SharedGameId == sg.Id),
                HasFailedPdfs = _dbContext.PdfDocuments
                    .Any(pd => pd.SharedGameId == sg.Id && pd.ProcessingState == FailedState)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = games.Select(g => new GameWithoutKbDto(
            GameId: g.Id,
            Title: g.Title,
            Publisher: g.Publishers.FirstOrDefault(),
            ImageUrl: string.IsNullOrWhiteSpace(g.ImageUrl) ? null : g.ImageUrl,
            PlayerCountLabel: FormatPlayerCount(g.MinPlayers, g.MaxPlayers),
            PdfCount: g.PdfCount,
            HasFailedPdfs: g.HasFailedPdfs
        )).ToList();

        var totalPages = total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;

        _logger.LogDebug(
            "GetGamesWithoutKb: found {Total} games, page {Page}/{TotalPages}",
            total,
            page,
            totalPages);

        return new GamesWithoutKbPagedResponse(
            Items: items,
            Total: total,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages);
    }

    private static string FormatPlayerCount(int min, int max)
    {
        if (min <= 0 && max <= 0)
        {
            return "—";
        }

        return min == max
            ? $"{min} giocatori"
            : $"{min}–{max} giocatori";
    }
}
