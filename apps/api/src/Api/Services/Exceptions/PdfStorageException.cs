namespace Api.Services.Exceptions;

/// <summary>
/// Exception thrown when PDF storage operations fail
/// </summary>
public class PdfStorageException : Exception
{
    public PdfStorageException(string message) : base(message)
    {
    }

    public PdfStorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
