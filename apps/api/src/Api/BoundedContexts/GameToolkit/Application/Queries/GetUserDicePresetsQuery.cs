using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Queries;

internal record GetUserDicePresetsQuery(
    Guid ToolkitId,
    Guid UserId
) : IQuery<IReadOnlyList<UserDicePresetDto>>;

internal class GetUserDicePresetsQueryHandler : IQueryHandler<GetUserDicePresetsQuery, IReadOnlyList<UserDicePresetDto>>
{
    private readonly IGameToolkitRepository _repository;

    public GetUserDicePresetsQueryHandler(IGameToolkitRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<UserDicePresetDto>> Handle(GetUserDicePresetsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var toolkit = await _repository.GetByIdAsync(query.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", query.ToolkitId.ToString());

        var presets = toolkit.GetUserDicePresets(query.UserId);

        return presets.Select(p => new UserDicePresetDto(p.UserId, p.Name, p.Formula, p.CreatedAt)).ToList();
    }
}
