using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Returns all games that have at least one PDF document uploaded by the given user.
/// Joins Games with PdfDocuments to find games with knowledge base content.
/// </summary>
internal sealed class GetGamesWithKbQueryHandler : IQueryHandler<GetGamesWithKbQuery, List<GameWithKbDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetGamesWithKbQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<List<GameWithKbDto>> Handle(GetGamesWithKbQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var results = await _db.PdfDocuments
            .Where(pdf => pdf.GameId != null && pdf.UploadedByUserId == query.UserId)
            .GroupBy(pdf => new { pdf.GameId, GameName = _db.Games.Where(g => g.Id == pdf.GameId).Select(g => g.Name).FirstOrDefault() })
            .Select(g => new GameWithKbDto(
                g.Key.GameId!.Value,
                g.Key.GameName ?? "Unknown",
                g.Count(),
                g.OrderByDescending(pdf => pdf.UploadedAt).Select(pdf => pdf.ProcessingState).FirstOrDefault()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return results;
    }
}
