using Api.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Infrastructure.Services;

/// <summary>
/// #5076: building the service must not touch the file system. Its constructor created the base directory, and the
/// API image runs as a user that cannot write /app: every request resolving it, approve-game-proposal among them,
/// failed with an UnauthorizedAccessException that the middleware answers as 403.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class LocalStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"local-storage-{Guid.NewGuid():N}");

    public LocalStorageServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static LocalStorageService Create(string basePath) => new(
        NullLogger<LocalStorageService>.Instance,
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:BasePath"] = basePath })
            .Build());

    [Fact]
    public void Constructor_WithABasePathThatCannotBeCreated_DoesNotThrow()
    {
        // A directory cannot be created under a regular file: on Linux and on Windows this is the same failure the
        // container had on /app.
        var file = Path.Combine(_root, "not-a-directory");
        File.WriteAllText(file, "x");
        var basePath = Path.Combine(file, "storage");

        var act = () => Create(basePath);

        act.Should().NotThrow();
        Directory.Exists(basePath).Should().BeFalse();
    }

    [Fact]
    public async Task CopyFile_CreatesTheDestinationDirectory_AndReturnsTheRelativePath()
    {
        var basePath = Path.Combine(_root, "storage");
        var service = Create(basePath);
        Directory.Exists(basePath).Should().BeFalse("the constructor no longer creates it");
        var source = Path.Combine(_root, "rules.pdf");
        await File.WriteAllTextAsync(source, "rules", TestContext.Current.CancellationToken);

        var copied = await service.CopyFile(
            source, Path.Combine("games", "g1", "rules.pdf"), TestContext.Current.CancellationToken);

        copied.Should().Be(Path.Combine("games", "g1", "rules.pdf"));
        (await File.ReadAllTextAsync(Path.Combine(basePath, copied), TestContext.Current.CancellationToken))
            .Should().Be("rules");
    }
}
