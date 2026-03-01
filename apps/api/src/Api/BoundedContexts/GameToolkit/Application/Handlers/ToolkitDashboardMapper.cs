using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Entities;

namespace Api.BoundedContexts.GameToolkit.Application.Handlers;

internal static class ToolkitDashboardMapper
{
    internal static ToolkitDashboardDto ToDto(Toolkit toolkit)
    {
        return new ToolkitDashboardDto(
            toolkit.Id,
            toolkit.GameId,
            toolkit.OwnerUserId,
            toolkit.IsDefault,
            toolkit.DisplayName,
            toolkit.CreatedAt,
            toolkit.UpdatedAt,
            toolkit.Widgets
                .OrderBy(w => w.DisplayOrder)
                .Select(w => new ToolkitWidgetDto(w.Id, w.Type, w.IsEnabled, w.DisplayOrder, w.Config))
                .ToList()
                .AsReadOnly()
        );
    }
}
