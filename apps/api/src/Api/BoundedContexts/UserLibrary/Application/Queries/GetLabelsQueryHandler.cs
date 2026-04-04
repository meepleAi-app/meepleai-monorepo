using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.Labels;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries.Labels;

/// <summary>
/// Handler for getting all labels available to a user.
/// </summary>
internal class GetLabelsQueryHandler : IQueryHandler<GetLabelsQuery, IReadOnlyList<LabelDto>>
{
    private readonly IGameLabelRepository _labelRepository;

    public GetLabelsQueryHandler(IGameLabelRepository labelRepository)
    {
        _labelRepository = labelRepository ?? throw new ArgumentNullException(nameof(labelRepository));
    }

    public async Task<IReadOnlyList<LabelDto>> Handle(GetLabelsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var labels = await _labelRepository.GetAvailableLabelsAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return labels.Select(l => new LabelDto(
            Id: l.Id,
            Name: l.Name,
            Color: l.Color,
            IsPredefined: l.IsPredefined,
            CreatedAt: l.CreatedAt
        )).ToList();
    }
}
