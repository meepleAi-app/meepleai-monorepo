using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Evaluation;

/// <summary>
/// Issue #3438 — the sandbox that keeps the admin eval endpoints from reading, or writing, outside
/// the configured dataset root.
///
/// <para>
/// Before this, <c>datasetPath</c> reached <c>File.ReadAllTextAsync</c> unfiltered and
/// <c>outputPath</c> reached the writer: an admin session (or a stolen one, or a CSRF) could read
/// any file readable by the process and write an attacker-shaped JSON document anywhere writable.
/// Admin-gating narrows who can try; it does not make the capability acceptable.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3438")]
public sealed class EvalDatasetPathResolverTests : IDisposable
{
    private readonly string _root;

    public EvalDatasetPathResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"eval-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private EvalDatasetPathResolver CreateResolver(string? root) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [EvalDatasetPathResolver.RootConfigKey] = root
            })
            .Build());

    [Fact]
    public void RelativeJsonInsideRoot_Resolves()
    {
        var result = CreateResolver(_root).ResolveForRead("golden/catan.json");

        result.IsSuccess.Should().BeTrue();
        result.FullPath.Should().Be(Path.Combine(_root, "golden", "catan.json"));
    }

    [Theory]
    // Classic traversal, and one that only escapes after normalisation — the reason containment is
    // decided on the resolved path rather than by pattern-matching "..".
    [InlineData("../../../etc/passwd.json")]
    [InlineData("golden/../../../etc/passwd.json")]
    [InlineData("a/b/../../../outside.json")]
    public void TraversalOutsideRoot_IsRefused(string requested)
    {
        var result = CreateResolver(_root).ResolveForRead(requested);

        result.Error.Should().Be(EvalDatasetPathError.OutsideRoot);
        result.FullPath.Should().BeNull();
    }

    [Fact]
    public void AbsolutePath_IsRefused()
    {
        // Path.Combine(root, "/etc/passwd") returns "/etc/passwd" — the root is silently discarded.
        // Rooted input therefore has to be rejected before any combining happens.
        var absolute = Path.Combine(Path.GetTempPath(), "elsewhere.json");

        var result = CreateResolver(_root).ResolveForRead(absolute);

        result.Error.Should().Be(EvalDatasetPathError.OutsideRoot);
    }

    [Fact]
    public void SiblingDirectorySharingTheRootPrefix_IsRefused()
    {
        // "…/eval-root-XYZevil/x.json" starts with the root STRING but is a different directory.
        // Guarded by normalising the root with a trailing separator.
        var result = CreateResolver(_root).ResolveForRead($"../{Path.GetFileName(_root)}evil/x.json");

        result.Error.Should().Be(EvalDatasetPathError.OutsideRoot);
    }

    [Theory]
    [InlineData("appsettings.Production")]
    [InlineData("secrets.env")]
    [InlineData("dataset.json.bak")]
    public void NonJsonExtension_IsRefused(string requested)
    {
        var result = CreateResolver(_root).ResolveForRead(requested);

        result.Error.Should().Be(EvalDatasetPathError.NotJson);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPath_IsRefused(string? requested)
    {
        CreateResolver(_root).ResolveForRead(requested).Error.Should().Be(EvalDatasetPathError.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithoutAConfiguredRoot_EverythingIsRefused(string? root)
    {
        // Fail-closed. Guessing a default would hand back an implicit sandbox nobody chose, and one
        // that silently differs between environments.
        var resolver = CreateResolver(root);

        resolver.ResolveForRead("golden/catan.json").Error.Should().Be(EvalDatasetPathError.RootNotConfigured);
        resolver.ResolveForWrite("golden/catan.json").Error.Should().Be(EvalDatasetPathError.RootNotConfigured);
    }

    [Fact]
    public void ResolveForWrite_TargetNeedNotExist()
    {
        // merge-labels legitimately writes a brand-new labelled dataset.
        var result = CreateResolver(_root).ResolveForWrite("labelled/new-output.json");

        result.IsSuccess.Should().BeTrue();
        File.Exists(result.FullPath).Should().BeFalse();
    }

    [Fact]
    public void ResolveForWrite_AppliesTheSameContainmentAsRead()
    {
        // The write side is the more dangerous of the two: it must not be laxer.
        CreateResolver(_root).ResolveForWrite("../../evil.json").Error
            .Should().Be(EvalDatasetPathError.OutsideRoot);
    }

    [Fact]
    public void ResolveForRead_DoesNotProbeTheFileSystem()
    {
        // Existence is the endpoint's business. If the resolver reported it, a caller could use
        // refusals-vs-404s to probe for files outside the root — the very leak being closed.
        var result = CreateResolver(_root).ResolveForRead("definitely-missing.json");

        result.IsSuccess.Should().BeTrue();
        File.Exists(result.FullPath).Should().BeFalse();
    }

    [Fact]
    public void EnvAlias_IsAccepted()
    {
        var resolver = new EvalDatasetPathResolver(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [EvalDatasetPathResolver.RootEnvAlias] = _root
            })
            .Build());

        resolver.ResolveForRead("catan.json").IsSuccess.Should().BeTrue();
    }
}
