#pragma warning disable MA0048 // File name must match type name - Contains multiple supporting DTOs
namespace Api.Routing;

internal record InitChunkedUploadRequest(Guid? GameId, string FileName, long TotalFileSize, Guid? PrivateGameId = null);
internal record CompleteChunkedUploadRequest(Guid SessionId);
internal record SetPdfVisibilityRequest(bool IsPublic);
internal record SetActiveForRagRequest(bool IsActive); // Issue #5446
internal record ReclassifyDocumentRequest(string Category, Guid? BaseDocumentId, string? VersionLabel); // Issue #5447
internal record ExtractBggGamesRequest(string PdfFilePath); // ISSUE-2513
internal record OverridePdfLanguageRequest(string? LanguageCode); // E5-2
