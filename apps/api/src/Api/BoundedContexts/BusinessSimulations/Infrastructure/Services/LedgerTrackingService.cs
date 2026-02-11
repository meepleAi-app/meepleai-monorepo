using System.Globalization;
using System.Text.Json;
using Api.BoundedContexts.BusinessSimulations.Application.Interfaces;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.BusinessSimulations.Infrastructure.Services;

/// <summary>
/// Service that creates LedgerEntry records from system events.
/// Issue #3721: Automatic Ledger Tracking (Epic #3688)
/// </summary>
internal sealed class LedgerTrackingService : ILedgerTrackingService
{
    private readonly ILedgerEntryRepository _repository;
    private readonly ILogger<LedgerTrackingService> _logger;

    public LedgerTrackingService(
        ILedgerEntryRepository repository,
        ILogger<LedgerTrackingService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task TrackTokenUsageAsync(
        Guid userId,
        string modelId,
        int tokensConsumed,
        decimal costUsd,
        string? endpoint = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("ModelId is required", nameof(modelId));

        if (tokensConsumed <= 0)
            throw new ArgumentException("Tokens consumed must be positive", nameof(tokensConsumed));

        if (costUsd < 0)
            throw new ArgumentException("Cost cannot be negative", nameof(costUsd));

        // Skip zero-cost entries (free models) to avoid ledger clutter
        if (costUsd == 0)
        {
            _logger.LogDebug(
                "Skipping zero-cost ledger entry for user {UserId}, model {ModelId}",
                userId, modelId);
            return;
        }

        var metadata = JsonSerializer.Serialize(new
        {
            userId,
            modelId,
            tokensConsumed,
            endpoint,
            trackedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        });

        var entry = LedgerEntry.CreateAutoEntry(
            date: DateTime.UtcNow,
            type: LedgerEntryType.Expense,
            category: LedgerCategory.TokenUsage,
            amount: costUsd,
            currency: "USD",
            description: $"Token usage: {tokensConsumed} tokens via {modelId}",
            metadata: metadata);

        await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Ledger entry created for token usage: User={UserId}, Model={ModelId}, Tokens={Tokens}, Cost=${Cost}",
            userId, modelId, tokensConsumed, costUsd);
    }

    public async Task TrackSubscriptionPaymentAsync(
        Guid userId,
        decimal amount,
        string currency,
        string subscriptionType,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required", nameof(currency));

        if (string.IsNullOrWhiteSpace(subscriptionType))
            throw new ArgumentException("Subscription type is required", nameof(subscriptionType));

        var entryMetadata = metadata ?? JsonSerializer.Serialize(new
        {
            userId,
            subscriptionType,
            trackedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        });

        var entry = LedgerEntry.CreateAutoEntry(
            date: DateTime.UtcNow,
            type: LedgerEntryType.Income,
            category: LedgerCategory.Subscription,
            amount: amount,
            currency: currency,
            description: $"Subscription payment: {subscriptionType}",
            metadata: entryMetadata);

        await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Ledger entry created for subscription: User={UserId}, Type={Type}, Amount={Amount} {Currency}",
            userId, subscriptionType, amount, currency);
    }

    public async Task TrackInfrastructureCostAsync(
        decimal amount,
        string description,
        string? metadata = null,
        DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));

        var entryDate = date ?? DateTime.UtcNow;

        var entryMetadata = metadata ?? JsonSerializer.Serialize(new
        {
            description,
            trackedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        });

        var entry = LedgerEntry.CreateAutoEntry(
            date: entryDate,
            type: LedgerEntryType.Expense,
            category: LedgerCategory.Infrastructure,
            amount: amount,
            currency: "USD",
            description: description,
            metadata: entryMetadata);

        await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Ledger entry created for infrastructure cost: Amount=${Amount}, Description={Description}",
            amount, description);
    }
}
