using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders.Catalog.SeedBlob;

[Trait("Category", TestCategories.Unit)]
public class S3SeedBlobReaderTests
{
    private readonly Mock<IAmazonS3> _s3Mock = new();
    private readonly S3SeedBlobReader _sut;

    public S3SeedBlobReaderTests()
    {
        _sut = new S3SeedBlobReader(_s3Mock.Object, "meepleai-seeds");
    }

    [Fact]
    public void IsConfigured_AlwaysTrue()
    {
        _sut.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ObjectPresent_ReturnsTrue()
    {
        _s3Mock
            .Setup(x => x.GetObjectMetadataAsync(
                "meepleai-seeds",
                "rulebooks/v1/test.pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse { HttpStatusCode = HttpStatusCode.OK });

        var result = await _sut.ExistsAsync("rulebooks/v1/test.pdf", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ObjectMissing_ReturnsFalse()
    {
        _s3Mock
            .Setup(x => x.GetObjectMetadataAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

        var result = await _sut.ExistsAsync("rulebooks/v1/missing.pdf", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsResponseStream()
    {
        var payload = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"
        _s3Mock
            .Setup(x => x.GetObjectAsync(
                "meepleai-seeds",
                "rulebooks/v1/test.pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = payload });

        await using var stream = await _sut.OpenReadAsync("rulebooks/v1/test.pdf", CancellationToken.None);
        var buffer = new byte[4];
        await stream.ReadExactlyAsync(buffer);

        buffer.Should().Equal(0x25, 0x50, 0x44, 0x46);
    }
}
