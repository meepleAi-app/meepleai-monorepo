#pragma warning disable MA0048 // File name must match type name - Contains related query handlers
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Application.Queries;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.GameToolbox.Application.Handlers;

internal class GetToolboxQueryHandler : IRequestHandler<GetToolboxQuery, ToolboxDto?>
{
    private readonly IToolboxRepository _repository;

    public GetToolboxQueryHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ToolboxDto?> Handle(GetToolboxQuery request, CancellationToken cancellationToken)
    {
        var toolbox = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return toolbox is null ? null : ToolboxMapper.ToDto(toolbox);
    }
}

internal class GetToolboxByGameQueryHandler : IRequestHandler<GetToolboxByGameQuery, ToolboxDto?>
{
    private readonly IToolboxRepository _repository;

    public GetToolboxByGameQueryHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ToolboxDto?> Handle(GetToolboxByGameQuery request, CancellationToken cancellationToken)
    {
        var toolbox = await _repository.GetByGameIdAsync(request.GameId, cancellationToken).ConfigureAwait(false);
        return toolbox is null ? null : ToolboxMapper.ToDto(toolbox);
    }
}

internal class GetToolboxTemplatesQueryHandler : IRequestHandler<GetToolboxTemplatesQuery, List<ToolboxTemplateDto>>
{
    private readonly IToolboxTemplateRepository _repository;

    public GetToolboxTemplatesQueryHandler(IToolboxTemplateRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<ToolboxTemplateDto>> Handle(GetToolboxTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = request.GameId.HasValue
            ? await _repository.GetByGameIdAsync(request.GameId.Value, cancellationToken).ConfigureAwait(false)
            : await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return templates.Select(ToolboxMapper.ToDto).ToList();
    }
}

internal class GetAvailableToolsQueryHandler : IRequestHandler<GetAvailableToolsQuery, List<AvailableToolDto>>
{
    public Task<List<AvailableToolDto>> Handle(GetAvailableToolsQuery request, CancellationToken cancellationToken)
    {
        // Static list of available tool types
        var tools = new List<AvailableToolDto>
        {
            new("DiceRoller", "Dice Roller", "Adapted"),
            new("ScoreTracker", "Score Tracker", "Adapted"),
            new("TurnManager", "Turn Manager", "Adapted"),
            new("ResourceManager", "Resource Manager", "Adapted"),
            new("Notes", "Notes", "Adapted"),
            new("Whiteboard", "Whiteboard", "Adapted"),
            new("CardDeck", "Card Deck", "Adapted"),
        };

        return Task.FromResult(tools);
    }
}
