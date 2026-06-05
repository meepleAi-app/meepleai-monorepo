using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertChannels;

internal sealed class GetAllAlertChannelsQueryHandler
    : IRequestHandler<GetAllAlertChannelsQuery, IReadOnlyList<AlertChannelDto>>
{
    private readonly IAlertChannelRepository _repository;

    public GetAllAlertChannelsQueryHandler(IAlertChannelRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<IReadOnlyList<AlertChannelDto>> Handle(
        GetAllAlertChannelsQuery request,
        CancellationToken cancellationToken)
    {
        var channels = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return channels.Select(Map).ToList();
    }

    private static AlertChannelDto Map(AlertChannel c) => new(
        Type: c.Type.ToWireValue(),
        ConfigJson: c.ConfigJson,
        IsEnabled: c.IsEnabled,
        LastTestedAt: c.LastTestedAt,
        LastTestStatus: c.LastTestStatus,
        LastTestMessage: c.LastTestMessage,
        UpdatedAt: c.UpdatedAt,
        UpdatedBy: c.UpdatedBy,
        RowVersion: Convert.ToBase64String(c.RowVersion ?? Array.Empty<byte>()));
}
