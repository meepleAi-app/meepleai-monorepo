using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #5162 — the retrying execution strategy runs its delegate again after a transient failure, and a code path that
/// opens it by hand (<c>Database.CreateExecutionStrategy()</c>) found the tracker the failed attempt had left: rows
/// still <c>Added</c> after a failed save, changes already accepted after a failed commit. A backup code marked used
/// in a failed attempt stayed usable. The strategy goes through <c>RewindingExecution.ExecuteRewindingAsync</c>,
/// which starts every attempt from the tracker the caller handed in, or through one of the loops below, each of
/// which says why it needs no rewind.
/// </summary>
/// <remarks>
/// Text scan of the API source, comments left out. A new direct use has to go through the helper, or be added here
/// with its reason; an entry whose file no longer opens the strategy fails too, so the list does not outlive the code.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "5162")]
public sealed class ExecutionStrategyUsesTheRewindArchitectureTests
{
    private static readonly Regex OpensTheStrategy = new(
        @"\bDatabase\s*\.\s*CreateExecutionStrategy\s*\(", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    /// <summary>The files allowed to open the strategy by hand, and why the tracker of a failed attempt does not reach a retry.</summary>
    private static readonly IReadOnlyDictionary<string, string> Allowed = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SharedKernel/Infrastructure/Persistence/RewindingExecution.cs"] = "the helper itself",
        ["SharedKernel/Infrastructure/Persistence/EfCoreUnitOfWork.cs"] = "rewinds with ChangeTrackerRewind (#5145)",
        ["SharedKernel/Infrastructure/Persistence/UnitOfWork.cs"] = "rewinds with ChangeTrackerRewind (#5145)",
        ["BoundedContexts/Administration/Application/Behaviors/AuditLoggingBehavior.cs"] = "rewinds with ChangeTrackerRewind (#5145)",
        ["BoundedContexts/Administration/Infrastructure/BackgroundJobs/AuditOutboxProcessor.cs"] =
            "a scope of its own, ChangeTracker.Clear() at the start of every attempt",
        ["Infrastructure/BackgroundJobs/DomainEventOutboxProcessor.cs"] =
            "a scope of its own, ChangeTracker.Clear() and collector.Clear() at the start of every attempt",
        ["Infrastructure/BackgroundJobs/DomainEventOutboxRetentionService.cs"] =
            "ChangeTracker.Clear() at the start of every attempt, then a read without tracking and ExecuteDeleteAsync",
        ["BoundedContexts/DocumentProcessing/Infrastructure/Services/OrphanedProcessingJobRecoveryService.cs"] =
            "only ExecuteUpdateAsync: nothing goes through the tracker",
        ["BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicRecalcJobRepository.cs"] =
            "raw SQL read AsNoTracking: nothing goes through the tracker",
    };

    [Fact]
    public void TheStrategyIsOpenedByHandOnlyWhereARetryCannotFindAFailedAttemptsTracker()
    {
        var apiSrc = LocateApiSrc();
        var opening = Directory.EnumerateFiles(apiSrc, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => OpensTheStrategy.IsMatch(CodeOnly(File.ReadAllText(path))))
            .Select(path => Path.GetRelativePath(apiSrc, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        opening.Should().Contain("SharedKernel/Infrastructure/Persistence/EfCoreUnitOfWork.cs",
            "the scan must see the unit of work, or it sees nothing");
        opening.Except(Allowed.Keys).Should().BeEmpty(
            "a retried delegate must start from the caller's tracker: use MeepleAiDbContext.ExecuteRewindingAsync, "
            + "or add the file here with the reason a retry cannot find the failed attempt's rows (#5162)");
        Allowed.Keys.Except(opening).Should().BeEmpty("an entry whose file no longer opens the strategy goes");
    }

    /// <summary>The source without its comments, so that a comment naming the call is not a use of it.</summary>
    private static string CodeOnly(string source) =>
        string.Join('\n', source.Split('\n').Where(line =>
        {
            var trimmed = line.TrimStart();
            return !trimmed.StartsWith("//", StringComparison.Ordinal) && !trimmed.StartsWith('*');
        }));

    private static string LocateApiSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // In a git worktree `.git` is a file, not a directory: accepting both stops the climb at the right root.
        while (dir is not null)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                break;
            }

            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the tests run inside the repository");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}
