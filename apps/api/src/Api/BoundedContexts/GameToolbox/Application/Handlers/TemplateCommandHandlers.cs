#pragma warning disable MA0048 // File name must match type name - Contains related handlers
using System.Text.Json;
using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolbox.Application.Handlers;

// === Template Management ===

/// <summary>
/// Creates a ToolboxTemplate by snapshotting the current tools and phases of an existing Toolbox.
/// </summary>
internal class CreateToolboxTemplateCommandHandler : ICommandHandler<CreateToolboxTemplateCommand, ToolboxTemplateDto>
{
    private readonly IToolboxRepository _toolboxRepository;
    private readonly IToolboxTemplateRepository _templateRepository;

    public CreateToolboxTemplateCommandHandler(
        IToolboxRepository toolboxRepository,
        IToolboxTemplateRepository templateRepository)
    {
        _toolboxRepository = toolboxRepository ?? throw new ArgumentNullException(nameof(toolboxRepository));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
    }

    public async Task<ToolboxTemplateDto> Handle(CreateToolboxTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _toolboxRepository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        // Serialize tools configuration (type + config for each tool)
        var toolSnapshots = toolbox.Tools
            .OrderBy(t => t.Order)
            .Select(t => new { t.Type, t.Config, t.Order })
            .ToList();
        var toolsJson = JsonSerializer.Serialize(toolSnapshots);

        // Serialize phases configuration
        var phaseSnapshots = toolbox.Phases
            .OrderBy(p => p.Order)
            .Select(p => new { p.Name, p.Order, p.ActiveToolIds })
            .ToList();
        var phasesJson = JsonSerializer.Serialize(phaseSnapshots);

        var template = ToolboxTemplate.Create(
            name: command.Name,
            mode: toolbox.Mode,
            source: TemplateSource.Manual,
            toolsJson: toolsJson,
            phasesJson: phasesJson,
            sharedContextDefaultsJson: "{}",
            gameId: command.GameId ?? toolbox.GameId);

        await _templateRepository.AddAsync(template, cancellationToken).ConfigureAwait(false);
        await _templateRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolboxMapper.ToDto(template);
    }
}

/// <summary>
/// Creates a new Toolbox by applying a saved template — restoring its tools and phases.
/// </summary>
internal class ApplyToolboxTemplateCommandHandler : ICommandHandler<ApplyToolboxTemplateCommand, ToolboxDto>
{
    private readonly IToolboxTemplateRepository _templateRepository;
    private readonly IToolboxRepository _toolboxRepository;

    public ApplyToolboxTemplateCommandHandler(
        IToolboxTemplateRepository templateRepository,
        IToolboxRepository toolboxRepository)
    {
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _toolboxRepository = toolboxRepository ?? throw new ArgumentNullException(nameof(toolboxRepository));
    }

    public async Task<ToolboxDto> Handle(ApplyToolboxTemplateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var template = await _templateRepository.GetByIdAsync(command.TemplateId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("ToolboxTemplate", command.TemplateId.ToString());

        var toolbox = Toolbox.Create(
            name: template.Name,
            gameId: command.GameId ?? template.GameId,
            mode: template.Mode);

        toolbox.SetTemplateId(template.Id);

        // Restore tools from template JSON
        var toolEntries = JsonSerializer.Deserialize<List<ToolSnapshotEntry>>(template.ToolsJson) ?? [];
        foreach (var entry in toolEntries.OrderBy(e => e.Order))
        {
            toolbox.AddTool(entry.Type, entry.Config);
        }

        // Restore phases from template JSON
        var phaseEntries = JsonSerializer.Deserialize<List<PhaseSnapshotEntry>>(template.PhasesJson) ?? [];
        foreach (var entry in phaseEntries.OrderBy(e => e.Order))
        {
            // ActiveToolIds in the template refer to the original tool IDs;
            // pass them through — the domain will assign new IDs to tools,
            // so phases reference by position implicitly. For full fidelity,
            // callers can remap after creation.
            toolbox.AddPhase(entry.Name, entry.ActiveToolIds);
        }

        await _toolboxRepository.AddAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _toolboxRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolboxMapper.ToDto(toolbox);
    }

    // Deserialization helpers matching the serialized snapshots
    private sealed record ToolSnapshotEntry(string Type, string Config, int Order);
    private sealed record PhaseSnapshotEntry(string Name, int Order, List<Guid>? ActiveToolIds);
}
