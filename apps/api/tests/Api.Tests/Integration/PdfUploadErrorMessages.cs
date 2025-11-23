using FluentAssertions;

namespace Api.Tests.Integration;

/// <summary>
/// Expected error messages for PDF upload validation.
/// Defined as constants to avoid magic strings in test assertions (Issue #1732).
///
/// IMPORTANT: These constants represent the API contract for error messages.
/// Changes to these messages should be coordinated with frontend/API consumers.
/// </summary>
public static class PdfUploadErrorMessages
{
    #region File Validation Errors

    /// <summary>
    /// Error when no file is provided or file is empty.
    /// Source: UploadPdfCommandHandler.cs line 86
    /// </summary>
    public const string NoFileProvided = "No file provided";

    /// <summary>
    /// Error pattern when file exceeds size limit.
    /// Source: UploadPdfCommandHandler.cs line 94
    /// </summary>
    public const string FileTooLarge = "File is too large";
    public const string FileSizeMaximum = "Maximum size";

    /// <summary>
    /// Error pattern for invalid content type.
    /// Source: UploadPdfCommandHandler.cs line 100
    /// </summary>
    public const string InvalidFileType = "Invalid file type";
    public const string OnlyPdfAllowed = "Only PDF files are allowed";

    #endregion

    #region PDF Structure Validation Errors

    /// <summary>
    /// Error prefix for all PDF validation failures.
    /// Source: ValidatePdfStructureAsync method
    /// </summary>
    public const string InvalidPdfFile = "Invalid PDF file";

    /// <summary>
    /// Error when PDF file is too small to be valid.
    /// Source: UploadPdfCommandHandler.cs line 311
    /// </summary>
    public const string PdfTooSmall = "too small to be a valid PDF";
    public const string MinimumBytesRequired = "minimum 50 bytes required";

    /// <summary>
    /// Error when PDF header signature is missing (corrupted file).
    /// Source: UploadPdfCommandHandler.cs line 323
    /// </summary>
    public const string MissingPdfHeader = "Missing PDF header signature";
    public const string CorruptedOrNotPdf = "corrupted or not a valid PDF";

    /// <summary>
    /// Error when PDF EOF marker is missing (incomplete/malformed file).
    /// Source: UploadPdfCommandHandler.cs line 336
    /// </summary>
    public const string MissingEofMarker = "Missing PDF end-of-file marker";
    public const string IncompleteOrMalformed = "incomplete or malformed";

    /// <summary>
    /// Error pattern when PDF structure validation fails with exception.
    /// Source: UploadPdfCommandHandler.cs line 348
    /// </summary>
    public const string FailedToValidateStructure = "Failed to validate PDF structure";

    #endregion

    #region Storage and Permission Errors

    /// <summary>
    /// Error when blob storage fails.
    /// Source: UploadPdfCommandHandler.cs line 200
    /// </summary>
    public const string FailedToStoreFile = "Failed to store file";
    public const string StorageFailurePattern = "storage";

    /// <summary>
    /// Error pattern for permission denied scenarios.
    /// Common in mock: "Access denied: Insufficient permissions"
    /// </summary>
    public const string AccessDenied = "Access denied";
    public const string InsufficientPermissions = "Insufficient permissions";
    public const string PermissionPattern = "permission";

    #endregion

    #region Entity Validation Errors

    /// <summary>
    /// Error when file name is invalid.
    /// Source: UploadPdfCommandHandler.cs line 120, 130
    /// </summary>
    public const string InvalidFileName = "Invalid file name";

    /// <summary>
    /// Error when game is not found.
    /// Source: UploadPdfCommandHandler.cs line 140
    /// </summary>
    public const string GameNotFound = "Game not found";

    /// <summary>
    /// Error when user is not found.
    /// Source: UploadPdfCommandHandler.cs line 155
    /// </summary>
    public const string UserNotFound = "User not found";

    #endregion
}

/// <summary>
/// Helper extension methods for FluentAssertions to validate PDF upload error messages.
/// Provides specific, non-magic-string assertions that catch regressions (Issue #1732).
/// </summary>
public static class PdfUploadAssertionExtensions
{
    /// <summary>
    /// Asserts that the message indicates a corrupted PDF validation failure.
    /// Accepts any "Invalid PDF file" message including: too small, missing header, or general corruption.
    /// </summary>
    public static void ShouldIndicateCorruptedPdf(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Match(m =>
            m.Contains(PdfUploadErrorMessages.InvalidPdfFile, StringComparison.OrdinalIgnoreCase) &&
            (m.Contains(PdfUploadErrorMessages.CorruptedOrNotPdf, StringComparison.OrdinalIgnoreCase) ||
             m.Contains(PdfUploadErrorMessages.MissingPdfHeader, StringComparison.OrdinalIgnoreCase) ||
             m.Contains(PdfUploadErrorMessages.PdfTooSmall, StringComparison.OrdinalIgnoreCase)),
            because.Length > 0 ? because : "should indicate PDF is corrupted with specific validation message");
    }

    /// <summary>
    /// Asserts that the message indicates an invalid content type rejection.
    /// </summary>
    public static void ShouldIndicateInvalidContentType(this string? message, string expectedContentType, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(PdfUploadErrorMessages.InvalidFileType, because);
        message.Should().Contain(expectedContentType, because);
        message.Should().Contain(PdfUploadErrorMessages.OnlyPdfAllowed, because);
    }

    /// <summary>
    /// Asserts that the message indicates file size limit exceeded.
    /// </summary>
    public static void ShouldIndicateFileTooLarge(this string? message, int? expectedMaxSizeMb = null, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Contain(PdfUploadErrorMessages.FileTooLarge, because);
        message.Should().Contain(PdfUploadErrorMessages.FileSizeMaximum, because);

        if (expectedMaxSizeMb.HasValue)
        {
            message.Should().Contain(expectedMaxSizeMb.Value.ToString(), because);
        }
    }

    /// <summary>
    /// Asserts that the message indicates blob storage failure.
    /// </summary>
    public static void ShouldIndicateStorageFailure(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Match(m =>
            m.Contains(PdfUploadErrorMessages.FailedToStoreFile, StringComparison.OrdinalIgnoreCase) ||
            m.Contains(PdfUploadErrorMessages.StorageFailurePattern, StringComparison.OrdinalIgnoreCase),
            because.Length > 0 ? because : "should indicate blob storage failure");
    }

    /// <summary>
    /// Asserts that the message indicates permission denied error.
    /// </summary>
    public static void ShouldIndicatePermissionDenied(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Match(m =>
            m.Contains(PdfUploadErrorMessages.AccessDenied, StringComparison.OrdinalIgnoreCase) ||
            m.Contains(PdfUploadErrorMessages.InsufficientPermissions, StringComparison.OrdinalIgnoreCase) ||
            m.Contains(PdfUploadErrorMessages.PermissionPattern, StringComparison.OrdinalIgnoreCase),
            because.Length > 0 ? because : "should indicate permission denied error");
    }

    /// <summary>
    /// Asserts that the message indicates malformed PDF structure.
    /// </summary>
    public static void ShouldIndicateMalformedPdf(this string? message, string because = "")
    {
        message.Should().NotBeNullOrWhiteSpace(because);
        message.Should().Match(m =>
            m.Contains(PdfUploadErrorMessages.InvalidPdfFile, StringComparison.OrdinalIgnoreCase) &&
            (m.Contains(PdfUploadErrorMessages.IncompleteOrMalformed, StringComparison.OrdinalIgnoreCase) ||
             m.Contains(PdfUploadErrorMessages.MissingEofMarker, StringComparison.OrdinalIgnoreCase) ||
             m.Contains(PdfUploadErrorMessages.PdfTooSmall, StringComparison.OrdinalIgnoreCase)),
            because.Length > 0 ? because : "should indicate PDF structure is malformed");
    }
}
