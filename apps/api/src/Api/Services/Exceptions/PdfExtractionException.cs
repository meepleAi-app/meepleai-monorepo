namespace Api.Services.Exceptions;

/// <summary>
/// Exception thrown when PDF text extraction operations fail
/// </summary>
public class PdfExtractionException : Exception
{
    public PdfExtractionException(string message) : base(message)
    {
    }

    public PdfExtractionException(string message, Exception innerException) : base(message, innerException)
    {
    }
    public PdfExtractionException()
    {
    }
}
