using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Handler for <see cref="GetPublishedMechanicCardByGameQuery"/> (#528). Delegates the by-game lookup
/// to <see cref="IMechanicCardRepository.GetActiveByGameAsync"/> (which honors the <c>!IsSuppressed</c>
/// query filter, so takedowns return null), then deserializes the immutable content snapshot and groups
/// the flat claim list into the 6 sections in enum order for the public card renderer.
/// </summary>
internal sealed class GetPublishedMechanicCardByGameQueryHandler
    : IRequestHandler<GetPublishedMechanicCardByGameQuery, PublishedMechanicCardDto?>
{
    private readonly IMechanicCardRepository _cards;

    public GetPublishedMechanicCardByGameQueryHandler(IMechanicCardRepository cards)
    {
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }

    public async Task<PublishedMechanicCardDto?> Handle(
        GetPublishedMechanicCardByGameQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var card = await _cards.GetActiveByGameAsync(request.SharedGameId, cancellationToken).ConfigureAwait(false);
        if (card is null)
        {
            return null; // no card, or suppressed → 404
        }

        MechanicCardContent? content;
        try
        {
            content = JsonSerializer.Deserialize<MechanicCardContent>(card.Content);
        }
        catch (JsonException)
        {
            content = null;
        }

        if (content is null)
        {
            return null;
        }

        // The persisted claims are a flat list carrying a `section` string; group them into the 6
        // sections, ordered by the MechanicSection enum (Summary..Faq) then by each claim's ordinal.
        var sections = content.Claims
            .GroupBy(c => c.Section, StringComparer.Ordinal)
            .OrderBy(g => SectionOrder(g.Key))
            .Select(g => new PublishedMechanicCardSectionDto(
                g.Key,
                g.OrderBy(c => c.Ordinal)
                    .Select(c => new PublishedMechanicCardClaimDto(
                        c.Id,
                        c.Claim,
                        c.Citations
                            .Select(cit => new PublishedMechanicCardCitationDto(cit.PdfId, cit.PdfPage, cit.Quote))
                            .ToList()))
                    .ToList()))
            .ToList();

        return new PublishedMechanicCardDto(
            CardId: card.Id,
            SharedGameId: card.SharedGameId,
            Title: card.Title,
            Version: card.Version,
            PublishedAt: card.PublishedAt,
            GameName: content.Metadata.SharedGameName,
            Publisher: content.Metadata.Publisher,
            Language: content.Metadata.Language,
            Sections: sections);
    }

    // Unknown section names sort last (defensive; the snapshot only writes enum names).
    private static int SectionOrder(string section) =>
        Enum.TryParse<MechanicSection>(section, ignoreCase: false, out var parsed) ? (int)parsed : int.MaxValue;
}
