using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Queries;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Handlers;

internal class GetToolkitQueryHandler : IQueryHandler<GetToolkitQuery, GameToolkitDto?>
{
    private readonly IGameToolkitRepository _repository;

    public GetToolkitQueryHandler(IGameToolkitRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GameToolkitDto?> Handle(GetToolkitQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var toolkit = await _repository.GetByIdAsync(query.ToolkitId, cancellationToken).ConfigureAwait(false);
        return toolkit != null ? ToolkitMapper.ToDto(toolkit) : null;
    }
}

internal class GetToolkitsByGameQueryHandler : IQueryHandler<GetToolkitsByGameQuery, IReadOnlyList<GameToolkitDto>>
{
    private readonly IGameToolkitRepository _repository;

    public GetToolkitsByGameQueryHandler(IGameToolkitRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<GameToolkitDto>> Handle(GetToolkitsByGameQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var toolkits = await _repository.GetByGameIdAsync(query.GameId, cancellationToken).ConfigureAwait(false);
        return toolkits.Select(ToolkitMapper.ToDto).ToList();
    }
}

internal class GetPublishedToolkitsQueryHandler : IQueryHandler<GetPublishedToolkitsQuery, IReadOnlyList<GameToolkitDto>>
{
    private readonly IGameToolkitRepository _repository;

    public GetPublishedToolkitsQueryHandler(IGameToolkitRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<GameToolkitDto>> Handle(GetPublishedToolkitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var toolkits = await _repository.GetPublishedAsync(cancellationToken).ConfigureAwait(false);
        return toolkits.Select(ToolkitMapper.ToDto).ToList();
    }
}
