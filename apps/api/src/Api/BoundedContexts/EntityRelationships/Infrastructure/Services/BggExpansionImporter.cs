using System.Globalization;
using System.Xml.Linq;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.EntityRelationships.Infrastructure.Services;

/// <summary>
/// Fetches expansion/reimplements relationships from BGG XML API v2
/// and persists them as EntityLinks (IsBggImported=true, IsAdminApproved=true).
///
/// Issue #5141 (Epic A — EntityRelationships)
/// BGG XML API: https://boardgamegeek.com/xmlapi2/thing?id={bggId}&amp;type=boardgame
/// </summary>
internal sealed class BggExpansionImporter : IBggExpansionImporter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IEntityLinkRepository _entityLinkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BggExpansionImporter> _logger;

    private const string BggLinkTypeExpansion = "boardgameexpansion";
    private const string BggLinkTypeImplementation = "boardgameimplementation";

    public BggExpansionImporter(
        IHttpClientFactory httpClientFactory,
        ISharedGameRepository sharedGameRepository,
        IEntityLinkRepository entityLinkRepository,
        IUnitOfWork unitOfWork,
        ILogger<BggExpansionImporter> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _entityLinkRepository = entityLinkRepository ?? throw new ArgumentNullException(nameof(entityLinkRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<int> ImportExpansionsAsync(
        Guid sharedGameId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve the source SharedGame and validate it has a BGG ID
        var sharedGame = await _sharedGameRepository
            .GetByIdAsync(sharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (sharedGame is null)
            throw new NotFoundException($"SharedGame {sharedGameId} not found");

        if (!sharedGame.BggId.HasValue)
        {
            _logger.LogInformation(
                "BggExpansionImporter: SharedGame {Id} has no BggId — skipping import",
                sharedGameId);
            return 0;
        }

        // 2. Fetch the BGG XML for this game
        var xmlContent = await FetchBggXmlAsync(sharedGame.BggId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(xmlContent))
            return 0;

        var item = TryParseItem(xmlContent, sharedGame.BggId.Value);
        if (item is null)
            return 0;

        // 3. Extract link entries from XML
        var bggLinks = ExtractBggLinks(item);

        if (bggLinks.Count == 0)
        {
            _logger.LogInformation(
                "BggExpansionImporter: No expansion/reimplements links found for BggId {BggId}",
                sharedGame.BggId.Value);
            return 0;
        }

        // 4. Process each link and persist new EntityLinks
        int created = 0;
        foreach (var (linkedBggId, linkType, isInbound) in bggLinks)
        {
            created += await ProcessBggLinkAsync(
                sharedGame.Id,
                sharedGame.BggId.Value,
                linkedBggId,
                linkType,
                isInbound,
                adminUserId,
                cancellationToken).ConfigureAwait(false);
        }

        if (created > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "BggExpansionImporter: Created {Count} EntityLinks for SharedGame {Id} (BggId={BggId})",
                created, sharedGameId, sharedGame.BggId.Value);
        }

        return created;
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task<string?> FetchBggXmlAsync(int bggId, CancellationToken ct)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("BggApi");
            var url = $"thing?id={bggId}&type=boardgame";

            _logger.LogInformation("BggExpansionImporter: fetching {Url}", url);

            using var response = await httpClient.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BggExpansionImporter: BGG API returned {Status} for BggId {BggId}",
                    response.StatusCode, bggId);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "BggExpansionImporter: HTTP error fetching BggId {BggId}", bggId);
            return null;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeout (not caller-requested cancellation)
            _logger.LogWarning(ex, "BggExpansionImporter: timeout fetching BggId {BggId}", bggId);
            return null;
        }
    }

    private XElement? TryParseItem(string xmlContent, int bggId)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var item = doc.Root?.Element("item");
            if (item is null)
                _logger.LogWarning("BggExpansionImporter: No <item> element in BGG XML for BggId {BggId}", bggId);
            return item;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BggExpansionImporter: XML parse error for BggId {BggId}", bggId);
            return null;
        }
    }

    /// <summary>
    /// Returns tuples: (linkedBggId, bggLinkType, isInbound).
    /// isInbound=true means the current game IS THE expansion/reimplementation.
    /// </summary>
    private static List<(int LinkedBggId, string BggLinkType, bool IsInbound)> ExtractBggLinks(XElement item)
    {
        var results = new List<(int, string, bool)>();

        foreach (var link in item.Elements("link"))
        {
            var type = link.Attribute("type")?.Value;
            if (!string.Equals(type, BggLinkTypeExpansion, StringComparison.Ordinal) &&
                !string.Equals(type, BggLinkTypeImplementation, StringComparison.Ordinal))
                continue;

            if (!int.TryParse(link.Attribute("id")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var linkedId) || linkedId <= 0)
                continue;

            var inboundAttr = link.Attribute("inbound")?.Value;
            var isInbound = string.Equals(inboundAttr, "true", StringComparison.OrdinalIgnoreCase);

            results.Add((linkedId, type!, isInbound));
        }

        return results;
    }

    /// <summary>
    /// Resolves which EntityLink to create based on link type and directionality,
    /// then persists it if it doesn't already exist (BR-08).
    /// </summary>
    private async Task<int> ProcessBggLinkAsync(
        Guid currentGameId,
        int currentBggId,
        int linkedBggId,
        string bggLinkType,
        bool isInbound,
        Guid adminUserId,
        CancellationToken ct)
    {
        // Resolve linked game from our catalog
        var linkedGame = await _sharedGameRepository
            .GetByBggIdAsync(linkedBggId, ct)
            .ConfigureAwait(false);

        if (linkedGame is null)
        {
            _logger.LogDebug(
                "BggExpansionImporter: BggId {LinkedBggId} not in local catalog — skipped",
                linkedBggId);
            return 0;
        }

        // Determine EntityLink direction and type
        Guid sourceId;
        Guid targetId;
        EntityLinkType entityLinkType;

        if (string.Equals(bggLinkType, BggLinkTypeExpansion, StringComparison.Ordinal))
        {
            if (!isInbound)
            {
                // linkedGame IS an expansion OF currentGame
                sourceId = linkedGame.Id;
                targetId = currentGameId;
            }
            else
            {
                // currentGame IS an expansion OF linkedGame
                sourceId = currentGameId;
                targetId = linkedGame.Id;
            }
            entityLinkType = EntityLinkType.ExpansionOf;
        }
        else // boardgameimplementation
        {
            if (isInbound)
            {
                // currentGame REIMPLEMENTS linkedGame
                sourceId = currentGameId;
                targetId = linkedGame.Id;
            }
            else
            {
                // linkedGame REIMPLEMENTS currentGame
                sourceId = linkedGame.Id;
                targetId = currentGameId;
            }
            entityLinkType = EntityLinkType.Reimplements;
        }

        // BR-08: skip if link already exists
        var exists = await _entityLinkRepository.ExistsAsync(
            MeepleEntityType.Game, sourceId,
            MeepleEntityType.Game, targetId,
            entityLinkType,
            ct).ConfigureAwait(false);

        if (exists)
        {
            _logger.LogDebug(
                "BggExpansionImporter: link {Source}→{Target} ({Type}) already exists — skipped",
                sourceId, targetId, entityLinkType);
            return 0;
        }

        // Create EntityLink — IsBggImported=true ensures IsAdminApproved=true (BR-04 analogue)
        var link = EntityLink.Create(
            sourceEntityType: MeepleEntityType.Game,
            sourceEntityId: sourceId,
            targetEntityType: MeepleEntityType.Game,
            targetEntityId: targetId,
            linkType: entityLinkType,
            scope: EntityLinkScope.Shared,
            ownerUserId: adminUserId,
            metadata: $"{{\"bgg_source_id\":{currentBggId},\"bgg_linked_id\":{linkedBggId}}}",
            isBggImported: true);

        await _entityLinkRepository.AddAsync(link, ct).ConfigureAwait(false);

        _logger.LogDebug(
            "BggExpansionImporter: queued link {Source}→{Target} ({Type})",
            sourceId, targetId, entityLinkType);

        return 1;
    }
}
