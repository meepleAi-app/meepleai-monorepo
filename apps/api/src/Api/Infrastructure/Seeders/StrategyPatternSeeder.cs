using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Seeds common game strategy patterns for AI agent decision-making.
/// Issue #3493: PostgreSQL Schema Extensions - Deferred strategy pattern seeding.
/// Issue #3956: Technical Debt - Complete deferred Phase 1+2 work.
/// Issue #3984: Add embedding generation and configuration toggle.
/// </summary>
internal static class StrategyPatternSeeder
{
    private const int EmbeddingBatchSize = 5;

    /// <summary>
    /// Seeds common game openings and strategies for known games.
    /// Only seeds if no strategy patterns exist for the game.
    /// Optionally generates embeddings for pattern descriptions when embedding service is available.
    /// </summary>
    public static async Task SeedAsync(
        MeepleAiDbContext db,
        ILogger logger,
        IEmbeddingService? embeddingService = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting strategy pattern seeding for common game openings");

        var seededCount = 0;
        var embeddingCount = 0;
        var allNewEntities = new List<StrategyPatternEntity>();

        foreach (var (gameName, patterns) in GameStrategyPatterns)
        {
            var game = await db.Games
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Name == gameName, cancellationToken)
                .ConfigureAwait(false);

            if (game == null)
            {
                logger.LogDebug("Game '{GameName}' not found in catalog, skipping strategy seeding", gameName);
                continue;
            }

            var existingCount = await db.StrategyPatterns
                .CountAsync(sp => sp.GameId == game.Id, cancellationToken)
                .ConfigureAwait(false);

            if (existingCount > 0)
            {
                logger.LogDebug(
                    "Game '{GameName}' already has {Count} strategy patterns, skipping",
                    gameName, existingCount);
                continue;
            }

            var entities = patterns.Select(p => new StrategyPatternEntity
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                PatternName = p.Name,
                ApplicablePhase = p.Phase,
                Description = p.Description,
                EvaluationScore = (float)p.Score,
                BoardConditionsJson = p.BoardConditions,
                MoveSequenceJson = p.MoveSequence,
                Source = p.Source
            }).ToList();

            db.StrategyPatterns.AddRange(entities);
            allNewEntities.AddRange(entities);
            seededCount += entities.Count;

            logger.LogInformation(
                "Seeded {Count} strategy patterns for '{GameName}'",
                entities.Count, gameName);
        }

        if (seededCount > 0)
        {
            if (embeddingService != null)
            {
                embeddingCount = await GenerateEmbeddingsAsync(
                    allNewEntities, embeddingService, logger, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                logger.LogWarning(
                    "Embedding service not available. {Count} patterns seeded without embeddings",
                    seededCount);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Strategy pattern seeding complete: {SeededCount} patterns seeded, {EmbeddingCount} embeddings generated",
            seededCount, embeddingCount);
    }

    private static async Task<int> GenerateEmbeddingsAsync(
        List<StrategyPatternEntity> entities,
        IEmbeddingService embeddingService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var embeddingCount = 0;

        for (var i = 0; i < entities.Count; i += EmbeddingBatchSize)
        {
            var batch = entities.Skip(i).Take(EmbeddingBatchSize).ToList();
            var texts = batch.Select(e => $"{e.PatternName}: {e.Description}").ToList();

            try
            {
                var result = await embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken)
                    .ConfigureAwait(false);

                if (!result.Success)
                {
                    logger.LogWarning(
                        "Embedding generation failed for batch {BatchIndex}: {Error}. Continuing without embeddings",
                        i / EmbeddingBatchSize, result.ErrorMessage);
                    continue;
                }

                for (var j = 0; j < batch.Count && j < result.Embeddings.Count; j++)
                {
                    batch[j].Embedding = new Vector(result.Embeddings[j]);
                    embeddingCount++;
                }

                logger.LogDebug(
                    "Generated embeddings for batch {BatchIndex} ({Count} patterns)",
                    i / EmbeddingBatchSize, batch.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Embedding generation failed for batch {BatchIndex}. Continuing without embeddings",
                    i / EmbeddingBatchSize);
            }
        }

        return embeddingCount;
    }

    private static readonly Dictionary<string, StrategyPatternSeedData[]> GameStrategyPatterns = new(StringComparer.Ordinal)
    {
        ["Chess"] =
        [
            new("Italian Game", "opening", "Classical opening with 1.e4 e5 2.Nf3 Nc6 3.Bc4. Aims to control center and develop pieces rapidly.", 0.82,
                """{"position":"starting","center_control":"e4_e5"}""",
                """{"sequence":["e4","e5","Nf3","Nc6","Bc4"]}""",
                "chess.com"),
            new("Sicilian Defense", "opening", "The most popular response to 1.e4. Creates asymmetric pawn structure with active counterplay.", 0.85,
                """{"position":"starting","pawn_structure":"asymmetric"}""",
                """{"sequence":["e4","c5"]}""",
                "chess.com"),
            new("Queen's Gambit", "opening", "Classical opening with 1.d4 d5 2.c4. White offers a pawn to gain center control.", 0.84,
                """{"position":"starting","gambit":"queens_side"}""",
                """{"sequence":["d4","d5","c4"]}""",
                "chess.com"),
            new("King's Indian Defense", "opening", "Hypermodern defense allowing White to build center, then counterattacking.", 0.80,
                """{"position":"starting","approach":"hypermodern"}""",
                """{"sequence":["d4","Nf6","c4","g6"]}""",
                "chess.com"),
            new("Ruy Lopez", "opening", "One of the oldest and most classical openings. Targets the knight defending e5.", 0.83,
                """{"position":"starting","target":"knight_c6"}""",
                """{"sequence":["e4","e5","Nf3","Nc6","Bb5"]}""",
                "chess.com"),
            new("Central Control Strategy", "midgame", "Maintain control of d4, d5, e4, e5 squares. Restrict opponent piece mobility.", 0.78,
                """{"phase":"midgame","center_pawns":"strong"}""",
                """{"strategy":"occupy_or_control_center_squares"}""",
                "manual"),
            new("King Safety Evaluation", "midgame", "Assess king safety based on pawn shield, piece activity near king, open files.", 0.75,
                """{"phase":"midgame","king_safety":"critical"}""",
                """{"strategy":"maintain_pawn_shield_and_piece_coordination"}""",
                "manual"),
            new("Basic Endgame Principles", "endgame", "King activation, passed pawns, and opposition. Fundamental endgame technique.", 0.88,
                """{"phase":"endgame","material":"simplified"}""",
                """{"strategy":"activate_king_push_passed_pawns"}""",
                "manual"),
        ],

        ["Catan"] =
        [
            new("Ore-Wheat Strategy", "opening", "Focus initial settlements on ore and wheat for fast city development.", 0.80,
                """{"resources_priority":["ore","wheat"],"goal":"cities"}""",
                """{"placement":"high_ore_wheat_hexes","development":"upgrade_settlements_early"}""",
                "manual"),
            new("Road Building Rush", "opening", "Expand quickly with roads to claim best resource hexes and longest road.", 0.75,
                """{"resources_priority":["brick","wood"],"goal":"longest_road"}""",
                """{"placement":"connected_brick_wood","development":"road_building_card"}""",
                "manual"),
            new("Port Strategy", "midgame", "Settle on 2:1 port early, focus on that resource for efficient trading.", 0.72,
                """{"trading":"port_focus","phase":"midgame"}""",
                """{"strategy":"monopolize_single_resource_with_port"}""",
                "manual"),
            new("Development Card Strategy", "midgame", "Invest in development cards for knights (largest army) and victory points.", 0.77,
                """{"resources_priority":["ore","wheat","sheep"],"goal":"largest_army"}""",
                """{"strategy":"buy_development_cards_prioritize_knights"}""",
                "manual"),
        ],

        ["Carcassonne"] =
        [
            new("Farmer Strategy", "opening", "Place farmers early on large fields connected to many cities.", 0.78,
                """{"focus":"fields","timing":"early"}""",
                """{"strategy":"place_farmers_on_large_connected_fields"}""",
                "manual"),
            new("City Stealing", "midgame", "Join your meeple to opponent's city to share or steal points.", 0.70,
                """{"phase":"midgame","tactic":"aggressive"}""",
                """{"strategy":"place_tiles_to_merge_cities_with_opponent"}""",
                "manual"),
            new("Road Completion", "any", "Focus on completing short roads for steady point generation.", 0.65,
                """{"focus":"roads","length":"short"}""",
                """{"strategy":"complete_short_roads_for_guaranteed_points"}""",
                "manual"),
        ],

        ["Ticket to Ride"] =
        [
            new("Long Route Focus", "opening", "Select and prioritize the longest route tickets for maximum points.", 0.82,
                """{"ticket_selection":"long_routes","risk":"medium"}""",
                """{"strategy":"keep_longest_tickets_build_backbone_first"}""",
                "manual"),
            new("Blocking Strategy", "midgame", "Identify and block key routes opponents need to complete tickets.", 0.68,
                """{"phase":"midgame","tactic":"defensive"}""",
                """{"strategy":"claim_critical_bottleneck_routes"}""",
                "manual"),
            new("Card Hoarding", "opening", "Collect specific color cards before claiming routes for efficiency.", 0.74,
                """{"phase":"opening","resource":"cards"}""",
                """{"strategy":"draw_matching_colors_before_claiming_routes"}""",
                "manual"),
        ],

        ["Pandemic"] =
        [
            new("Contain Early Outbreaks", "opening", "Prioritize treating cities with 3 disease cubes to prevent cascading outbreaks.", 0.90,
                """{"disease_cubes":"3","outbreak_risk":"high"}""",
                """{"strategy":"treat_3cube_cities_immediately","priority":"critical"}""",
                "manual"),
            new("Research Station Network", "midgame", "Build research stations strategically for efficient movement and curing.", 0.85,
                """{"phase":"midgame","stations":"strategic_placement"}""",
                """{"strategy":"place_stations_at_network_hubs_for_mobility"}""",
                "manual"),
            new("Card Sharing Protocol", "any", "Coordinate card sharing between players to collect sets for curing diseases.", 0.88,
                """{"cooperation":"card_sharing","goal":"cure_diseases"}""",
                """{"strategy":"meet_at_research_stations_share_matching_cards"}""",
                "manual"),
        ],

        ["Splendor"] =
        [
            new("Engine Building", "opening", "Focus on cheap gems cards early to build purchasing power for expensive cards.", 0.80,
                """{"phase":"opening","tier":"level_1"}""",
                """{"strategy":"buy_level1_cards_matching_noble_requirements"}""",
                "manual"),
            new("Noble Rush", "midgame", "Target specific nobles by collecting the gem types they require.", 0.76,
                """{"phase":"midgame","target":"nobles"}""",
                """{"strategy":"focus_purchases_on_noble_requirement_colors"}""",
                "manual"),
            new("Gold Token Control", "any", "Reserve high-value cards to get gold tokens and deny opponents.", 0.72,
                """{"resource":"gold_tokens","tactic":"reserve"}""",
                """{"strategy":"reserve_key_cards_accumulate_gold_for_flexibility"}""",
                "manual"),
        ],
    };

    private sealed record StrategyPatternSeedData(
        string Name,
        string Phase,
        string Description,
        double Score,
        string BoardConditions,
        string MoveSequence,
        string Source);
}
