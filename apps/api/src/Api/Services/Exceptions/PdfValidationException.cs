namespace Api.Services.Exceptions;

/// <summary>
/// Exception thrown when PDF validation operations fail
/// </summary>
public class PdfValidationException : Exception
{
    public PdfValidationException(string message) : base(message)
    {
    }

    public PdfValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
    public PdfValidationException()
    {
    }
}
