using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

internal sealed class GetGameMechanicsQueryHandler : IRequestHandler<GetGameMechanicsQuery, List<GameMechanicDto>>
{
    private readonly MeepleAiDbContext _context;

    public GetGameMechanicsQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<GameMechanicDto>> Handle(GetGameMechanicsQuery query, CancellationToken cancellationToken)
    {
        var mechanics = await _context.GameMechanics
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new GameMechanicDto(m.Id, m.Name, m.Slug))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return mechanics;
    }
}
