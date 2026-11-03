using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4775 — no email template carries a literal copyright year. The footer year comes from the injected
/// <c>TimeProvider</c>; a «© 2025» typed into the markup says 2025 for as long as the code lives, and it is
/// exactly what nobody notices (it was there from the first template until #4754).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a text scan and not <see cref="SourceScanner"/>.</b> The thing to find is inside a string literal —
/// the HTML — and the shared scanner deliberately skips literals. A plain regex over the text of the files of
/// <c>Services/Email</c> is the right tool here; the comments of those files do not write a year after a
/// copyright sign, and if one ever must, it can say «the year» in words.
/// </para>
/// <para>
/// <b>Limit, admitted.</b> This looks at <c>Services/Email</c> only. A template written elsewhere (the
/// notification templates of UserNotifications already read <c>DateTime.UtcNow.Year</c>) is not seen; and a
/// year assembled at run time from the wrong source is not a literal. The behavioural half —
/// <c>EmailServiceFooterYearTests</c> — runs every builder against an injected clock and catches that.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4775")]
public sealed class EmailFooterYearArchitectureTests
{
    // The sign (entity or character), optional spaces, then four digits: a year typed into the text.
    private static readonly Regex LiteralYear = new(
        @"(?:&copy;|©)\s*\d{4}", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    [Fact]
    public void NoEmailTemplateCarriesALiteralCopyrightYear()
    {
        var emailDir = Path.Combine(LocateApiSrc(), "Services", "Email");
        Directory.Exists(emailDir).Should().BeTrue($"the email services must live at {emailDir}");

        var offenders = Directory.EnumerateFiles(emailDir, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, index) => (path, line, number: index + 1))
                .Where(x => LiteralYear.IsMatch(x.line)))
            .Select(x => $"{Path.GetRelativePath(emailDir, x.path).Replace('\\', '/')}:{x.number}: {x.line.Trim()}")
            .ToList();

        offenders.Should().BeEmpty(
            "the footer year is read from the TimeProvider, not typed. Use the FooterYear of EmailService (or the " +
            "year argument of the account builders). Literal years found:\n" + string.Join('\n', offenders));
    }

    [Theory]
    [InlineData("<p>&copy; 2025 MeepleAI. All rights reserved.</p>")]
    [InlineData("<p>© 2031 MeepleAI</p>")]
    [InlineData("<p>&copy;2025 MeepleAI</p>")]
    public void TheScanSeesALiteralYear(string line)
    {
        LiteralYear.IsMatch(line).Should().BeTrue();
    }

    [Theory]
    [InlineData("<p>&copy; {FooterYear} MeepleAI. All rights reserved.</p>")]
    [InlineData("<p>&copy; {year} MeepleAI</p>")]
    [InlineData("// the footer year was the literal 2025")]
    public void TheScanLetsTheInterpolatedFormAndPlainCommentsThrough(string line)
    {
        LiteralYear.IsMatch(line).Should().BeFalse();
    }

    private static string LocateApiSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // In a git worktree `.git` is a file, not a directory: accepting both stops the climb at the right root
        // instead of going on to the main checkout.
        while (dir is not null)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                break;
            }

            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}
