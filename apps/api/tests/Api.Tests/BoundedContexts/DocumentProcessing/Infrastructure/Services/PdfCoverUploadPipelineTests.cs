using Amazon.S3;
using Amazon.S3.Model;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Issue #4085/#4016 causa A — <c>PdfCoverUploadPipeline.UploadAsync</c> hardcoded
/// <c>DisablePayloadSigning = true</c>, which AWS SDK rejects against a plain-HTTP
/// endpoint with <c>AmazonClientException: When DisablePayloadSigning is true, the
/// request must be sent over HTTPS.</c> — observed in this exact job's logs against
/// MinIO (<c>http://minio:9000</c>) in local dev, where 125 of 133 Ready PDFs had
/// already exhausted their retry budget as a result.
///
/// <para>No behavior tests previously existed for this pipeline (only a DI-registration
/// test, <c>PdfCoverUploadPipelineRegistrationTests</c>); this file follows the same
/// mock-<c>IAmazonS3</c>-and-capture-the-request pattern already used by the sibling
/// pipelines (<c>BggCoverUploadPipelineTests</c>, <c>CoverR2UploadPipelineTests</c>).</para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4085")]
public sealed class PdfCoverUploadPipelineTests : IDisposable
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly Mock<ILogger<PdfCoverUploadPipeline>> _mockLogger;
    private readonly S3StorageOptions _httpsOptions;
    private readonly PdfCoverUploadPipeline _sut;

    public PdfCoverUploadPipelineTests()
    {
        _mockS3Client = new Mock<IAmazonS3>(MockBehavior.Strict);
        _mockLogger = new Mock<ILogger<PdfCoverUploadPipeline>>();
        _httpsOptions = new S3StorageOptions
        {
            Endpoint = "https://test.r2.cloudflarestorage.com",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            BucketName = "test-bucket",
            Region = "auto",
            PresignedUrlExpirySeconds = 3600,
            EnableEncryption = true,
            ForcePathStyle = false,
        };
        _sut = new PdfCoverUploadPipeline(_mockS3Client.Object, _httpsOptions, _mockLogger.Object);
    }

    public void Dispose() => _mockS3Client.Reset();

    [Fact]
    public async Task UploadAsync_PlainHttpEndpoint_DisablesPayloadSigningIsFalse()
    {
        var options = new S3StorageOptions
        {
            Endpoint = "http://minio:9000",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            BucketName = "test-bucket",
            Region = "auto",
            PresignedUrlExpirySeconds = 3600,
            EnableEncryption = true,
            ForcePathStyle = false,
        };
        var sut = new PdfCoverUploadPipeline(_mockS3Client.Object, options, _mockLogger.Object);
        PutObjectRequest? captured = null;
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        await sut.UploadAsync("db-key", new byte[] { 0x52, 0x49, 0x46, 0x46 }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DisablePayloadSigning.Should().Be(
            false,
            "AWS SDK rejects DisablePayloadSigning=true over plain HTTP — this was the measured #4016 failure");
    }

    [Fact]
    public async Task UploadAsync_HttpsEndpoint_DisablesPayloadSigningIsTrue()
    {
        PutObjectRequest? captured = null;
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        await _sut.UploadAsync("db-key", new byte[] { 0x52, 0x49, 0x46, 0x46 }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.DisablePayloadSigning.Should().Be(
            true,
            "R2/AWS over HTTPS keeps the pre-existing behaviour — staging/prod must not change");
    }

    [Fact]
    public async Task UploadAsync_ValidWebpBytes_ReturnsDbKeyUnchanged()
    {
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });

        var returned = await _sut.UploadAsync("my-db-key", new byte[] { 0x52, 0x49, 0x46, 0x46 }, CancellationToken.None);

        returned.Should().Be("my-db-key");
    }
}
