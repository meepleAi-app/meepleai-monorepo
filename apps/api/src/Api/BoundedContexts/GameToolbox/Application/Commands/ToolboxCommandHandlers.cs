#pragma warning disable MA0048 // File name must match type name - Contains related handlers
using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameToolbox.Application.Commands;

// === Toolbox CRUD ===

internal class CreateToolboxCommandHandler : ICommandHandler<CreateToolboxCommand, ToolboxDto>
{
    private readonly IToolboxRepository _repository;

    public CreateToolboxCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ToolboxDto> Handle(CreateToolboxCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var mode = Enum.Parse<ToolboxMode>(command.Mode, ignoreCase: true);
        var toolbox = Toolbox.Create(command.Name, command.GameId, mode);

        await _repository.AddAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolboxMapper.ToDto(toolbox);
    }
}

internal class UpdateToolboxModeCommandHandler : ICommandHandler<UpdateToolboxModeCommand, ToolboxDto>
{
    private readonly IToolboxRepository _repository;

    public UpdateToolboxModeCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ToolboxDto> Handle(UpdateToolboxModeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _repository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        var mode = Enum.Parse<ToolboxMode>(command.Mode, ignoreCase: true);
        toolbox.UpdateMode(mode);

        await _repository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolboxMapper.ToDto(toolbox);
    }
}

// === Tool Management ===

internal class AddToolToToolboxCommandHandler : ICommandHandler<AddToolToToolboxCommand, ToolboxToolDto>
{
    private readonly IToolboxRepository _repository;

    public AddToolToToolboxCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ToolboxToolDto> Handle(AddToolToToolboxCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _repository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        var tool = toolbox.AddTool(command.Type, command.Config);

        await _repository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolboxMapper.ToDto(tool);
    }
}

internal class RemoveToolFromToolboxCommandHandler : ICommandHandler<RemoveToolFromToolboxCommand, Unit>
{
    private readonly IToolboxRepository _repository;

    public RemoveToolFromToolboxCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Unit> Handle(RemoveToolFromToolboxCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _repository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        toolbox.RemoveTool(command.ToolId);

        await _repository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}

internal class ReorderToolsCommandHandler : ICommandHandler<ReorderToolsCommand, Unit>
{
    private readonly IToolboxRepository _repository;

    public ReorderToolsCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Unit> Handle(ReorderToolsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _repository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        toolbox.ReorderTools(command.OrderedToolIds);

        await _repository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}

// === Shared Context ===

internal class UpdateSharedContextCommandHandler : ICommandHandler<UpdateSharedContextCommand, SharedContextDto>
{
    private readonly IToolboxRepository _repository;

    public UpdateSharedContextCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<SharedContextDto> Handle(UpdateSharedContextCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _repository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        var context = ToolboxMapper.ToDomain(
            command.Players, command.CurrentPlayerIndex,
            command.CurrentRound, command.CustomProperties);

        toolbox.UpdateSharedContext(context);

        await _repository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolboxMapper.ToDto(toolbox.SharedContext);
    }
}
