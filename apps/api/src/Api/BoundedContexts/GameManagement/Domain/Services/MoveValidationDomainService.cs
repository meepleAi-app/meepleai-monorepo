using System.Globalization;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Domain.Services;

/// <summary>
/// Domain service for validating player moves against game rules (RuleSpec).
/// Part of the GameManagement bounded context, integrates with RuleSpec v2.
/// </summary>
public class MoveValidationDomainService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<MoveValidationDomainService> _logger;

    // Minimum confidence threshold for validation
    private const double MinimumConfidenceThreshold = 0.70;

    public MoveValidationDomainService(
        MeepleAiDbContext dbContext,
        ILogger<MoveValidationDomainService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates a player move against the game's rule specification.
    /// </summary>
    /// <param name="session">The active game session.</param>
    /// <param name="move">The move to validate.</param>
    /// <param name="ruleSpecVersion">Optional specific version of rules to use. Uses latest if null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with applicable rules and errors.</returns>
    public async Task<MoveValidationResult> ValidateMoveAsync(
        GameSession session,
        Move move,
        string? ruleSpecVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        if (move == null)
            throw new ArgumentNullException(nameof(move));

        _logger.LogInformation(
            "Validating move for session {SessionId}, game {GameId}: {Move}",
            session.Id,
            session.GameId,
            move);

        // Check if session is in a valid state for move validation
        if (session.Status.IsFinished)
        {
            return MoveValidationResult.Invalid(
                new[] { $"Cannot validate move: session is {session.Status}" },
                Array.Empty<RuleAtom>(),
                confidenceScore: 1.0);
        }

        // Check if player is in the session
        if (!session.HasPlayer(move.PlayerName))
        {
            return MoveValidationResult.Invalid(
                new[] { $"Player '{move.PlayerName}' is not in this session" },
                Array.Empty<RuleAtom>(),
                confidenceScore: 1.0);
        }

        // Retrieve RuleSpec for the game
        var ruleSpec = await GetRuleSpecAsync(session.GameId, ruleSpecVersion, cancellationToken).ConfigureAwait(false);

        if (ruleSpec == null)
        {
            _logger.LogWarning(
                "No RuleSpec found for game {GameId}, version {Version}",
                session.GameId,
                ruleSpecVersion ?? "latest");

            return MoveValidationResult.Uncertain(
                "No rule specification available for this game",
                Array.Empty<RuleAtom>(),
                confidenceScore: 0.0);
        }

        // Find applicable rules for this move
        var applicableRules = FindApplicableRules(ruleSpec, move);

        _logger.LogDebug(
            "Found {RuleCount} applicable rules for move action '{Action}'",
            applicableRules.Count,
            move.Action);

        // Perform validation logic
        var validationResult = ValidateAgainstRules(move, applicableRules, session);

        return validationResult;
    }

    /// <summary>
    /// Retrieves the RuleSpec for a game, using specified version or latest.
    /// </summary>
    private async Task<RuleSpec?> GetRuleSpecAsync(
        Guid gameId,
        string? version,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.RuleSpecs
            .Where(r => r.GameId == gameId);

        if (!string.IsNullOrWhiteSpace(version))
        {
            query = query.Where(r => r.Version == version);
        }

        var ruleSpecDto = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.GameId,
                r.Version,
                r.CreatedAt,
                Rules = r.Atoms
                    .OrderBy(a => a.SortOrder)
                    .ThenBy(a => a.Id)
                    .Select(atom => new
                    {
                        atom.Id,
                        atom.Key,
                        atom.Text,
                        atom.Section,
                        atom.PageNumber,
                        atom.LineNumber
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ruleSpecDto == null)
        {
            return null;
        }

        var rules = ruleSpecDto.Rules
            .Select(atom =>
            {
                var ruleKey = string.IsNullOrWhiteSpace(atom.Key) ? atom.Id.ToString() : atom.Key!;
                var page = atom.PageNumber.HasValue
                    ? atom.PageNumber.Value.ToString(CultureInfo.InvariantCulture)
                    : null;
                var line = atom.LineNumber.HasValue
                    ? atom.LineNumber.Value.ToString(CultureInfo.InvariantCulture)
                    : null;

                return new RuleAtom(ruleKey, atom.Text, atom.Section, page, line);
            })
            .ToList();

        return new RuleSpec(
            ruleSpecDto.GameId.ToString(),
            ruleSpecDto.Version,
            ruleSpecDto.CreatedAt,
            rules);
    }

    /// <summary>
    /// Finds rules that are applicable to the given move.
    /// Uses keyword matching on action, position, and additional context.
    /// </summary>
    private List<RuleAtom> FindApplicableRules(RuleSpec ruleSpec, Move move)
    {
        var applicableRules = new List<RuleAtom>();
        var searchTerms = BuildSearchTerms(move);

        foreach (var rule in ruleSpec.rules)
        {
            var ruleTextLower = rule.text.ToLowerInvariant();

            // Check if any search term appears in the rule text
            if (searchTerms.Any(term => ruleTextLower.Contains(term)))
            {
                applicableRules.Add(rule);
            }
        }

        return applicableRules;
    }

    /// <summary>
    /// Builds search terms from the move for rule matching.
    /// </summary>
    private List<string> BuildSearchTerms(Move move)
    {
        var terms = new List<string>();

        // Add action terms
        var actionWords = move.Action.ToLowerInvariant()
            .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        terms.AddRange(actionWords);

        // Add position if provided
        if (!string.IsNullOrWhiteSpace(move.Position))
        {
            terms.Add(move.Position.ToLowerInvariant());
        }

        // Add additional context terms
        if (move.AdditionalContext != null)
        {
            foreach (var kvp in move.AdditionalContext)
            {
                terms.Add(kvp.Key.ToLowerInvariant());
                terms.Add(kvp.Value.ToLowerInvariant());
            }
        }

        return terms.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Validates the move against applicable rules.
    /// Initial implementation uses basic heuristics; can be enhanced with AI/LLM.
    /// </summary>
    private MoveValidationResult ValidateAgainstRules(
        Move move,
        List<RuleAtom> applicableRules,
        GameSession session)
    {
        var errors = new List<string>();
        var suggestions = new List<string>();

        // If no applicable rules found, we can't validate with confidence
        if (applicableRules.Count == 0)
        {
            _logger.LogWarning(
                "No applicable rules found for move action '{Action}'",
                move.Action);

            return MoveValidationResult.Uncertain(
                "No specific rules found for this type of move",
                Array.Empty<RuleAtom>(),
                confidenceScore: 0.3);
        }

        // Basic validation: Check for common rule violations
        // This is a simplified implementation that can be enhanced with NLP/LLM

        // Example heuristic: Check if rules mention restrictions
        var hasRestrictions = applicableRules.Any(r =>
            r.text.ToLowerInvariant().Contains("cannot") ||
            r.text.ToLowerInvariant().Contains("must not") ||
            r.text.ToLowerInvariant().Contains("forbidden") ||
            r.text.ToLowerInvariant().Contains("illegal"));

        if (hasRestrictions)
        {
            // Found potential restrictions - flag for manual review
            suggestions.Add("This action may have restrictions. Review applicable rules carefully.");
        }

        // Check session-specific constraints
        if (session.Status == SessionStatus.Setup)
        {
            // During setup, certain moves might be restricted
            var setupRules = applicableRules
                .Where(r => r.text.ToLowerInvariant().Contains("setup") ||
                           r.text.ToLowerInvariant().Contains("start"))
                .ToList();

            if (setupRules.Any())
            {
                suggestions.Add("During setup phase, follow setup-specific rules.");
            }
        }

        // Calculate confidence based on rule matching
        // Higher confidence when more specific rules are found
        var confidence = CalculateConfidence(applicableRules, move);

        if (confidence < MinimumConfidenceThreshold)
        {
            return MoveValidationResult.Uncertain(
                "Low confidence in validation due to ambiguous rules",
                applicableRules,
                confidenceScore: confidence);
        }

        // If we reach here with no errors and sufficient confidence, move is likely valid
        if (errors.Count == 0)
        {
            _logger.LogInformation(
                "Move validated successfully with {RuleCount} applicable rules, confidence {Confidence:F2}",
                applicableRules.Count,
                confidence);

            return MoveValidationResult.Valid(
                applicableRules,
                confidenceScore: confidence,
                suggestions: suggestions.Count > 0 ? suggestions : null);
        }

        return MoveValidationResult.Invalid(
            errors,
            applicableRules,
            confidenceScore: confidence,
            suggestions: suggestions.Count > 0 ? suggestions : null);
    }

    /// <summary>
    /// Calculates confidence score for validation result.
    /// Based on number and specificity of applicable rules.
    /// </summary>
    private double CalculateConfidence(List<RuleAtom> applicableRules, Move move)
    {
        // Base confidence starts at 0.5
        var confidence = 0.5;

        // Increase confidence with more applicable rules (up to a point)
        if (applicableRules.Count >= 1) confidence += 0.1;
        if (applicableRules.Count >= 3) confidence += 0.1;
        if (applicableRules.Count >= 5) confidence += 0.1;

        // Increase confidence if rules have page/section references (more specific)
        var rulesWithReferences = applicableRules.Count(r =>
            !string.IsNullOrWhiteSpace(r.page) || !string.IsNullOrWhiteSpace(r.section));

        if (rulesWithReferences > 0)
        {
            confidence += 0.1 * (rulesWithReferences / (double)applicableRules.Count);
        }

        // Decrease confidence if move action is very generic
        var genericActions = new[] { "move", "play", "take", "use", "do" };
        if (genericActions.Contains(move.Action.ToLowerInvariant(), StringComparer.Ordinal))
        {
            confidence -= 0.1;
        }

        // Ensure confidence stays within bounds
        return Math.Max(0.0, Math.Min(1.0, confidence));
    }
}
