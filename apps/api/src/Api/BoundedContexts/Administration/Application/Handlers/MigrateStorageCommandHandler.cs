using Api.BoundedContexts.Administration.Application.Commands;
using Api.Services.Pdf;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handles migration of files from local filesystem to S3-compatible storage.
/// Scans pdf_uploads/ directory, uploads each file to S3, skips files already on S3.
/// </summary>
internal sealed class MigrateStorageCommandHandler : IRequestHandler<MigrateStorageCommand, MigrateStorageResult>
{
    private readonly IBlobStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MigrateStorageCommandHandler> _logger;

    public MigrateStorageCommandHandler(
        IBlobStorageService storageService,
        IConfiguration configuration,
        ILogger<MigrateStorageCommandHandler> logger)
    {
        _storageService = storageService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MigrateStorageResult> Handle(MigrateStorageCommand request, CancellationToken cancellationToken)
    {
        var storageProvider = _configuration["STORAGE_PROVIDER"]?.ToLowerInvariant() ?? "local";

        if (!string.Equals(storageProvider, "s3", StringComparison.Ordinal))
        {
            return new MigrateStorageResult(0, 0, 0, 0, request.DryRun, 0,
                new List<string> { "STORAGE_PROVIDER is not set to 's3'. Migration requires S3 to be the active provider." });
        }

        // Find local PDF storage directory
        var localPath = _configuration["PDF_STORAGE_PATH"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "pdf_uploads");

        if (!Directory.Exists(localPath))
        {
            return new MigrateStorageResult(0, 0, 0, 0, request.DryRun, 0,
                new List<string> { $"Local storage directory not found: {localPath}" });
        }

        // Scan all files in pdf_uploads/{gameId}/{fileId}_{filename} pattern
        var allFiles = Directory.GetFiles(localPath, "*", SearchOption.AllDirectories);
        var errors = new List<string>();
        var migrated = 0;
        var skipped = 0;
        var failed = 0;
        long totalSizeBytes = 0;

        _logger.LogInformation(
            "Storage migration {Mode}: found {Count} files in {Path}",
            request.DryRun ? "DRY RUN" : "EXECUTE", allFiles.Length, localPath);

        foreach (var filePath in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var fileInfo = new FileInfo(filePath);
                totalSizeBytes += fileInfo.Length;

                // Parse path: pdf_uploads/{gameId}/{fileId}_{filename}
                var relativePath = Path.GetRelativePath(localPath, filePath);
                var parts = relativePath.Replace('\\', '/').Split('/');

                if (parts.Length < 2)
                {
                    errors.Add($"Skipping file with unexpected path structure: {relativePath}");
                    failed++;
                    continue;
                }

                var gameId = parts[0];
                var fileNameWithId = parts[^1]; // Last segment: {fileId}_{filename}
                var underscoreIndex = fileNameWithId.IndexOf('_', StringComparison.Ordinal);

                if (underscoreIndex < 1)
                {
                    errors.Add($"Skipping file with unexpected name format (no fileId): {relativePath}");
                    failed++;
                    continue;
                }

                var fileId = fileNameWithId[..underscoreIndex];

                // Check if file already exists on S3
                var exists = await _storageService.ExistsAsync(fileId, gameId, cancellationToken)
                    .ConfigureAwait(false);

                if (exists)
                {
                    skipped++;
                    continue;
                }

                if (request.DryRun)
                {
                    migrated++; // Count as "would be migrated"
                    continue;
                }

                // For migration, we need to preserve the exact S3 key. Since StoreAsync generates
                // a new fileId, we access the S3 client directly through the service.
                if (_storageService is S3BlobStorageService s3Service)
                {
                    // Upload to S3
                    var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await using (fileStream.ConfigureAwait(false))
                    {
                        var s3Key = $"pdf_uploads/{gameId}/{fileNameWithId}";
                        var putRequest = new Amazon.S3.Model.PutObjectRequest
                        {
                            BucketName = s3Service.Options.BucketName,
                            Key = s3Key,
                            InputStream = fileStream,
                            ContentType = "application/pdf",
                            AutoCloseStream = false
                        };

                        if (s3Service.Options.EnableEncryption)
                        {
                            putRequest.ServerSideEncryptionMethod = Amazon.S3.ServerSideEncryptionMethod.AES256;
                        }

                        await s3Service.S3Client.PutObjectAsync(putRequest, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    _logger.LogInformation(
                        "Migrated {Key} ({Size} bytes)", $"pdf_uploads/{gameId}/{fileNameWithId}", fileInfo.Length);
                    migrated++;
                }
                else
                {
                    errors.Add("Storage service is not S3BlobStorageService despite STORAGE_PROVIDER=s3");
                    failed++;
                }
            }
#pragma warning disable CA1031 // Service boundary: migration must continue on individual file errors
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var relativePath = Path.GetRelativePath(localPath, filePath);
                errors.Add($"Error migrating {relativePath}: {ex.Message}");
                failed++;
                _logger.LogWarning(ex, "Failed to migrate file {Path}", filePath);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation(
            "Storage migration {Mode} complete: {Total} files, {Migrated} migrated, {Skipped} skipped, {Failed} failed",
            request.DryRun ? "DRY RUN" : "EXECUTE", allFiles.Length, migrated, skipped, failed);

        return new MigrateStorageResult(allFiles.Length, migrated, skipped, failed, request.DryRun, totalSizeBytes, errors);
    }
}
