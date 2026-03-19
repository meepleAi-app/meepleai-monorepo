using Api.BoundedContexts.KnowledgeBase.Application.Commands.AdminStrategy;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AdminStrategy;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AdminStrategy;

/// <summary>
/// Handlers for Admin Strategy CRUD operations.
/// Issue #5314.
/// </summary>
internal sealed class ListAdminStrategiesHandler : IRequestHandler<ListAdminStrategiesQuery, IReadOnlyList<AdminStrategyResult>>
{
    private readonly IAdminRagStrategyRepository _repository;

    public ListAdminStrategiesHandler(IAdminRagStrategyRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AdminStrategyResult>> Handle(ListAdminStrategiesQuery request, CancellationToken cancellationToken)
    {
        var strategies = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return strategies.Select(s => new AdminStrategyResult(
            s.Id, s.Name, s.Description, s.StepsJson, s.CreatedAt, s.UpdatedAt)).ToList();
    }
}

internal sealed class GetAdminStrategyByIdHandler : IRequestHandler<GetAdminStrategyByIdQuery, AdminStrategyResult?>
{
    private readonly IAdminRagStrategyRepository _repository;

    public GetAdminStrategyByIdHandler(IAdminRagStrategyRepository repository)
    {
        _repository = repository;
    }

    public async Task<AdminStrategyResult?> Handle(GetAdminStrategyByIdQuery request, CancellationToken cancellationToken)
    {
        var strategy = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (strategy == null) return null;

        return new AdminStrategyResult(
            strategy.Id, strategy.Name, strategy.Description, strategy.StepsJson, strategy.CreatedAt, strategy.UpdatedAt);
    }
}

internal sealed class CreateAdminStrategyHandler : IRequestHandler<CreateAdminStrategyCommand, AdminStrategyResult>
{
    private readonly IAdminRagStrategyRepository _repository;
    private readonly ILogger<CreateAdminStrategyHandler> _logger;

    public CreateAdminStrategyHandler(IAdminRagStrategyRepository repository, ILogger<CreateAdminStrategyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AdminStrategyResult> Handle(CreateAdminStrategyCommand request, CancellationToken cancellationToken)
    {
        var strategy = AdminRagStrategy.Create(
            request.Name,
            request.Description,
            request.StepsJson,
            request.AdminUserId);

        await _repository.AddAsync(strategy, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Admin {AdminId} created strategy {StrategyId}: {Name}",
            request.AdminUserId, strategy.Id, strategy.Name);

        return new AdminStrategyResult(
            strategy.Id, strategy.Name, strategy.Description, strategy.StepsJson, strategy.CreatedAt, strategy.UpdatedAt);
    }
}

internal sealed class UpdateAdminStrategyHandler : IRequestHandler<UpdateAdminStrategyCommand, AdminStrategyResult>
{
    private readonly IAdminRagStrategyRepository _repository;
    private readonly ILogger<UpdateAdminStrategyHandler> _logger;

    public UpdateAdminStrategyHandler(IAdminRagStrategyRepository repository, ILogger<UpdateAdminStrategyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AdminStrategyResult> Handle(UpdateAdminStrategyCommand request, CancellationToken cancellationToken)
    {
        var strategy = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Strategy {request.Id} not found");

        strategy.Update(request.Name, request.Description, request.StepsJson);
        await _repository.UpdateAsync(strategy, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Admin {AdminId} updated strategy {StrategyId}: {Name}",
            request.AdminUserId, strategy.Id, strategy.Name);

        return new AdminStrategyResult(
            strategy.Id, strategy.Name, strategy.Description, strategy.StepsJson, strategy.CreatedAt, strategy.UpdatedAt);
    }
}

internal sealed class DeleteAdminStrategyHandler : IRequestHandler<DeleteAdminStrategyCommand, bool>
{
    private readonly IAdminRagStrategyRepository _repository;
    private readonly ILogger<DeleteAdminStrategyHandler> _logger;

    public DeleteAdminStrategyHandler(IAdminRagStrategyRepository repository, ILogger<DeleteAdminStrategyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteAdminStrategyCommand request, CancellationToken cancellationToken)
    {
        var strategy = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (strategy == null) return false;

        strategy.SoftDelete();
        await _repository.UpdateAsync(strategy, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Admin {AdminId} deleted strategy {StrategyId}",
            request.AdminUserId, strategy.Id);

        return true;
    }
}
