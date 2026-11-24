using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #3994 — «is this caller an admin?» was written as an exact comparison with the admin role in several shapes
/// (<c>string.Equals(role, "Admin", …)</c>, <c>role == Role.Admin</c>, <c>UserRole.Admin.ToString()</c>, a switch arm
/// on <c>"admin"</c>), and every one of them treats a superadmin as a plain user. The same defect shipped four times
/// (#3842, #3873, #4003, #3994) because each fix corrected the sites it was shown and the shape stayed writable.
/// The decision of 2026-11-24 is that a superadmin counts as an admin everywhere: this test fails on a new comparison
/// that names the admin role without naming the superadmin.
/// </summary>
/// <remarks>
/// <para>
/// <b>What to write instead.</b> With a <c>Role</c>: <c>role.HasPermission(Role.Admin)</c>. With a raw string:
/// <c>Role.TryParse(value, out var role) &amp;&amp; role.HasPermission(Role.Admin)</c>, which fails closed on an absent or
/// unknown value. In an endpoint with a session: <c>session.RequireRole(UserRole.Admin).IsAuthorized</c>, which reads
/// the effective actor.
/// </para>
/// <para>
/// <b>Text scan, per statement.</b> Comments are removed and the source is cut at <c>;</c>, <c>{</c> and <c>}</c>. A
/// statement fails when it compares with the admin role and does not mention the superadmin anywhere: a statement that
/// names both (<c>role == "admin" || role == "superadmin"</c>, a switch with both arms) is left alone. The only
/// exemption is <c>Role.IsExactlyAdmin()</c>, the one predicate whose job is the exact identity.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "3994")]
public sealed class AdminRoleComparisonIncludesSuperAdminArchitectureTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private const string AdminRole = @"(?:""[Aa][Dd][Mm][Ii][Nn]""|UserRole\.Admin\.ToString\(\)|nameof\(UserRole\.Admin\))";

    // The enum and value-object constants, compared directly: UserRole.Admin, Role.Admin, Role.Admin.Value.
    private const string AdminConstant = @"\b(?:UserRole\.Admin\b(?!\.ToString)|Role\.Admin(?:\.Value)?\b)";

    // Call arguments, with up to two levels of nested parentheses.
    private const string Arguments = @"(?:[^;{}()]|\((?:[^()]|\([^()]*\))*\))*?";

    private static readonly Regex[] AdminComparisons =
    [
        // Equals(x, "admin", …) on any receiver — string., String., StringComparer.X., bare — and x.Equals("admin").
        // The literal must be an argument of the call, not a value next to it.
        new(@"\bEquals\(" + Arguments + "(?:" + AdminRole + "|" + AdminConstant + @")\s*[,)]", RegexOptions.None, Timeout),
        new(AdminRole + @"\s*\.Equals\(", RegexOptions.None, Timeout),
        new(@"[=!]=\s*(?:" + AdminRole + "|" + AdminConstant + ")", RegexOptions.None, Timeout),
        new("(?:" + AdminRole + "|" + AdminConstant + @")\s*[=!]=", RegexOptions.None, Timeout),
        // pattern matching: x is "admin", x is not UserRole.Admin
        new(@"\bis\s+(?:not\s+)?(?:" + AdminRole + "|" + AdminConstant + ")", RegexOptions.None, Timeout),
        // switch arms: "admin" => …, "admin" or …, case "admin":, UserRole.Admin => …
        new(AdminRole + @"\s*(?:=>|or\b)", RegexOptions.None, Timeout),
        new(@"\bcase\s+(?:" + AdminRole + "|" + AdminConstant + ")", RegexOptions.None, Timeout),
        new(@"\bor\s+(?:" + AdminRole + "|" + AdminConstant + ")", RegexOptions.None, Timeout),
        new(@"\bUserRole\.Admin\s*=>", RegexOptions.None, Timeout),
        // ASP.NET role checks: IsInRole("Admin"), RequireRole("Admin"), [Authorize(Roles = "Admin")]
        new(@"\b(?:IsInRole|RequireRole)\(\s*" + AdminRole, RegexOptions.None, Timeout),
        new(@"\bRoles\s*=\s*" + AdminRole, RegexOptions.None, Timeout),
    ];

    private static readonly Regex MentionsSuperAdmin = new("[Ss]uper[Aa]dmin", RegexOptions.None, Timeout);
    private static readonly Regex LineComment = new(@"//[^\n]*", RegexOptions.None, Timeout);
    private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Singleline, Timeout);

    private static readonly string ExemptFile = Path.Combine("SharedKernel", "Domain", "ValueObjects", "Role.cs");

    [Fact]
    public void NoAdminRoleComparison_LeavesTheSuperAdminOut()
    {
        var apiSrc = LocateApiSrc();
        var findings = Directory.EnumerateFiles(apiSrc, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGenerated(path))
            .SelectMany(path => Scan(Path.GetRelativePath(apiSrc, path), File.ReadAllText(path)))
            .ToList();

        findings.Should().Contain(
            f => f.File == ExemptFile && f.Statement.Contains("IsExactlyAdmin", StringComparison.Ordinal),
            "the scan must recognise the one exact comparison that is meant, or it is checking nothing");

        // Joined, so that the message lists every site and not only the first one.
        string.Join(Environment.NewLine, findings.Where(f => f.File != ExemptFile)).Should().BeEmpty(
            "a superadmin counts as an admin everywhere (#3994): use role.HasPermission(Role.Admin), "
            + "Role.TryParse(value, out var role) && role.HasPermission(Role.Admin), "
            + "or session.RequireRole(UserRole.Admin).IsAuthorized");
    }

    [Theory]
    [InlineData("if (string.Equals(user.Role, \"Admin\", StringComparison.OrdinalIgnoreCase)) { }")]
    [InlineData("var isAdmin = string.Equals(session!.Principal!.EffectiveActor.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);")]
    [InlineData("var isAdmin = role.Equals(\"admin\", StringComparison.OrdinalIgnoreCase);")]
    [InlineData("if (_options.AdminBypass && userRole == Role.Admin) { }")]
    [InlineData("var c = role switch { \"admin\" => 1, \"editor\" => 2, _ => 0 };")]
    [InlineData("if (role == UserRole.Admin) { }")]
    [InlineData("if (userRole.Equals(Role.Admin)) { }")]
    [InlineData("var isAdmin = user.Role.Value == Role.Admin.Value;")]
    [InlineData("var isAdmin = role is \"admin\";")]
    [InlineData("var notAdmin = role is not UserRole.Admin;")]
    [InlineData("var isAdmin = \"admin\".Equals(role, StringComparison.OrdinalIgnoreCase);")]
    [InlineData("var isAdmin = StringComparer.OrdinalIgnoreCase.Equals(role, \"Admin\");")]
    [InlineData("var isAdmin = String.Equals(role, \"admin\");")]
    [InlineData("var isAdmin = string.Equals(Normalize(user.Role.Trim()), \"admin\", StringComparison.Ordinal);")]
    [InlineData("var isAdmin = context.User.IsInRole(\"Admin\");")]
    [InlineData("[Authorize(Roles = \"Admin\")] public sealed class Sample { }")]
    public void Scan_FlagsAComparisonThatLeavesTheSuperAdminOut(string source)
    {
        Scan("Sample.cs", source).Should().ContainSingle();
    }

    [Theory]
    [InlineData("var isAdmin = role == \"admin\" || role == \"superadmin\";")]
    [InlineData("var c = role switch { \"admin\" or \"superadmin\" => 1, _ => 0 };")]
    [InlineData("var isAdmin = Role.TryParse(value, out var role) && role.HasPermission(Role.Admin);")]
    [InlineData("var name = string.Equals(a.DisplayName, a.Email, StringComparison.Ordinal) ? \"Admin\" : a.DisplayName;")]
    [InlineData("// if (string.Equals(role, \"admin\")) — the old form")]
    [InlineData("if (role == UserRole.Admin || role == UserRole.SuperAdmin) { }")]
    [InlineData("var isAdmin = session.RequireRole(UserRole.Admin).IsAuthorized;")]
    [InlineData("var isAdmin = context.User.IsInRole(\"Admin\") || context.User.IsInRole(\"SuperAdmin\");")]
    public void Scan_LeavesAloneWhatIsNotTheDefect(string source)
    {
        Scan("Sample.cs", source).Should().BeEmpty();
    }

    private static IEnumerable<Finding> Scan(string file, string source)
    {
        // Comments are blanked, newlines kept, so that line numbers still point at the source.
        var text = BlockComment.Replace(source, m => new string('\n', m.Value.Count(c => c == '\n')));
        text = LineComment.Replace(text, string.Empty);

        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] is not (';' or '{' or '}'))
            {
                continue;
            }

            var statement = text[start..i];
            if (AdminComparisons.Any(r => r.IsMatch(statement)) && !MentionsSuperAdmin.IsMatch(statement))
            {
                var firstChar = start + (statement.Length - statement.TrimStart().Length);
                var line = text.Take(firstChar).Count(c => c == '\n') + 1;
                yield return new Finding(file, line, string.Join(' ', statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
            }

            start = i + 1;
        }
    }

    private static bool IsGenerated(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
            || path.Contains($"{sep}bin{sep}", StringComparison.Ordinal)
            || path.Contains($"{sep}Migrations{sep}", StringComparison.Ordinal);
    }

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

    private sealed record Finding(string File, int Line, string Statement)
    {
        public override string ToString() => $"{File}:{Line}: {(Statement.Length > 160 ? Statement[..160] : Statement)}";
    }
}
