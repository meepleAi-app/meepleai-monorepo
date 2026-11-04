using System.Reflection;
using Api.Services;
using Api.Services.Email;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// #4775. An account registered without a name is given an opaque one (<c>Player-1a2b3c4</c>), and the email
/// verification used to open with «Hello Player-1a2b3c4,». The six account emails greet through
/// <see cref="AuthEmailTexts.Greeting"/>; this walks all of them, so a seventh that greets by hand shows up
/// as a missing name in the list instead of passing unnoticed.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4775")]
public sealed class EmailServiceGreetingTests
{
    private const string Generated = "Player-1a2b3c4";

    // One per email, so a discovery that matched nothing (a rename of the convention) cannot pass.
    private static readonly string[] KnownBuilders =
    [
        "BuildVerificationEmailBody",
        "BuildPasswordResetEmailBody",
        "BuildTwoFactorDisabledEmailBody",
        "BuildAccountSuspendedEmailBody",
        "BuildAccountReactivatedEmailBody",
        "BuildAccountLockedEmailBody",
    ];

    /// <summary>The account emails: a builder that takes the words and the name of the recipient.</summary>
    private static List<MethodInfo> AccountEmailBuilders() =>
        typeof(EmailService)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name.StartsWith("Build", StringComparison.Ordinal)
                && m.Name.EndsWith("EmailBody", StringComparison.Ordinal)
                && m.ReturnType == typeof(string)
                && m.GetParameters() is { Length: >= 2 } p
                && p[0].ParameterType == typeof(AuthEmailTexts)
                && string.Equals(p[1].Name, "userName", StringComparison.Ordinal))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

    private static string Render(MethodInfo builder, string language, string userName)
    {
        var arguments = builder.GetParameters().Select(p => ArgumentFor(builder, p, language, userName)).ToArray();
        return (string)builder.Invoke(null, arguments)!;
    }

    private static object? ArgumentFor(MethodInfo builder, ParameterInfo parameter, string language, string userName)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        if (type == typeof(AuthEmailTexts)) return AuthEmailTexts.For(language);
        if (type == typeof(string) && string.Equals(parameter.Name, "userName", StringComparison.Ordinal)) return userName;
        if (type == typeof(string)) return "sample";
        if (type == typeof(int)) return 2031;
        if (type == typeof(bool)) return false;
        if (type == typeof(DateTime)) return new DateTime(2031, 5, 17, 9, 0, 0, DateTimeKind.Utc);

        throw new NotSupportedException(
            $"{builder.Name}: no sample value for parameter '{parameter.Name}' of type {parameter.ParameterType}. " +
            "Teach ArgumentFor about it — do not skip the builder, or its greeting goes unchecked.");
    }

    [Fact]
    public void TheDiscoveryFindsTheSixAccountEmails_SoThatTheChecksBelowAreNotVacuous()
    {
        AccountEmailBuilders().Select(m => m.Name).Should().Contain(KnownBuilders);
    }

    [Theory]
    [InlineData("en", "Hello,")]
    [InlineData("it", "Ciao,")]
    public void EveryAccountEmail_GreetsAGeneratedNameWithoutIt(string language, string greeting)
    {
        var problems = new List<string>();

        foreach (var builder in AccountEmailBuilders())
        {
            var body = Render(builder, language, Generated);

            if (body.Contains("Player-", StringComparison.Ordinal))
                problems.Add($"{builder.Name} ({language}): the generated name is in the email");
            if (!body.Contains($"<p>{greeting}</p>", StringComparison.Ordinal))
                problems.Add($"{builder.Name} ({language}): no «{greeting}» paragraph");
        }

        problems.Should().BeEmpty("a person who gave no name is greeted without one, not with an opaque id");
    }

    [Theory]
    [InlineData("en", "Hello Marco,")]
    [InlineData("it", "Ciao Marco,")]
    public void EveryAccountEmail_StillGreetsAChosenNameByName(string language, string greeting)
    {
        var problems = new List<string>();

        foreach (var builder in AccountEmailBuilders())
        {
            var body = Render(builder, language, "Marco");

            if (!body.Contains($"<p>{greeting}</p>", StringComparison.Ordinal))
                problems.Add($"{builder.Name} ({language}): no «{greeting}» paragraph");
        }

        problems.Should().BeEmpty("the name a person chose is the name they are greeted by");
    }
}
