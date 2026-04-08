using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

internal class AddUserDicePresetCommandHandler : ICommandHandler<AddUserDicePresetCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddUserDicePresetCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(AddUserDicePresetCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        toolkit.AddUserDicePreset(command.UserId, command.Name, command.Formula);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}

internal class RemoveUserDicePresetCommandHandler : ICommandHandler<RemoveUserDicePresetCommand, GameToolkitDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveUserDicePresetCommandHandler(IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameToolkitDto> Handle(RemoveUserDicePresetCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolkit = await _repository.GetByIdAsync(command.ToolkitId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        if (!toolkit.RemoveUserDicePreset(command.UserId, command.PresetName))
            throw new NotFoundException("UserDicePreset", command.PresetName);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolkitMapper.ToDto(toolkit);
    }
}
