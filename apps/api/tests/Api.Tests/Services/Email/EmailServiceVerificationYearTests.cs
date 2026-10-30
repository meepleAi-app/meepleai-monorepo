using Api.Services;
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
/// Scope: only this template. The other auth/admin templates carry the same literal; they are separate
/// templates with their own builders and were not part of the report. Pinned here at the builder, not through
/// <c>SendVerificationEmailAsync</c>, because that method sends through <c>SmtpClient</c> directly and has no
/// transport seam to capture the body from — so that the year really comes from the <see cref="TimeProvider"/>
/// is a one-line wiring that no test here covers.
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
        var body = EmailService.BuildVerificationEmailBody("Marco", "https://meepleai.test/verify-email?token=t", year);

        body.Should().Contain($"&copy; {year} MeepleAI");
        body.Should().NotContain("&copy; 2025", "the year used to be a literal");
    }

    [Fact]
    public void TheRestOfTheTemplateIsUnchanged()
    {
        var body = EmailService.BuildVerificationEmailBody("Marco", "https://meepleai.test/verify-email?token=t", 2026);

        body.Should().Contain("Hello Marco,");
        body.Should().Contain("href=\"https://meepleai.test/verify-email?token=t\"");
        body.Should().Contain("Verify Email Address");
    }
}
