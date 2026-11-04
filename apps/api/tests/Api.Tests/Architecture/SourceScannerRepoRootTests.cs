using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
public sealed class SourceScannerRepoRootTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "repo-root-" + Guid.NewGuid().ToString("N"));

    public SourceScannerRepoRootTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void IsRepoRoot_GitDirectory_IsTrue()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        SourceScanner.IsRepoRoot(_root).Should().BeTrue();
    }

    [Fact]
    public void IsRepoRoot_GitFile_IsTrue()
    {
        // Un worktree (`git worktree add`) ha `.git` come FILE: "gitdir: <path>".
        File.WriteAllText(Path.Combine(_root, ".git"), "gitdir: D:/somewhere/.git/worktrees/x\n");

        SourceScanner.IsRepoRoot(_root).Should().BeTrue();
    }

    [Fact]
    public void IsRepoRoot_NoGit_IsFalse()
    {
        SourceScanner.IsRepoRoot(_root).Should().BeFalse();
    }
}
