using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.BoundedContexts.Authentication.Application.Commands.Waitlist;

/// <summary>
/// Handles enrollment into the public Alpha waitlist.
/// Spec §3.5 / §4.4 (2026-04-27-v2-migration-wave-a-2-join.md).
/// </summary>
internal class JoinWaitlistCommandHandler : ICommandHandler<JoinWaitlistCommand, JoinWaitlistResult>
{
    /// <summary>Postgres unique_violation SQLSTATE.</summary>
    private const string UniqueViolationSqlState = "23505";

    private readonly IWaitlistEntryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JoinWaitlistCommandHandler> _logger;

    public JoinWaitlistCommandHandler(
        IWaitlistEntryRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<JoinWaitlistCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<JoinWaitlistResult> Handle(JoinWaitlistCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Optimistic pre-check: in steady state most submissions are unique, so we avoid
        // opening a transaction on the duplicate path (HTTP 409).
        var existing = await _repository.GetByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return new JoinWaitlistResult(
                IsAlreadyOnList: true,
                Position: existing.Position,
                EstimatedWeeks: ComputeEstimatedWeeks(existing.Position));
        }

        // Race-safe insert: the advisory lock + transaction serializes Position assignment.
        // The unique index on Email is the second line of defense if two requests slip
        // past the optimistic pre-check between the GetByEmailAsync and the insert.
        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _repository.AcquirePositionLockAsync(cancellationToken).ConfigureAwait(false);

            var maxPosition = await _repository.GetMaxPositionAsync(cancellationToken).ConfigureAwait(false);
            var newPosition = (maxPosition ?? 0) + 1;

            var entry = WaitlistEntry.Create(
                email: normalizedEmail,
                name: request.Name,
                gamePreferenceId: request.GamePreferenceId,
                gamePreferenceOther: request.GamePreferenceOther,
                newsletterOptIn: request.NewsletterOptIn,
                position: newPosition);

            await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            return new JoinWaitlistResult(
                IsAlreadyOnList: false,
                Position: newPosition,
                EstimatedWeeks: ComputeEstimatedWeeks(newPosition));
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent request inserted the same email between our pre-check and our insert.
            // Roll back, re-query, and return the existing entry's position.
            await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);

            var raceWinner = await _repository.GetByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
            if (raceWinner is null)
            {
                // Should not happen — the unique violation means the row exists.
                _logger.LogError(
                    ex,
                    "Waitlist unique-violation race: post-rollback re-query found no row for {Email}",
                    normalizedEmail);
                throw;
            }

            _logger.LogInformation(
                "Waitlist concurrent-duplicate race resolved for {Email} → existing position {Position}",
                normalizedEmail,
                raceWinner.Position);

            return new JoinWaitlistResult(
                IsAlreadyOnList: true,
                Position: raceWinner.Position,
                EstimatedWeeks: ComputeEstimatedWeeks(raceWinner.Position));
        }
    }

    /// <summary>
    /// Coarse ETA shown to the user: <c>Ceiling(position / 100)</c>.
    /// Position 1–100 → 1 week; 101–200 → 2 weeks; etc.
    /// </summary>
    private static int ComputeEstimatedWeeks(int position) =>
        (int)Math.Ceiling(position / 100.0);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pgEx &&
        string.Equals(pgEx.SqlState, UniqueViolationSqlState, StringComparison.Ordinal);
}
