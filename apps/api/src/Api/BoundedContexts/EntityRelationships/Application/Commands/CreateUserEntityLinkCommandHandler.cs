using Api.BoundedContexts.EntityRelationships.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.EntityRelationships.Application.Commands;

/// <summary>
/// Handler for <see cref="CreateUserEntityLinkCommand"/> (#4325).
/// </summary>
/// <remarks>
/// <para>
/// <c>POST /library/entity-links</c> used to write a link between any two ids with nothing but a session (measured:
/// 201 for <c>Game(any id) → KbCard(somebody else's PDF)</c>). Code reads a <c>Game → KbCard</c> link as "this game
/// uses this PDF" (<c>KbCardLinkRule</c>, the library listing of rulebooks), so the request of a user has to point at
/// things that exist and that the caller may link.
/// </para>
/// <list type="bullet">
/// <item><b>Game</b>, on either end: a private game of the caller that exists, or a catalog game that exists. When
/// the other end is a PDF the catalog game also has to be in the caller's library, as <c>AddRulebook</c> asks: otherwise
/// anybody could attach their PDF to any catalog game, and it would show up in the rulebook list of everyone who owns
/// that game.</item>
/// <item><b>KbCard</b>: a PDF the caller uploaded. A PDF of the catalog that somebody else uploaded is not the
/// caller's to link: <c>SharedGameId</c> is set on every PDF a user uploads against a catalog game, so it says nothing
/// about who may link it, and the reuse of such a PDF is done by the deduplication on its own, not through here.</item>
/// <item><b>Any other type</b> is not checked: only the pair that the code treats as fact is covered, as decided on
/// 2026-10-26. Player, Session, Agent, Document, ChatSession, Event and Toolkit are the next ones to cover if a
/// reader starts to trust them.</item>
/// </list>
/// <para>
/// "Does not exist" and "is not yours" give the same <see cref="NotFoundException"/>, so the answer does not say
/// which ids exist. The duplicate and the shape of the link are checked by <see cref="CreateEntityLinkCommand"/>,
/// which this handler hands the link to once the entities are agreed.
/// </para>
/// </remarks>
internal sealed class CreateUserEntityLinkCommandHandler
    : ICommandHandler<CreateUserEntityLinkCommand, EntityLinkDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly IMediator _mediator;

    public CreateUserEntityLinkCommandHandler(MeepleAiDbContext db, IMediator mediator)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<EntityLinkDto> Handle(CreateUserEntityLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var linksAPdf = command.SourceEntityType == MeepleEntityType.KbCard
                        || command.TargetEntityType == MeepleEntityType.KbCard;

        await EnsureTheCallerMayLinkAsync(
            command.SourceEntityType, command.SourceEntityId, command.OwnerUserId, linksAPdf, cancellationToken).ConfigureAwait(false);
        await EnsureTheCallerMayLinkAsync(
            command.TargetEntityType, command.TargetEntityId, command.OwnerUserId, linksAPdf, cancellationToken).ConfigureAwait(false);

        return await _mediator.Send(
            new CreateEntityLinkCommand(
                command.SourceEntityType,
                command.SourceEntityId,
                command.TargetEntityType,
                command.TargetEntityId,
                command.LinkType,
                EntityLinkScope.User,
                command.OwnerUserId,
                command.Metadata,
                IsBggImported: false),
            cancellationToken).ConfigureAwait(false);
    }

    // The soft-delete filters of the context (private and catalog games) leave out the games that are deleted.
    private async Task EnsureTheCallerMayLinkAsync(
        MeepleEntityType type, Guid id, Guid callerId, bool linksAPdf, CancellationToken cancellationToken)
    {
        var mayLink = type switch
        {
            MeepleEntityType.Game =>
                await _db.PrivateGames
                    .AnyAsync(g => g.Id == id && g.OwnerId == callerId, cancellationToken)
                    .ConfigureAwait(false)
                || await MayLinkACatalogGameAsync(id, callerId, linksAPdf, cancellationToken).ConfigureAwait(false),
            MeepleEntityType.KbCard =>
                await _db.PdfDocuments
                    .AnyAsync(p => p.Id == id && p.UploadedByUserId == callerId, cancellationToken)
                    .ConfigureAwait(false),
            _ => true,
        };

        if (!mayLink)
        {
            throw new NotFoundException(type.ToString(), id.ToString());
        }
    }

    /// <summary>
    /// A catalog game that exists. To attach a PDF to it, also one the caller has in the library (the same ask as
    /// <c>AddRulebook</c>).
    /// </summary>
    private async Task<bool> MayLinkACatalogGameAsync(
        Guid id, Guid callerId, bool linksAPdf, CancellationToken cancellationToken)
    {
        if (!await _db.SharedGames.AnyAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return !linksAPdf
               || await _db.UserLibraryEntries
                   .AnyAsync(e => e.UserId == callerId && e.SharedGameId == id, cancellationToken)
                   .ConfigureAwait(false);
    }
}
