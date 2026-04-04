using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IRuleDisputeRepository.
/// Maps between domain RuleDispute aggregate and RuleDisputeEntity persistence model.
/// Serializes/deserializes JSONB fields for verdict, votes, and related dispute IDs.
/// </summary>
internal sealed class RuleDisputeRepository : RepositoryBase, IRuleDisputeRepository
{
    public RuleDisputeRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<RuleDispute?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.RuleDisputes
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<RuleDispute>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var entities = await DbContext.RuleDisputes
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<RuleDispute>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var entities = await DbContext.RuleDisputes
            .AsNoTracking()
            .Where(e => e.GameId == gameId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(RuleDispute dispute, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dispute);
        CollectDomainEvents(dispute);

        var entity = MapToPersistence(dispute);
        await DbContext.RuleDisputes.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(RuleDispute dispute, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dispute);
        CollectDomainEvents(dispute);

        var entity = MapToPersistence(dispute);
        DbContext.RuleDisputes.Update(entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps persistence entity to domain aggregate.
    /// </summary>
    private static RuleDispute MapToDomain(RuleDisputeEntity entity)
    {
        // Use factory method to create the aggregate
        var dispute = RuleDispute.Open(
            entity.SessionId,
            entity.GameId,
            entity.InitiatorPlayerId,
            entity.InitiatorClaim);

        // Restore Id via reflection (factory generates a new one)
        SetPrivateProperty(dispute, nameof(RuleDispute.Id), entity.Id);
        SetPrivateProperty(dispute, nameof(RuleDispute.CreatedAt), entity.CreatedAt);

        // Restore respondent
        if (entity.RespondentPlayerId.HasValue && entity.RespondentClaim != null)
        {
            dispute.AddRespondentClaim(entity.RespondentPlayerId.Value, entity.RespondentClaim);
        }

        // Restore verdict from JSONB
        if (entity.VerdictJson != null)
        {
            var verdictDto = JsonSerializer.Deserialize<DisputeVerdictDto>(entity.VerdictJson);
            if (verdictDto != null)
            {
                var verdict = new DisputeVerdict(
                    (RulingFor)verdictDto.RulingFor,
                    verdictDto.Reasoning,
                    verdictDto.Citation,
                    (VerdictConfidence)verdictDto.Confidence);
                dispute.SetVerdict(verdict);
            }
        }

        // Restore votes from JSONB
        if (entity.VotesJson != null)
        {
            var votes = JsonSerializer.Deserialize<List<DisputeVoteDto>>(entity.VotesJson);
            if (votes != null)
            {
                foreach (var vote in votes)
                {
                    dispute.CastVote(vote.PlayerId, vote.AcceptsVerdict);
                }
            }
        }

        // Restore final outcome via reflection (set after votes so CastVote doesn't interfere)
        SetPrivateProperty(dispute, nameof(RuleDispute.FinalOutcome), (DisputeOutcome)entity.FinalOutcome);

        // Restore override rule
        if (entity.OverrideRule != null && (DisputeOutcome)entity.FinalOutcome == DisputeOutcome.VerdictOverridden)
        {
            dispute.SetOverrideRule(entity.OverrideRule);
        }

        // Restore related dispute IDs from JSONB
        if (entity.RelatedDisputeIdsJson != null)
        {
            var relatedIds = JsonSerializer.Deserialize<List<Guid>>(entity.RelatedDisputeIdsJson);
            if (relatedIds != null)
            {
                dispute.SetRelatedDisputeIds(relatedIds);
            }
        }

        return dispute;
    }

    /// <summary>
    /// Maps domain aggregate to persistence entity.
    /// </summary>
    private static RuleDisputeEntity MapToPersistence(RuleDispute dispute)
    {
        var entity = new RuleDisputeEntity
        {
            Id = dispute.Id,
            SessionId = dispute.SessionId,
            GameId = dispute.GameId,
            InitiatorPlayerId = dispute.InitiatorPlayerId,
            RespondentPlayerId = dispute.RespondentPlayerId,
            InitiatorClaim = dispute.InitiatorClaim,
            RespondentClaim = dispute.RespondentClaim,
            FinalOutcome = (int)dispute.FinalOutcome,
            OverrideRule = dispute.OverrideRule,
            CreatedAt = dispute.CreatedAt,
        };

        // Serialize verdict to JSONB
        if (dispute.Verdict != null)
        {
            var verdictDto = new DisputeVerdictDto
            {
                RulingFor = (int)dispute.Verdict.RulingFor,
                Reasoning = dispute.Verdict.Reasoning,
                Citation = dispute.Verdict.Citation,
                Confidence = (int)dispute.Verdict.Confidence,
            };
            entity.VerdictJson = JsonSerializer.Serialize(verdictDto);
        }

        // Serialize votes to JSONB
        if (dispute.Votes.Count > 0)
        {
            var voteDtos = dispute.Votes.Select(v => new DisputeVoteDto
            {
                PlayerId = v.PlayerId,
                AcceptsVerdict = v.AcceptsVerdict,
            }).ToList();
            entity.VotesJson = JsonSerializer.Serialize(voteDtos);
        }

        // Serialize related dispute IDs to JSONB
        if (dispute.RelatedDisputeIds.Count > 0)
        {
            entity.RelatedDisputeIdsJson = JsonSerializer.Serialize(dispute.RelatedDisputeIds.ToList());
        }

        return entity;
    }

    private static void SetPrivateProperty(object obj, string propertyName, object? value)
    {
        var property = obj.GetType().GetProperty(propertyName);
        if (property != null && property.CanWrite)
        {
            property.SetValue(obj, value);
        }
    }

    /// <summary>DTO for JSON serialization of DisputeVerdict.</summary>
    private sealed class DisputeVerdictDto
    {
        public int RulingFor { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public string? Citation { get; set; }
        public int Confidence { get; set; }
    }

    /// <summary>DTO for JSON serialization of DisputeVote.</summary>
    private sealed class DisputeVoteDto
    {
        public Guid PlayerId { get; set; }
        public bool AcceptsVerdict { get; set; }
    }
}
