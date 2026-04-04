using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Queries;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

// ============================================================================
// Query handler
// ============================================================================

/// <summary>
/// Returns the active Toolkit for a game/user pair.
/// Issue #5147 — Epic B4.
/// </summary>
internal sealed class GetActiveToolkitQueryHandler
    : IRequestHandler<GetActiveToolkitQuery, ToolkitDashboardDto?>
{
    private readonly IToolkitRepository _repository;

    public GetActiveToolkitQueryHandler(IToolkitRepository repository)
    {
        _repository = repository;
    }

    public async Task<ToolkitDashboardDto?> Handle(
        GetActiveToolkitQuery request, CancellationToken cancellationToken)
    {
        var toolkit = await _repository
            .GetActiveAsync(request.GameId, request.UserId, cancellationToken)
            .ConfigureAwait(false);

        return toolkit is null ? null : ToolkitDashboardMapper.ToDto(toolkit);
    }
}

// ============================================================================
// Command handlers
// ============================================================================

/// <summary>
/// Clones the default Toolkit into a user-specific override.
/// Issue #5147 — Epic B4.
/// </summary>
internal sealed class OverrideToolkitCommandHandler
    : IRequestHandler<OverrideToolkitCommand, ToolkitDashboardDto>
{
    private readonly IToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public OverrideToolkitCommandHandler(IToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ToolkitDashboardDto> Handle(
        OverrideToolkitCommand request, CancellationToken cancellationToken)
    {
        // If the user already has an override, return it (idempotent)
        var existing = await _repository
            .GetActiveAsync(request.GameId, request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null && !existing.IsDefault)
        {
            if (request.DisplayName is not null)
                existing.Rename(request.DisplayName);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToolkitDashboardMapper.ToDto(existing);
        }

        // Get the default to clone from
        var defaultToolkit = await _repository
            .GetDefaultAsync(request.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Toolkit", $"default for game {request.GameId}");

        var userToolkit = defaultToolkit.Override(request.UserId);

        if (request.DisplayName is not null)
            userToolkit.Rename(request.DisplayName);

        await _repository.AddAsync(userToolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitDashboardMapper.ToDto(userToolkit);
    }
}

/// <summary>
/// Updates a widget's enabled/disabled state or config.
/// Auto-creates a user override if the active toolkit is the default.
/// Issue #5147 — Epic B4.
/// </summary>
internal sealed class UpdateWidgetCommandHandler
    : IRequestHandler<UpdateWidgetCommand, ToolkitDashboardDto>
{
    private readonly IToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateWidgetCommandHandler(IToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ToolkitDashboardDto> Handle(
        UpdateWidgetCommand request, CancellationToken cancellationToken)
    {
        var toolkit = await _repository
            .GetActiveAsync(request.GameId, request.UserId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Toolkit", $"default for game {request.GameId}");

        // Cannot mutate the default — auto-clone it first (BR-02)
        if (toolkit.IsDefault)
        {
            toolkit = toolkit.Override(request.UserId);
            await _repository.AddAsync(toolkit, cancellationToken).ConfigureAwait(false);
        }

        if (request.IsEnabled is true)
            toolkit.EnableWidget(request.WidgetType);
        else if (request.IsEnabled is false)
            toolkit.DisableWidget(request.WidgetType);

        if (request.ConfigJson is not null)
            toolkit.ConfigureWidget(request.WidgetType, request.ConfigJson);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitDashboardMapper.ToDto(toolkit);
    }
}
