using Api.Services.Pdf;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Services;

/// <summary>
/// Issue #3498 — the presigned URL's scheme must follow the configured endpoint's scheme.
///
/// The AWS SDK decides a presigned URL's protocol from <c>AmazonS3Config.UseHttp</c>, NOT from
/// the scheme embedded in <c>ServiceURL</c>. With <c>UseHttp</c> left at its default (false), a
/// <c>ServiceURL</c> of <c>http://localhost:9000</c> still presigns as <c>https://localhost:9000</c>.
/// The browser then opens a TLS handshake against a plain-HTTP MinIO, the image fails to load, and
/// the card silently falls back to its emoji placeholder — a failure that looks like a missing
/// object rather than a scheme mismatch.
///
/// Production (R2/AWS) is unaffected: those endpoints are https, so the predicate returns false
/// and <c>UseHttp</c> stays off.
/// </summary>
[Trait("Category", "Unit")]
public class BlobStorageServiceFactoryTests
{
    [Theory]
    [InlineData("http://localhost:9000")]
    [InlineData("http://minio:9000")]
    [InlineData("HTTP://MINIO:9000")]
    [InlineData("http://127.0.0.1:9000/")]
    public void UsesPlainHttp_PlainHttpEndpoint_ReturnsTrue(string endpoint)
    {
        BlobStorageServiceFactory.UsesPlainHttp(endpoint).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://abc123.r2.cloudflarestorage.com")]
    [InlineData("https://s3.amazonaws.com")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    public void UsesPlainHttp_HttpsEndpoint_ReturnsFalse(string endpoint)
    {
        BlobStorageServiceFactory.UsesPlainHttp(endpoint).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("minio:9000")] // scheme-less: not parseable as absolute, must not opt into http
    public void UsesPlainHttp_MissingOrUnparseableEndpoint_ReturnsFalse(string? endpoint)
    {
        BlobStorageServiceFactory.UsesPlainHttp(endpoint).Should().BeFalse();
    }
}
