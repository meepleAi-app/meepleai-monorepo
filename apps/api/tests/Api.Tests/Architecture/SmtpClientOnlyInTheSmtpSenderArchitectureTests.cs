using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4845 — no code in the API builds an <c>SmtpClient</c> except <c>SmtpEmailSender</c>. Every other mail goes
/// through <c>IEmailSender</c>, so it follows <c>EMAIL_PROVIDER</c>, is counted when it fails, and can be replaced in
/// a test. A method that builds its own client sends over whatever SMTP host the configuration names — which, with
/// <c>EMAIL_PROVIDER=resend</c> on a deployment where nobody configured SMTP, is the wrong place: that is how the
/// reset and verification emails of an account ended up outside the provider the runbook tells operators to set.
/// </summary>
/// <remarks>
/// The scan reads CODE: a comment or a string that names the type (this class does, and so does the documentation
/// of <c>SmtpEmailSender</c>) is not counted, which is the property of <see cref="SourceScanner"/> that makes a gate
/// like this usable. Limit, admitted: it sees the type name, so a client reached through another type (a library
/// that opens SMTP itself) is not seen; the gate is about the one type this code base used.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4845")]
public sealed class SmtpClientOnlyInTheSmtpSenderArchitectureTests
{
    private const string TheOneAllowedFile = "Services/Email/SmtpEmailSender.cs";

    [Fact]
    public void NoCodeOutsideTheSmtpSenderBuildsAnSmtpClient()
    {
        var src = LocateApiSrc();
        var offenders = new List<string>();
        foreach (var path in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(p => !IsBuildOutput(p)))
        {
            var relative = Path.GetRelativePath(src, path).Replace('\\', '/');
            if (string.Equals(relative, TheOneAllowedFile, StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            offenders.AddRange(SourceScanner.FindCodeOccurrences(text, "SmtpClient")
                .Select(offset => $"{relative}:{LineOf(text, offset)}"));
        }

        offenders.Should().BeEmpty(
            "send through IEmailSender (EmailService.SendViaTransportAsync), not through a client of your own. Found:\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void TheScanSeesTheTypeInCodeAndOnlyThere()
    {
        SourceScanner.FindCodeOccurrences("using var c = new SmtpClient(host, port);", "SmtpClient").Should().ContainSingle();
        SourceScanner.FindCodeOccurrences("// built an SmtpClient by hand", "SmtpClient").Should().BeEmpty();
        SourceScanner.FindCodeOccurrences("/// <see cref=\"SmtpClient\"/>", "SmtpClient").Should().BeEmpty();
        SourceScanner.FindCodeOccurrences("var s = \"SmtpClient\";", "SmtpClient").Should().BeEmpty();

        // The ways of naming the type that do not look like `new SmtpClient(`: an alias, and the full name.
        SourceScanner.FindCodeOccurrences("using Mail = System.Net.Mail.SmtpClient;", "SmtpClient").Should().ContainSingle();
        SourceScanner.FindCodeOccurrences("System.Net.Mail.SmtpClient c = new();", "SmtpClient").Should().ContainSingle();
    }

    [Fact]
    public void TheGateReallyLooksAtTheApi_AndTheOneAllowedFileStillBuildsTheClient()
    {
        var src = LocateApiSrc();

        var files = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(p => !IsBuildOutput(p)).ToList();
        files.Count.Should().BeGreaterThan(500, "the scan must cover the whole API, not an empty folder");

        var sender = File.ReadAllText(Path.Combine(src, "Services", "Email", "SmtpEmailSender.cs"));
        SourceScanner.FindCodeOccurrences(sender, "SmtpClient").Should().NotBeEmpty(
            "if the sender no longer builds the client, the exception is stale and the gate should be tightened");
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static int LineOf(string text, int offset) => text.AsSpan(0, offset).Count('\n') + 1;

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

        dir.Should().NotBeNull("the tests run inside the repository");
        var src = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(src).Should().BeTrue($"the API sources must be at {src}");
        return src;
    }
}
