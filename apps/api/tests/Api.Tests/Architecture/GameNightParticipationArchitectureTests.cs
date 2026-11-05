using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4759: the rule «who sits at a game night's table» used to be written in line, eleven times,
/// as <c>OrganizerId == caller || Rsvps.Any(r => r.UserId == caller)</c>, and a registered user who
/// accepted an email invitation matched none of the copies. The rule now lives in
/// <c>GameNightParticipation</c> (the loaded aggregate) and <c>GameNightEventRepository.ParticipatedBy</c>
/// (EF). A handler that writes the RSVP-only form again would quietly bring the 403 back on that
/// one endpoint, with every mocked test still green (Moq answers <c>false</c> by default, so a
/// dropped call to the shared rule is invisible to them). Hence a closed list.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4759")]
public sealed class GameNightParticipationArchitectureTests
{
    private const string MarkerFile =
        "apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/GameNightParticipation.cs";

    /// <summary>
    /// Files allowed to test an RSVP row by user id, relative to <c>apps/api/src/Api/</c>. The first
    /// two ARE the rule. The third uses the RSVP rows for a purpose that is not access to one night —
    /// who counts as a friend — and whether email guests belong there is a separate decision, taken
    /// in neither direction here.
    /// </summary>
    private static readonly string[] AllowedFiles =
    [
        "BoundedContexts/GameManagement/Application/Services/GameNightParticipation.cs",
        "BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepository.cs",
        "BoundedContexts/GameManagement/Application/Queries/Dashboard/GetFriendsActivityQueryHandler.cs",
    ];

    [Fact]
    public void RsvpRowByUserId_IsTestedOnlyWhereTheRuleLives_OrForStatistics()
    {
        var apiRoot = LocateApiSourceRoot();

        var mentioning = Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("Rsvps.Any(r => r.UserId ==", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(apiRoot, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        mentioning.Should().NotBeEmpty("a scan that finds nothing would be a scan that checked nothing");
        mentioning.Should().BeEquivalentTo(
            AllowedFiles,
            "a read of ONE night decides who may see it with GameNightParticipation.IsParticipantAsync, "
            + "never with the RSVP rows alone: that form leaves out whoever accepted an email invitation (#4759)");
    }

    private static string LocateApiSourceRoot()
    {
        var marker = MarkerFile.Replace('/', Path.DirectorySeparatorChar);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, marker)))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        return Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
    }
}
