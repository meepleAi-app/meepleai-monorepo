using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Issue #2055: Handler for getting editor lock status.
/// </summary>
public class GetEditorLockStatusQueryHandler : IQueryHandler<GetEditorLockStatusQuery, EditorLockDto>
{
    private readonly IEditorLockService _lockService;

    public GetEditorLockStatusQueryHandler(IEditorLockService lockService)
    {
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
    }

    public Task<EditorLockDto> Handle(GetEditorLockStatusQuery query, CancellationToken cancellationToken)
    {
        return _lockService.GetLockStatusAsync(
            query.GameId,
            query.CurrentUserId,
            cancellationToken);
    }
}
