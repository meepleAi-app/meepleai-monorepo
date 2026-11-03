using System.Reflection;
using System.Text.RegularExpressions;
using Api.Services;
using Api.Services.Email;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// #4775. Every template of <c>EmailService</c> closed with «© 2025 MeepleAI»: the year was a literal in the
/// markup, so each email would say 2025 for as long as the code lived. #4754 fixed the verification email and
/// #4812 the other five account emails; this pins the rest — the two invitations, the access-request
/// rejection, and the admin, gamification, PDF, report and share-request emails — by pinning ALL of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why reflection over the builders.</b> The templates are private builders reached through ~25 send
/// methods with different dependencies. A test per builder is 25 tests that a new template silently escapes;
/// a test that discovers every <c>Build*EmailBody</c> catches the next one without being touched. A non-empty
/// list of known names guards against the discovery matching nothing.
/// </para>
/// <para>
/// <b>Time.</b> The clock is a <see cref="FakeTimeProvider"/> set far from the machine's own year, so a year
/// read from <see cref="DateTime.UtcNow"/> instead of the injected provider gives a different number, not by
/// luck the same one. The five account builders that take the year as an argument are given the same year:
/// how their send methods read it from the clock is pinned in <c>EmailServiceAuthLanguageTests</c>.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4775")]
public sealed class EmailServiceFooterYearTests
{
    private const int ClockYear = 2031;

    private static readonly DateTimeOffset Now = new(ClockYear, 5, 17, 9, 0, 0, TimeSpan.Zero);

    private static readonly Regex FooterYear = new(
        @"(?:&copy;|©)\s*(?<year>\d{4})",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(2));

    // One per family, so a discovery that matched nothing (a rename of the convention) cannot pass.
    private static readonly string[] KnownBuilders =
    [
        "BuildVerificationEmailBody",
        "BuildInvitationEmailBody",
        "BuildAccessRequestRejectedEmailBody",
        "BuildAdminShareRequestDigestEmailBody",
        "BuildBadgeEarnedEmailBody",
        "BuildPdfReadyEmailBody",
        "BuildReportEmailBody",
        "BuildShareRequestKbMergedEmailBody",
    ];

    private static EmailService NewService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FromAddress"] = "noreply@meepleai.app",
                ["Frontend:BaseUrl"] = "https://app.meepleai.test",
            })
            .Build();
        return new EmailService(
            configuration, NullLogger<EmailService>.Instance, sender: null, new FakeTimeProvider(Now));
    }

    private static List<MethodInfo> Builders() =>
        typeof(EmailService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name.StartsWith("Build", StringComparison.Ordinal)
                && m.Name.EndsWith("EmailBody", StringComparison.Ordinal)
                && m.ReturnType == typeof(string))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>Plain values for what a builder asks for. A type it does not know fails loudly, not silently.</summary>
    private static object?[] ArgumentsFor(MethodInfo builder) =>
        builder.GetParameters().Select(p => ArgumentFor(builder, p)).ToArray();

    private static object? ArgumentFor(MethodInfo builder, ParameterInfo parameter)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        if (type == typeof(int) && string.Equals(parameter.Name, "year", StringComparison.Ordinal))
        {
            return ClockYear;
        }

        if (type == typeof(string)) return "sample";
        if (type == typeof(int)) return 1;
        if (type == typeof(long)) return 1L;
        if (type == typeof(bool)) return false;
        if (type == typeof(Guid)) return Guid.NewGuid();
        if (type == typeof(DateTime)) return Now.UtcDateTime;
        if (type == typeof(Dictionary<string, int>)) return new Dictionary<string, int>();
        if (type == typeof(AuthEmailTexts)) return AuthEmailTexts.For(null);

        throw new NotSupportedException(
            $"{builder.Name}: no sample value for parameter '{parameter.Name}' of type {parameter.ParameterType}. " +
            "Teach ArgumentFor about it — do not skip the builder, or its footer goes unchecked.");
    }

    [Fact]
    public void TheDiscoveryFindsTheTemplates_SoThatTheCheckBelowIsNotVacuous()
    {
        Builders().Select(m => m.Name).Should().Contain(KnownBuilders);
    }

    [Fact]
    public void EveryTemplateFooterShowsTheYearOfTheInjectedClock()
    {
        var service = NewService();
        var problems = new List<string>();

        foreach (var builder in Builders())
        {
            var body = (string)builder.Invoke(builder.IsStatic ? null : service, ArgumentsFor(builder))!;
            var years = FooterYear.Matches(body).Select(m => m.Groups["year"].Value).ToList();

            if (years.Count == 0)
            {
                problems.Add($"{builder.Name}: no «© <year>» in the footer");
            }
            else if (years.Any(y => !string.Equals(y, ClockYear.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)))
            {
                problems.Add($"{builder.Name}: footer says {string.Join(", ", years)}, the clock says {ClockYear}");
            }

            body.Should().NotContain("{FooterYear}", $"{builder.Name}: the placeholder must be interpolated, not printed");
        }

        problems.Should().BeEmpty(
            "every template reads its footer year from the TimeProvider (or from the year its send method passes)");
    }
}
