using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;

/// <summary>
/// Builder for creating PdfDocument test instances with sensible defaults.
/// </summary>
public class PdfDocumentBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _gameId = Guid.NewGuid();
    private FileName _fileName = new("test-rulebook.pdf");
    private string _filePath = "/uploads/test-rulebook.pdf";
    private FileSize _fileSize = new(1024 * 1024); // 1 MB
    private Guid _uploadedByUserId = Guid.NewGuid();
    private string _processingStatus = "pending";
    private int? _pageCount;
    private string? _processingError;

    public PdfDocumentBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public PdfDocumentBuilder WithGameId(Guid gameId)
    {
        _gameId = gameId;
        return this;
    }

    public PdfDocumentBuilder WithFileName(string fileName)
    {
        _fileName = new FileName(fileName);
        return this;
    }

    public PdfDocumentBuilder WithFilePath(string filePath)
    {
        _filePath = filePath;
        return this;
    }

    public PdfDocumentBuilder WithFileSize(long bytes)
    {
        _fileSize = new FileSize(bytes);
        return this;
    }

    public PdfDocumentBuilder WithUploadedBy(Guid userId)
    {
        _uploadedByUserId = userId;
        return this;
    }

    /// <summary>
    /// Creates document in Processing status.
    /// </summary>
    public PdfDocumentBuilder ThatIsProcessing()
    {
        _processingStatus = "processing";
        return this;
    }

    /// <summary>
    /// Creates document in Completed status with page count.
    /// </summary>
    public PdfDocumentBuilder ThatIsCompleted(int pageCount = 10)
    {
        _processingStatus = "completed";
        _pageCount = pageCount;
        return this;
    }

    /// <summary>
    /// Creates document in Failed status with error message.
    /// </summary>
    public PdfDocumentBuilder ThatIsFailed(string error = "Processing error")
    {
        _processingStatus = "failed";
        _processingError = error;
        return this;
    }

    /// <summary>
    /// Creates a small PDF (100 KB).
    /// </summary>
    public PdfDocumentBuilder ThatIsSmall()
    {
        return WithFileSize(100 * 1024);
    }

    /// <summary>
    /// Creates a large PDF (10 MB).
    /// </summary>
    public PdfDocumentBuilder ThatIsLarge()
    {
        return WithFileSize(PdfUploadTestConstants.FileSizes.TestMaxBytes);
    }

    /// <summary>
    /// Creates a typical game rulebook (2-5 MB, 20-50 pages).
    /// </summary>
    public PdfDocumentBuilder AsTypicalRulebook()
    {
        return WithFileSize(3 * 1024 * 1024)
            .WithFileName("game-rulebook.pdf")
            .ThatIsCompleted(pageCount: 32);
    }

    /// <summary>
    /// Builds the PdfDocument instance.
    /// </summary>
    public PdfDocument Build()
    {
        var document = new PdfDocument(
            _id,
            _gameId,
            _fileName,
            _filePath,
            _fileSize,
            _uploadedByUserId);

        if (_processingStatus == "processing")
        {
            document.MarkAsProcessing();
        }
        else if (_processingStatus == "completed" && _pageCount.HasValue)
        {
            document.MarkAsProcessing();
            document.MarkAsCompleted(_pageCount.Value);
        }
        else if (_processingStatus == "failed" && _processingError != null)
        {
            document.MarkAsProcessing();
            document.MarkAsFailed(_processingError);
        }

        return document;
    }

    /// <summary>
    /// Implicit conversion to PdfDocument for convenience.
    /// </summary>
    public static implicit operator PdfDocument(PdfDocumentBuilder builder) => builder.Build();
}
