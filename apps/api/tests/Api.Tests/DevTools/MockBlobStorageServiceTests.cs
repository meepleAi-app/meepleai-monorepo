using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Xunit;

namespace Api.Tests.DevTools;

public class MockBlobStorageServiceTests
{
    [Fact]
    public async Task StoreAndRetrieve_RoundTrip()
    {
        var svc = new MockBlobStorageService();
        var content = System.Text.Encoding.UTF8.GetBytes("hello");
        using var input = new MemoryStream(content);
        var stored = await svc.StoreAsync(input, "test.txt", "game-1", CancellationToken.None);

        Assert.True(stored.Success);
        Assert.NotNull(stored.FileId);
        Assert.StartsWith("MOCK-", stored.FileId);

        using var retrieved = await svc.RetrieveAsync(stored.FileId!, "game-1", CancellationToken.None);
        Assert.NotNull(retrieved);
        using var ms = new MemoryStream();
        await retrieved!.CopyToAsync(ms, CancellationToken.None);
        Assert.Equal(content, ms.ToArray());
    }

    [Fact]
    public async Task Retrieve_ReturnsNullWhenNotFound()
    {
        var svc = new MockBlobStorageService();
        var result = await svc.RetrieveAsync("MOCK-none", "game-1", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_RemovesBlob()
    {
        var svc = new MockBlobStorageService();
        using var input = new MemoryStream(new byte[] { 1, 2, 3 });
        var stored = await svc.StoreAsync(input, "x.bin", "g", CancellationToken.None);

        Assert.True(await svc.DeleteAsync(stored.FileId!, "g", CancellationToken.None));
        Assert.False(await svc.ExistsAsync(stored.FileId!, "g", CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ReturnsFalseWhenNotFound()
    {
        var svc = new MockBlobStorageService();
        var result = await svc.DeleteAsync("MOCK-nonexistent", "g", CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task Exists_ReturnsTrueAfterStore()
    {
        var svc = new MockBlobStorageService();
        using var input = new MemoryStream(new byte[] { 42 });
        var stored = await svc.StoreAsync(input, "a.bin", "g", CancellationToken.None);
        Assert.True(await svc.ExistsAsync(stored.FileId!, "g", CancellationToken.None));
    }

    [Fact]
    public async Task GetPresignedUrl_ReturnsMockUrl()
    {
        var svc = new MockBlobStorageService();
        using var input = new MemoryStream(new byte[] { 1 });
        var stored = await svc.StoreAsync(input, "x.bin", "g", CancellationToken.None);
        var url = await svc.GetPresignedDownloadUrlAsync(stored.FileId!, "g", 7200);
        Assert.NotNull(url);
        Assert.Contains("mock-s3", url!);
        Assert.Contains("7200", url!);
    }

    [Fact]
    public async Task GetPresignedUrl_ReturnsNullWhenNotFound()
    {
        var svc = new MockBlobStorageService();
        var url = await svc.GetPresignedDownloadUrlAsync("MOCK-none", "g");
        Assert.Null(url);
    }

    [Fact]
    public void GetStoragePath_ReturnsMockPath()
    {
        var svc = new MockBlobStorageService();
        var path = svc.GetStoragePath("file-id", "game-1", "doc.pdf");
        Assert.StartsWith("mock://", path);
        Assert.Contains("game-1", path);
        Assert.Contains("file-id", path);
    }

    [Fact]
    public async Task Store_FileSizeMatchesContent()
    {
        var svc = new MockBlobStorageService();
        var content = new byte[256];
        using var input = new MemoryStream(content);
        var stored = await svc.StoreAsync(input, "big.bin", "g", CancellationToken.None);
        Assert.Equal(256L, stored.FileSizeBytes);
    }
}
