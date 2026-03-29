using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Projections;

/// <summary>
/// Read-only DTO for copyright tier resolution.
/// Projected from DocumentProcessing BC's PdfDocumentEntity without direct BC coupling.
/// </summary>
public sealed record PdfCopyrightInfo(
    string DocumentId,
    LicenseType LicenseType,
    DocumentCategory DocumentCategory,
    Guid UploadedByUserId,
    Guid? GameId,
    Guid? PrivateGameId,
    bool IsPublic);
