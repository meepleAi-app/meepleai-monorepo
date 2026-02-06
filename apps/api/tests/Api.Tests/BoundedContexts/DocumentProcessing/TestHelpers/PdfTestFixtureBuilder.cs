using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;

/// <summary>
/// Fluent builder for creating test fixtures with all required mocks for PDF upload testing.
/// Eliminates duplication of mock setup across unit tests.
/// </summary>
/// <remarks>
/// This builder consolidates the setup of 10+ mock dependencies required by UploadPdfCommandHandler,
/// reducing test code duplication by >50% and improving test maintainability.
///
/// Usage:
/// <code>
/// var fixture = new PdfTestFixtureBuilder()
///     .WithDefaultMocks()
///     .WithQuotaAllowed()
///     .Build();
///
/// var handler = fixture.CreateHandler();
/// </code>
/// </remarks>
internal class PdfTestFixtureBuilder
{
    private MeepleAiDbContext? _dbContext;
    private Mock<IServiceScopeFactory>? _scopeFactoryMock;
    private Mock<ILogger<UploadPdfCommandHandler>>? _loggerMock;
    private Mock<IPdfTextExtractor>? _pdfTextExtractorMock;
    private Mock<IPdfTableExtractor>? _tableExtractorMock;
    private Mock<IBackgroundTaskService>? _backgroundTaskServiceMock;
    private Mock<IAiResponseCacheService>? _cacheServiceMock;
    private Mock<IBlobStorageService>? _blobStorageServiceMock;
    private Mock<IPdfUploadQuotaService>? _quotaServiceMock;
    private IOptions<PdfProcessingOptions>? _pdfOptions;
    private TimeProvider? _timeProvider;

    /// <summary>
    /// Sets up all mocks with default sensible behavior.
    /// This is the recommended starting point for most tests.
    /// </summary>
    public PdfTestFixtureBuilder WithDefaultMocks()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _loggerMock = new Mock<ILogger<UploadPdfCommandHandler>>();
        _pdfTextExtractorMock = new Mock<IPdfTextExtractor>();
        _tableExtractorMock = new Mock<IPdfTableExtractor>();
        _backgroundTaskServiceMock = new Mock<IBackgroundTaskService>();
        _cacheServiceMock = new Mock<IAiResponseCacheService>();
        _blobStorageServiceMock = new Mock<IBlobStorageService>();
        _quotaServiceMock = new Mock<IPdfUploadQuotaService>();
        _pdfOptions = Options.Create(new PdfProcessingOptions
        {
            MaxFileSizeBytes = 10 * 1024 * 1024 // 10 MB default
        });

        return this;
    }

    /// <summary>
    /// Creates a fresh in-memory database context for this test.
    /// </summary>
    public PdfTestFixtureBuilder WithFreshDbContext()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        return this;
    }

    /// <summary>
    /// Uses the provided database context.
    /// </summary>
    public PdfTestFixtureBuilder WithDbContext(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
        return this;
    }

    /// <summary>
    /// Configures quota service to allow unlimited uploads (default for success scenarios).
    /// </summary>
    public PdfTestFixtureBuilder WithQuotaAllowed()
    {
        if (_quotaServiceMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        _quotaServiceMock
            .Setup(q => q.CheckQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<UserTier>(),
                It.IsAny<Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PdfUploadQuotaResult.Success(
                dailyUsed: 0,
                dailyLimit: int.MaxValue,
                weeklyUsed: 0,
                weeklyLimit: int.MaxValue,
                dailyReset: DateTime.MaxValue,
                weeklyReset: DateTime.MaxValue));

        return this;
    }

    /// <summary>
    /// Configures quota service to deny uploads (for quota enforcement testing).
    /// </summary>
    public PdfTestFixtureBuilder WithQuotaExceeded(string reason = "Daily limit exceeded")
    {
        if (_quotaServiceMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        _quotaServiceMock
            .Setup(q => q.CheckQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<UserTier>(),
                It.IsAny<Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PdfUploadQuotaResult.Denied(
                errorMessage: reason,
                dailyUsed: 10,
                dailyLimit: 10,
                weeklyUsed: 50,
                weeklyLimit: 50,
                dailyReset: DateTime.UtcNow.AddDays(1),
                weeklyReset: DateTime.UtcNow.AddDays(7)));

        return this;
    }

    /// <summary>
    /// Configures PDF text extractor with successful extraction result.
    /// </summary>
    public PdfTestFixtureBuilder WithSuccessfulTextExtraction(
        string extractedText = "Mock extracted text from PDF",
        int pageCount = 10,
        ExtractionQuality quality = ExtractionQuality.High)
    {
        if (_pdfTextExtractorMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        _pdfTextExtractorMock
            .Setup(e => e.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: true,
                ExtractedText: extractedText,
                PageCount: pageCount,
                CharacterCount: extractedText.Length,
                OcrTriggered: false,
                Quality: quality,
                ErrorMessage: null));

        return this;
    }

    /// <summary>
    /// Configures PDF text extractor to fail (for error handling testing).
    /// </summary>
    public PdfTestFixtureBuilder WithFailedTextExtraction(string errorMessage = "Extraction failed")
    {
        if (_pdfTextExtractorMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        _pdfTextExtractorMock
            .Setup(e => e.ExtractTextAsync(
                It.IsAny<Stream>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TextExtractionResult(
                Success: false,
                ExtractedText: string.Empty,
                PageCount: 0,
                CharacterCount: 0,
                OcrTriggered: false,
                Quality: ExtractionQuality.VeryLow,
                ErrorMessage: errorMessage));

        return this;
    }

    /// <summary>
    /// Configures blob storage to succeed with default behavior.
    /// </summary>
    public PdfTestFixtureBuilder WithSuccessfulBlobStorage(string fileId = "test-file-123")
    {
        if (_blobStorageServiceMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        _blobStorageServiceMock
            .Setup(b => b.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream stream, string fileName, string gameId, CancellationToken ct) =>
                new BlobStorageResult(
                    Success: true,
                    FileId: fileId,
                    FilePath: $"/uploads/{gameId}/{fileName}",
                    FileSizeBytes: stream.Length,
                    ErrorMessage: null));

        return this;
    }

    /// <summary>
    /// Configures blob storage to fail (for error handling testing).
    /// </summary>
    public PdfTestFixtureBuilder WithFailedBlobStorage(string errorMessage = "Storage failed")
    {
        if (_blobStorageServiceMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        _blobStorageServiceMock
            .Setup(b => b.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: false,
                FileId: null,
                FilePath: null,
                FileSizeBytes: 0,
                ErrorMessage: errorMessage));

        return this;
    }

    /// <summary>
    /// Configures background task service (for enqueue verification).
    /// </summary>
    public PdfTestFixtureBuilder WithBackgroundTaskService()
    {
        if (_backgroundTaskServiceMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        _backgroundTaskServiceMock
            .Setup(b => b.ExecuteWithCancellation(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>()));

        return this;
    }

    /// <summary>
    /// Configures cache service (for invalidation verification).
    /// </summary>
    public PdfTestFixtureBuilder WithCacheService()
    {
        if (_cacheServiceMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        _cacheServiceMock
            .Setup(c => c.InvalidateGameAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return this;
    }

    /// <summary>
    /// Sets custom PDF processing options (file size limits, etc.).
    /// </summary>
    public PdfTestFixtureBuilder WithPdfOptions(PdfProcessingOptions options)
    {
        _pdfOptions = Options.Create(options);
        return this;
    }

    /// <summary>
    /// Sets a custom time provider (for time-dependent tests).
    /// </summary>
    public PdfTestFixtureBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return this;
    }

    /// <summary>
    /// Provides access to the scope factory mock for custom setup.
    /// </summary>
    public PdfTestFixtureBuilder ConfigureScopeFactory(Action<Mock<IServiceScopeFactory>> configure)
    {
        if (_scopeFactoryMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        configure(_scopeFactoryMock);
        return this;
    }

    /// <summary>
    /// Provides access to the logger mock for verification.
    /// </summary>
    public PdfTestFixtureBuilder ConfigureLogger(Action<Mock<ILogger<UploadPdfCommandHandler>>> configure)
    {
        if (_loggerMock == null)
            throw new InvalidOperationException("Call WithDefaultMocks() first");

        configure(_loggerMock);
        return this;
    }

    /// <summary>
    /// Builds the test fixture with all configured mocks.
    /// </summary>
    /// <returns>A configured test fixture ready for use.</returns>
    public PdfTestFixture Build()
    {
        if (_dbContext == null)
            WithFreshDbContext();

        if (_scopeFactoryMock == null)
            WithDefaultMocks();

        return new PdfTestFixture(
            _dbContext!,
            _scopeFactoryMock!,
            _loggerMock!,
            _pdfTextExtractorMock!,
            _tableExtractorMock!,
            _backgroundTaskServiceMock!,
            _cacheServiceMock!,
            _blobStorageServiceMock!,
            _quotaServiceMock!,
            _pdfOptions!,
            _timeProvider);
    }
}

/// <summary>
/// Test fixture containing all configured mocks for PDF upload testing.
/// Provides helper methods for creating handler instances and accessing mocks.
/// </summary>
internal sealed class PdfTestFixture : IDisposable
{
    public MeepleAiDbContext DbContext { get; }
    public Mock<IServiceScopeFactory> ScopeFactoryMock { get; }
    public Mock<ILogger<UploadPdfCommandHandler>> LoggerMock { get; }
    public Mock<IPdfTextExtractor> PdfTextExtractorMock { get; }
    public Mock<IPdfTableExtractor> TableExtractorMock { get; }
    public Mock<IBackgroundTaskService> BackgroundTaskServiceMock { get; }
    public Mock<IAiResponseCacheService> CacheServiceMock { get; }
    public Mock<IBlobStorageService> BlobStorageServiceMock { get; }
    public Mock<IPdfUploadQuotaService> QuotaServiceMock { get; }
    public IOptions<PdfProcessingOptions> PdfOptions { get; }
    public TimeProvider? TimeProvider { get; }

    public PdfTestFixture(
        MeepleAiDbContext dbContext,
        Mock<IServiceScopeFactory> scopeFactoryMock,
        Mock<ILogger<UploadPdfCommandHandler>> loggerMock,
        Mock<IPdfTextExtractor> pdfTextExtractorMock,
        Mock<IPdfTableExtractor> tableExtractorMock,
        Mock<IBackgroundTaskService> backgroundTaskServiceMock,
        Mock<IAiResponseCacheService> cacheServiceMock,
        Mock<IBlobStorageService> blobStorageServiceMock,
        Mock<IPdfUploadQuotaService> quotaServiceMock,
        IOptions<PdfProcessingOptions> pdfOptions,
        TimeProvider? timeProvider = null)
    {
        DbContext = dbContext;
        ScopeFactoryMock = scopeFactoryMock;
        LoggerMock = loggerMock;
        PdfTextExtractorMock = pdfTextExtractorMock;
        TableExtractorMock = tableExtractorMock;
        BackgroundTaskServiceMock = backgroundTaskServiceMock;
        CacheServiceMock = cacheServiceMock;
        BlobStorageServiceMock = blobStorageServiceMock;
        QuotaServiceMock = quotaServiceMock;
        PdfOptions = pdfOptions;
        TimeProvider = timeProvider;
    }

    /// <summary>
    /// Creates a new UploadPdfCommandHandler instance with all configured mocks.
    /// </summary>
    public UploadPdfCommandHandler CreateHandler()
    {
        return new UploadPdfCommandHandler(
            DbContext,
            ScopeFactoryMock.Object,
            LoggerMock.Object,
            PdfTextExtractorMock.Object,
            TableExtractorMock.Object,
            BackgroundTaskServiceMock.Object,
            CacheServiceMock.Object,
            BlobStorageServiceMock.Object,
            QuotaServiceMock.Object,
            PdfOptions,
            privateGameRepository: null,  // Issue #3664: New parameter
            timeProvider: TimeProvider);   // Moved to last parameter
    }

    /// <summary>
    /// Creates a handler with null dependency for constructor validation tests.
    /// </summary>
    public UploadPdfCommandHandler CreateHandlerWithNullDependency(string dependencyName)
    {
        return dependencyName switch
        {
            nameof(DbContext) => new UploadPdfCommandHandler(
                null!,
                ScopeFactoryMock.Object,
                LoggerMock.Object,
                PdfTextExtractorMock.Object,
                TableExtractorMock.Object,
                BackgroundTaskServiceMock.Object,
                CacheServiceMock.Object,
                BlobStorageServiceMock.Object,
                QuotaServiceMock.Object,
                PdfOptions),

            nameof(ScopeFactoryMock) => new UploadPdfCommandHandler(
                DbContext,
                null!,
                LoggerMock.Object,
                PdfTextExtractorMock.Object,
                TableExtractorMock.Object,
                BackgroundTaskServiceMock.Object,
                CacheServiceMock.Object,
                BlobStorageServiceMock.Object,
                QuotaServiceMock.Object,
                PdfOptions),

            nameof(LoggerMock) => new UploadPdfCommandHandler(
                DbContext,
                ScopeFactoryMock.Object,
                null!,
                PdfTextExtractorMock.Object,
                TableExtractorMock.Object,
                BackgroundTaskServiceMock.Object,
                CacheServiceMock.Object,
                BlobStorageServiceMock.Object,
                QuotaServiceMock.Object,
                PdfOptions),

            nameof(PdfTextExtractorMock) => new UploadPdfCommandHandler(
                DbContext,
                ScopeFactoryMock.Object,
                LoggerMock.Object,
                null!,
                TableExtractorMock.Object,
                BackgroundTaskServiceMock.Object,
                CacheServiceMock.Object,
                BlobStorageServiceMock.Object,
                QuotaServiceMock.Object,
                PdfOptions),

            nameof(TableExtractorMock) => new UploadPdfCommandHandler(
                DbContext,
                ScopeFactoryMock.Object,
                LoggerMock.Object,
                PdfTextExtractorMock.Object,
                null!,
                BackgroundTaskServiceMock.Object,
                CacheServiceMock.Object,
                BlobStorageServiceMock.Object,
                QuotaServiceMock.Object,
                PdfOptions),

            nameof(BackgroundTaskServiceMock) => new UploadPdfCommandHandler(
                DbContext,
                ScopeFactoryMock.Object,
                LoggerMock.Object,
                PdfTextExtractorMock.Object,
                TableExtractorMock.Object,
                null!,
                CacheServiceMock.Object,
                BlobStorageServiceMock.Object,
                QuotaServiceMock.Object,
                PdfOptions),

            nameof(CacheServiceMock) => new UploadPdfCommandHandler(
                DbContext,
                ScopeFactoryMock.Object,
                LoggerMock.Object,
                PdfTextExtractorMock.Object,
                TableExtractorMock.Object,
                BackgroundTaskServiceMock.Object,
                null!,
                BlobStorageServiceMock.Object,
                QuotaServiceMock.Object,
                PdfOptions),

            nameof(BlobStorageServiceMock) => new UploadPdfCommandHandler(
                DbContext,
                ScopeFactoryMock.Object,
                LoggerMock.Object,
                PdfTextExtractorMock.Object,
                TableExtractorMock.Object,
                BackgroundTaskServiceMock.Object,
                CacheServiceMock.Object,
                null!,
                QuotaServiceMock.Object,
                PdfOptions),

            nameof(QuotaServiceMock) => new UploadPdfCommandHandler(
                DbContext,
                ScopeFactoryMock.Object,
                LoggerMock.Object,
                PdfTextExtractorMock.Object,
                TableExtractorMock.Object,
                BackgroundTaskServiceMock.Object,
                CacheServiceMock.Object,
                BlobStorageServiceMock.Object,
                null!,
                PdfOptions),

            nameof(PdfOptions) => new UploadPdfCommandHandler(
                DbContext,
                ScopeFactoryMock.Object,
                LoggerMock.Object,
                PdfTextExtractorMock.Object,
                TableExtractorMock.Object,
                BackgroundTaskServiceMock.Object,
                CacheServiceMock.Object,
                BlobStorageServiceMock.Object,
                QuotaServiceMock.Object,
                null!),

            _ => throw new ArgumentException($"Unknown dependency: {dependencyName}", nameof(dependencyName))
        };
    }

    public void Dispose()
    {
        DbContext.Dispose();
    }
}
