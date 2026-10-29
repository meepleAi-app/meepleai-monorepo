namespace Api.Services.Pdf;

/// <summary>
/// The message a failed <c>IBlobStorageService.StoreAsync</c> puts in <see cref="BlobStorageResult.ErrorMessage"/>:
/// fixed, never the exception's message (#4660).
/// </summary>
/// <remarks>
/// That message reaches whoever uploaded: the standard and private upload results, the chunked-upload session.
/// An exception message is whatever its author put there: the bucket, the endpoint, a path on the host. The
/// services already log the failure with the exception, so the detail is in the log.
/// </remarks>
internal static class BlobStorageFailureText
{
    /// <summary>The object store (S3, R2, MinIO) rejected the write.</summary>
    public const string S3 = "S3 storage error";

    /// <summary>The local disk refused the write.</summary>
    public const string IO = "I/O error while storing the file";

    /// <summary>Anything else.</summary>
    public const string Generic = "Failed to store file";
}
