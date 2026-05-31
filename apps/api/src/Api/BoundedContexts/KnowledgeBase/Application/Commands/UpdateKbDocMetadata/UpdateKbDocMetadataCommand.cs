using Api.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.UpdateKbDocMetadata;

/// <summary>
/// Command for <c>UpdateKbDocMetadataCommandHandler</c> — partial-update PATCH
/// of editable PDF document metadata. Issue #1687.
///
/// JSON null on any field = no-op (D-4); explicit empty string clears <c>Title</c>;
/// explicit empty array clears <c>Tags</c>. The validator gates each field with
/// <c>.When(x => x.Field is not null)</c>.
/// </summary>
/// <param name="DocId">Target PDF id (route parameter).</param>
/// <param name="EditorUserId">Authenticated caller's id (session principal).</param>
/// <param name="EditorRole">Caller's role string ("Owner", "Admin", "SuperAdmin", "Editor", "User", …).</param>
/// <param name="Title">New display title; null = no-op; empty/whitespace = clear.</param>
/// <param name="DocumentType">DocumentCategory enum name (case-insensitive); null = no-op.</param>
/// <param name="Language">ISO 639-1 code (whitelist via LanguageCode.IsSupported); null = no-op.</param>
/// <param name="Tags">Replacement tag set; null = no-op; empty = clear; deduped+lowercased+sorted by aggregate.</param>
internal sealed record UpdateKbDocMetadataCommand(
    Guid DocId,
    Guid EditorUserId,
    string EditorRole,
    string? Title,
    string? DocumentType,
    string? Language,
    IReadOnlyList<string>? Tags) : ICommand<UserKbDocDto>;
