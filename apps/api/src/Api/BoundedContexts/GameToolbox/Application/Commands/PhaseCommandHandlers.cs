#pragma warning disable MA0048 // File name must match type name - Contains related handlers
using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameToolbox.Application.Commands;

internal class AddPhaseCommandHandler : ICommandHandler<AddPhaseCommand, PhaseDto>
{
    private readonly IToolboxRepository _repository;

    public AddPhaseCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PhaseDto> Handle(AddPhaseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _repository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        var phase = toolbox.AddPhase(command.Name, command.ActiveToolIds);

        await _repository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolboxMapper.ToDto(phase);
    }
}

internal class RemovePhaseCommandHandler : ICommandHandler<RemovePhaseCommand, Unit>
{
    private readonly IToolboxRepository _repository;

    public RemovePhaseCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Unit> Handle(RemovePhaseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _repository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        toolbox.RemovePhase(command.PhaseId);

        await _repository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}

internal class ReorderPhasesCommandHandler : ICommandHandler<ReorderPhasesCommand, Unit>
{
    private readonly IToolboxRepository _repository;

    public ReorderPhasesCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Unit> Handle(ReorderPhasesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _repository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        toolbox.ReorderPhases(command.OrderedPhaseIds);

        await _repository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}

internal class AdvancePhaseCommandHandler : ICommandHandler<AdvancePhaseCommand, PhaseDto>
{
    private readonly IToolboxRepository _repository;

    public AdvancePhaseCommandHandler(IToolboxRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PhaseDto> Handle(AdvancePhaseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _repository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        var nextPhase = toolbox.AdvancePhase();

        await _repository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolboxMapper.ToDto(nextPhase);
    }
}
