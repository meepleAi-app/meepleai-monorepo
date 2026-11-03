using Api.Services;
using Api.Services.Email;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// #4754. The verification email's footer said "© 2025" whatever the year: the number was a literal in the
/// template. The year is now an argument, which <c>SendVerificationEmailAsync</c> reads from the injected
/// <see cref="TimeProvider"/>.
/// </summary>
/// <remarks>
/// Pinned here at the builder. #4812 gave the other five account templates (reset, two-factor disabled,
/// suspended, reactivated, locked) the same argument, and pinned the wiring from every send method to the
/// <see cref="TimeProvider"/> end to end — through the real <c>SmtpClient</c> against a loopback server — in
/// <c>EmailServiceAuthLanguageTests</c>. This class keeps the builder's own contract.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4754")]
public sealed class EmailServiceVerificationYearTests
{
    [Theory]
    [InlineData(2026)]
    [InlineData(2031)]
    public void TheFooterShowsTheYearItIsGiven(int year)
    {
        var body = EmailService.BuildVerificationEmailBody(AuthEmailTexts.For(null), "Marco", "https://meepleai.test/verify-email?token=t", year);

        body.Should().Contain($"&copy; {year} MeepleAI");
        body.Should().NotContain("&copy; 2025", "the year used to be a literal");
    }

    [Fact]
    public void TheRestOfTheTemplateIsUnchanged()
    {
        var body = EmailService.BuildVerificationEmailBody(AuthEmailTexts.For(null), "Marco", "https://meepleai.test/verify-email?token=t", 2026);

        body.Should().Contain("Hello Marco,");
        body.Should().Contain("href=\"https://meepleai.test/verify-email?token=t\"");
        body.Should().Contain("Verify Email Address");
    }
}
