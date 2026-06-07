using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

// ── Results ─────────────────────────────────────────────────────────

public sealed record TriggerCatalogSyncResult(Guid RunId, string Status);

// ── Command ─────────────────────────────────────────────────────────

/// <summary>
/// Trigger a manual catalog sync run for the given provider (#1861).
/// </summary>
/// <remarks>
/// 409 Conflict if a run is already in <see cref="CatalogSyncStatus.Running"/> state.
/// </remarks>
public sealed record TriggerCatalogSyncCommand(
    CatalogSyncProvider Provider,
    Guid TriggeredByUserId) : ICommand<TriggerCatalogSyncResult>;

// ── Validator ───────────────────────────────────────────────────────

public sealed class TriggerCatalogSyncCommandValidator : AbstractValidator<TriggerCatalogSyncCommand>
{
    public TriggerCatalogSyncCommandValidator()
    {
        RuleFor(x => x.Provider)
            .IsInEnum()
            .WithMessage("Provider must be a valid CatalogSyncProvider value.");

        RuleFor(x => x.TriggeredByUserId)
            .NotEmpty()
            .WithMessage("TriggeredByUserId is required.");
    }
}

// ── Handler ─────────────────────────────────────────────────────────

internal sealed class TriggerCatalogSyncCommandHandler
    : ICommandHandler<TriggerCatalogSyncCommand, TriggerCatalogSyncResult>
{
    private readonly ICatalogSyncRunRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TriggerCatalogSyncCommandHandler> _logger;

    public TriggerCatalogSyncCommandHandler(
        ICatalogSyncRunRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<TriggerCatalogSyncCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TriggerCatalogSyncResult> Handle(
        TriggerCatalogSyncCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await _repository
            .GetCurrentRunningAsync(cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            throw new ConflictException(
                $"A catalog sync run is already in progress (runId={existing.Id}).");
        }

        var title = BuildTitle(command.Provider);
        var run = CatalogSyncRun.Enqueue(command.Provider, title, command.TriggeredByUserId);

        await _repository.AddAsync(run, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Catalog sync run {RunId} enqueued by user {UserId} (provider={Provider})",
            run.Id, command.TriggeredByUserId, command.Provider);

        return new TriggerCatalogSyncResult(run.Id, "queued");
    }

    private static string BuildTitle(CatalogSyncProvider provider) => provider switch
    {
        CatalogSyncProvider.BggApi => "BGG full sync",
        CatalogSyncProvider.CsvImport => "CSV bulk import",
        CatalogSyncProvider.Manual => "Manual catalog entry",
        _ => $"Catalog sync ({provider})",
    };
}
