using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.UpdateKbDocMetadata;

/// <summary>
/// Handler for <see cref="UpdateKbDocMetadataCommand"/>. Issue #1687 Task 6.
///
/// Flow:
/// <list type="number">
/// <item>Load the <c>PdfDocument</c> aggregate via the repository.</item>
/// <item>Authorize: owner OR Admin/SuperAdmin; everyone else gets <see cref="NotFoundException"/>
///       (D-2 anti-info-leak — divergence from <c>HandleUpdateChatThreadTitle</c> which returns 403).</item>
/// <item>For each non-null field on the command, compare current vs new (using the aggregate's normalization
///       rules) and apply via the SRP-narrow mutator if different.</item>
/// <item>If the change list is empty (idempotent no-op) skip persistence and event emission.</item>
/// <item>Otherwise raise the <c>PdfMetadataChangedEvent</c> via the aggregate, save, and return the updated DTO.</item>
/// </list>
/// </summary>
internal sealed class UpdateKbDocMetadataCommandHandler : ICommandHandler<UpdateKbDocMetadataCommand, UserKbDocDto>
{
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateKbDocMetadataCommandHandler> _logger;

    public UpdateKbDocMetadataCommandHandler(
        IPdfDocumentRepository pdfRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateKbDocMetadataCommandHandler> logger)
    {
        _pdfRepository = pdfRepository ?? throw new ArgumentNullException(nameof(pdfRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserKbDocDto> Handle(UpdateKbDocMetadataCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Step 1: load the aggregate.
        var doc = await _pdfRepository.GetByIdAsync(command.DocId, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            // D-2: never disambiguate "doesn't exist" from "can't see".
            throw new NotFoundException("KbDocument", command.DocId.ToString());
        }

        // Step 2: authorize. D-1 RBAC + D-2 anti-info-leak.
        var isOwner = doc.UploadedByUserId == command.EditorUserId;
        var isAdmin = IsAdminRole(command.EditorRole);
        if (!isOwner && !isAdmin)
        {
            _logger.LogWarning(
                "User {EditorId} denied PATCH on KB doc {DocId} (owner: {OwnerId}, role: {Role})",
                command.EditorUserId, command.DocId, doc.UploadedByUserId, command.EditorRole);
            throw new NotFoundException("KbDocument", command.DocId.ToString());
        }

        // Step 3: snapshot pre-mutation state for accurate OldValue diff entries.
        var preTitle = doc.Title;
        var preCategory = doc.DocumentCategory;
        var preLanguage = doc.LanguageOverride;
        var preTags = doc.Tags.ToList();

        // Step 4: diff & apply. Skip no-op writes (handler compares current vs new
        // using the aggregate's canonicalization rules so "Same Title" emits no event).
        var changes = new List<MetadataChange>();

        if (command.Title is not null)
        {
            var newTitle = NormalizeTitle(command.Title);
            if (!string.Equals(preTitle, newTitle, StringComparison.Ordinal))
            {
                doc.SetTitle(command.Title, command.EditorUserId);
                changes.Add(new MetadataChange("title", preTitle, newTitle));
            }
        }

        if (command.DocumentType is not null)
        {
            if (Enum.TryParse<DocumentCategory>(command.DocumentType, ignoreCase: true, out var parsedCategory)
                && preCategory != parsedCategory)
            {
                doc.SetCategory(parsedCategory, command.EditorUserId);
                changes.Add(new MetadataChange("documentType", preCategory.ToString(), parsedCategory.ToString()));
            }
        }

        if (command.Language is not null)
        {
            var newLanguage = command.Language.Trim().ToLowerInvariant();
            if (!string.Equals(preLanguage, newLanguage, StringComparison.Ordinal))
            {
                doc.OverrideLanguageByEditor(newLanguage, command.EditorUserId);
                changes.Add(new MetadataChange("language", preLanguage, newLanguage));
            }
        }

        if (command.Tags is not null)
        {
            var newTags = NormalizeTags(command.Tags);
            if (!preTags.SequenceEqual(newTags, StringComparer.Ordinal))
            {
                doc.SetTags(command.Tags, command.EditorUserId);
                changes.Add(new MetadataChange(
                    "tags",
                    OldValue: $"[{string.Join(",", preTags)}]",
                    NewValue: $"[{string.Join(",", newTags)}]"));
            }
        }

        // Step 5: idempotent no-op — return current DTO without persisting.
        if (changes.Count == 0)
        {
            _logger.LogDebug(
                "PATCH /kb-docs/{DocId} is a no-op for user {EditorId} — no changes detected",
                command.DocId, command.EditorUserId);
            return await BuildDtoAsync(doc, cancellationToken).ConfigureAwait(false);
        }

        // Step 6: emit + persist.
        doc.RaiseMetadataChangedEvent(changes, command.EditorUserId, ResolveEditorRoleLabel(isOwner, command.EditorRole));

        await _pdfRepository.UpdateAsync(doc, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "PATCH /kb-docs/{DocId} updated by user {EditorId} (role: {Role}): {ChangeCount} field(s) changed",
            command.DocId, command.EditorUserId, command.EditorRole, changes.Count);

        return await BuildDtoAsync(doc, cancellationToken).ConfigureAwait(false);
    }

    private static string? NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;
        var trimmed = title.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string> tags)
    {
        return tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsAdminRole(string role)
    {
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveEditorRoleLabel(bool isOwner, string requestedRole)
    {
        // Owner takes precedence even if the role string says Admin (the user owns the doc).
        return isOwner ? "Owner" : requestedRole;
    }

    private async Task<UserKbDocDto> BuildDtoAsync(PdfDocument doc, CancellationToken cancellationToken)
    {
        string? gameName = null;
        if (doc.SharedGameId is Guid gameId)
        {
            var names = await _sharedGameRepository
                .GetNamesByIdsAsync(new[] { gameId }, cancellationToken)
                .ConfigureAwait(false);
            if (names.TryGetValue(gameId, out var resolved))
                gameName = resolved;
        }

        return new UserKbDocDto(
            Id: doc.Id,
            GameId: doc.SharedGameId,
            GameName: gameName,
            FileName: doc.FileName.Value,
            ProcessingState: doc.ProcessingState.ToString(),
            PageCount: doc.PageCount,
            ProcessedAt: doc.ProcessedAt,
            UploadedAt: doc.UploadedAt,
            UpdatedAt: doc.UpdatedAt ?? doc.ProcessedAt ?? doc.UploadedAt,
            Title: doc.Title,
            Tags: doc.Tags,
            UpdatedBy: doc.UpdatedBy);
    }
}
