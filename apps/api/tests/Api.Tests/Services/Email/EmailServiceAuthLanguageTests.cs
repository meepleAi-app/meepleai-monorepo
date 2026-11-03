using System.Globalization;
using System.Text.RegularExpressions;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// #4812. The account emails — password reset, email verification, two-factor disabled, suspended,
/// reactivated, locked — were written in English in the code, whatever language the user they went to used:
/// only the game night invitation followed a language (#4754), and that is the organizer's.
/// </summary>
/// <remarks>
/// <para>
/// These go through the real <see cref="System.Net.Mail.SmtpClient"/> to a loopback <see cref="FakeSmtpServer"/>,
/// not through a builder: <c>EmailService</c> builds the client by hand for these six, so the subject and
/// body a recipient reads exist only on the wire. A test at the builder would pass with the language never
/// handed from the send method to it.
/// </para>
/// <para>
/// "In Italian" is checked three ways, because each alone passes a half-translated email: the exact subject,
/// distinctive sentences from the heading, the body and the footer, and the absence of English function words
/// in what is left once the markup is stripped. Whether English is "unchanged" is pinned the same way
/// (exact subject and the sentences), against the text as it was before the change.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "4812")]
public sealed class EmailServiceAuthLanguageTests
{
    private const string BaseUrl = "https://app.meepleai.test";
    private const string Reason = "Spam nei commenti";

    // The language part decides: case, region and separator do not matter, and surrounding spaces are ignored.
    private static readonly string?[] ItalianTags = ["it", "it-IT", "IT", "it_IT", " it "];

    // Everything else is English: unset, empty, another supported language, a language nothing is translated
    // into, and values that merely start like "it".
    private static readonly string?[] OtherTags = [null, "", "   ", "en", "en-GB", "fr", "de", "xx", "ita", "italian"];

    private static readonly Regex Markup = new("<[^>]+>|&[a-zA-Z]+;", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    // Words that exist in English and not in Italian. Not a dictionary: a tripwire for a sentence left untranslated.
    private static readonly Regex EnglishWord = new(
        @"\b(the|your|you|please|have|has|been|will|this|for|and|with|are|is|if|not|can|now|to|of|our|we)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(2));

    private sealed record AuthEmail(
        string Name,
        Func<EmailService, string?, Task> Send,
        string EnglishSubject,
        string ItalianSubject,
        string[] EnglishPhrases,
        string[] ItalianPhrases);

    private static readonly AuthEmail[] Cases =
    [
        new(
            "reset",
            (s, lang) => s.SendPasswordResetEmailAsync("marco@example.test", "Marco", "tok-reset", lang, Ct),
            "Reset Your MeepleAI Password",
            "Reimposta la tua password di MeepleAI",
            [
                "Password Reset Request", "We received a request to reset your password", "Reset Password",
                "will expire in 30 minutes", $"{BaseUrl}/reset-password?token=tok-reset",
                "This is an automated message, please do not reply to this email.",
            ],
            [
                "Richiesta di reimpostazione della password", "Abbiamo ricevuto una richiesta di reimpostazione", "Reimposta la password",
                "scade tra 30 minuti", $"{BaseUrl}/reset-password?token=tok-reset",
                "Questo è un messaggio automatico, non rispondere a questa email.",
            ]),
        new(
            "verify",
            (s, lang) => s.SendVerificationEmailAsync("marco@example.test", "Marco", "tok-verify", lang, Ct),
            "Verify Your MeepleAI Email",
            "Verifica la tua email di MeepleAI",
            [
                "Welcome to MeepleAI!", "Thank you for registering with MeepleAI", "Verify Email Address",
                "will expire in 24 hours", $"{BaseUrl}/verify-email?token=tok-verify",
                "This is an automated message, please do not reply to this email.",
            ],
            [
                "Benvenuto su MeepleAI!", "Grazie per esserti registrato su MeepleAI", "Verifica l'indirizzo email",
                "scade tra 24 ore", $"{BaseUrl}/verify-email?token=tok-verify",
                "Questo è un messaggio automatico, non rispondere a questa email.",
            ]),
        new(
            "two-factor-by-user",
            (s, lang) => s.SendTwoFactorDisabledEmailAsync("marco@example.test", "Marco", false, lang, Ct),
            "Two-Factor Authentication Disabled",
            "Autenticazione a due fattori disattivata",
            [
                "Security Alert", "Two-factor authentication has been disabled on your account.",
                "If you did not make this change", "To re-enable two-factor authentication:",
                "Go to Settings &gt; Privacy &gt; Two-Factor Authentication",
                "This is an automated security notification, please do not reply to this email.",
            ],
            [
                "Avviso di sicurezza", "L'autenticazione a due fattori è stata disattivata sul tuo account.",
                "Se non hai fatto tu questa modifica", "Per riattivare l'autenticazione a due fattori:",
                "Vai su Impostazioni &gt; Privacy &gt; Autenticazione a due fattori",
                "Questa è una notifica di sicurezza automatica, non rispondere a questa email.",
            ]),
        new(
            "two-factor-by-admin",
            (s, lang) => s.SendTwoFactorDisabledEmailAsync("marco@example.test", "Marco", true, lang, Ct),
            "Two-Factor Authentication Disabled",
            "Autenticazione a due fattori disattivata",
            [
                "Security Alert", "An administrator has disabled two-factor authentication on your account",
                "If you did not request this change", "Follow the setup instructions",
            ],
            [
                "Avviso di sicurezza", "Un amministratore ha disattivato l'autenticazione a due fattori sul tuo account",
                "Se non hai richiesto questa modifica", "Segui le istruzioni di configurazione",
            ]),
        new(
            "suspended-with-reason",
            (s, lang) => s.SendAccountSuspendedEmailAsync("marco@example.test", "Marco", Reason, lang, Ct),
            "Your MeepleAI Account Has Been Suspended",
            "Il tuo account MeepleAI è stato sospeso",
            [
                "Account Suspended", "has been suspended by an administrator", "Reason for suspension:", Reason,
                "Contact Support", "Thank you for your understanding.",
            ],
            [
                "Account sospeso", "è stato sospeso da un amministratore", "Motivo della sospensione:", Reason,
                "Contatta il supporto", "Grazie per la comprensione.",
            ]),
        new(
            "suspended-without-reason",
            (s, lang) => s.SendAccountSuspendedEmailAsync("marco@example.test", "Marco", null, lang, Ct),
            "Your MeepleAI Account Has Been Suspended",
            "Il tuo account MeepleAI è stato sospeso",
            ["Account Suspended", "has been suspended by an administrator", "Contact Support"],
            ["Account sospeso", "è stato sospeso da un amministratore", "Contatta il supporto"]),
        new(
            "reactivated",
            (s, lang) => s.SendAccountReactivatedEmailAsync("marco@example.test", "Marco", lang, Ct),
            "Your MeepleAI Account Has Been Reactivated",
            "Il tuo account MeepleAI è stato riattivato",
            [
                "Account Reactivated", "Welcome back! Your account is now active", "Next Steps:",
                "Review our community guidelines", "Thank you for being part of the MeepleAI community!",
            ],
            [
                "Account riattivato", "Bentornato! Il tuo account è di nuovo attivo", "Prossimi passi:",
                "Rileggi le linee guida della community", "Grazie di far parte della community di MeepleAI!",
            ]),
        new(
            "locked-with-ip",
            (s, lang) => s.SendAccountLockedEmailAsync("marco@example.test", "Marco", 5, DateTime.UtcNow.AddMinutes(14.5), "203.0.113.7", lang, Ct),
            "Security Alert: Your MeepleAI Account Has Been Locked",
            "Avviso di sicurezza: il tuo account MeepleAI è stato bloccato",
            [
                "Account Temporarily Locked", "5 consecutive failed login attempts", "Account locked for: 15 minutes",
                "IP Address: 203.0.113.7", "Wait for the lockout period to expire", "Review your recent account activity",
            ],
            [
                "Account bloccato temporaneamente", "5 tentativi di accesso consecutivi falliti", "Account bloccato per: 15 minuti",
                "Indirizzo IP: 203.0.113.7", "Aspetta che il blocco termini", "Controllare l'attività recente del tuo account",
            ]),
        new(
            "locked-without-ip",
            (s, lang) => s.SendAccountLockedEmailAsync("marco@example.test", "Marco", 5, DateTime.UtcNow.AddMinutes(14.5), null, lang, Ct),
            "Security Alert: Your MeepleAI Account Has Been Locked",
            "Avviso di sicurezza: il tuo account MeepleAI è stato bloccato",
            ["Account Temporarily Locked", "5 consecutive failed login attempts", "Make sure you're using the correct password"],
            ["Account bloccato temporaneamente", "5 tentativi di accesso consecutivi falliti", "Controlla di usare la password giusta"]),
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string?> ItalianRecipients() => Rows(ItalianTags);

    public static TheoryData<string, string?> OtherRecipients() => Rows(OtherTags);

    public static TheoryData<string, string?> OneOfEachLanguage() => Rows(["it", null]);

    private static TheoryData<string, string?> Rows(IEnumerable<string?> tags)
    {
        var rows = new TheoryData<string, string?>();
        foreach (var email in Cases)
        {
            foreach (var tag in tags)
                rows.Add(email.Name, tag);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(ItalianRecipients))]
    public async Task A_recipient_whose_language_is_Italian_gets_the_email_in_Italian(string name, string? language)
    {
        var email = Cases.Single(c => c.Name == name);

        var mail = await SendAsync(service => email.Send(service, language));

        mail.Subject.Should().Be(email.ItalianSubject);
        mail.Body.Should().Contain("Ciao Marco,");
        foreach (var phrase in email.ItalianPhrases)
            mail.Body.Should().Contain(phrase);

        EnglishWordsIn(mail.Body).Should().BeEmpty("nothing of an Italian email may be left in English");
    }

    [Theory]
    [MemberData(nameof(OtherRecipients))]
    public async Task A_recipient_with_any_other_or_no_language_gets_the_English_email_as_before(string name, string? language)
    {
        var email = Cases.Single(c => c.Name == name);

        var mail = await SendAsync(service => email.Send(service, language));

        mail.Subject.Should().Be(email.EnglishSubject);
        mail.Body.Should().Contain("Hello Marco,");
        foreach (var phrase in email.EnglishPhrases)
            mail.Body.Should().Contain(phrase);

        mail.Body.Should().NotContain(email.ItalianSubject);
        mail.Body.Should().NotContain("Ciao Marco,");
    }

    [Theory]
    [MemberData(nameof(OneOfEachLanguage))]
    public async Task The_document_declares_the_language_it_is_written_in(string name, string? language)
    {
        var email = Cases.Single(c => c.Name == name);

        var mail = await SendAsync(service => email.Send(service, language));

        mail.Body.Should().Contain(language is null ? "<html lang=\"en\">" : "<html lang=\"it\">");
    }

    [Theory]
    [MemberData(nameof(OneOfEachLanguage))]
    public async Task The_footer_shows_the_year_of_the_clock_not_a_literal(string name, string? language)
    {
        var email = Cases.Single(c => c.Name == name);

        var mail = await SendAsync(service => email.Send(service, language), new DateTimeOffset(2031, 3, 4, 0, 0, 0, TimeSpan.Zero));

        mail.Body.Should().Contain("&copy; 2031 MeepleAI");
        mail.Body.Should().NotContain("2025", "the year used to be a literal in the template");
    }

    [Fact]
    public async Task The_Italian_lock_duration_says_one_minute_in_the_singular()
    {
        var mail = await SendAsync(service => service.SendAccountLockedEmailAsync(
            "marco@example.test", "Marco", 5, DateTime.UtcNow.AddSeconds(50), null, "it", Ct));

        mail.Body.Should().Contain("Account bloccato per: 1 minuto<");
    }

    [Theory]
    [InlineData("it", "Si sblocca alle: 07/03/2030 14:05 UTC")]
    [InlineData("en", "Unlocks at: 2030-03-07 14:05 UTC")]
    public async Task The_unlock_time_is_written_the_way_the_recipients_language_writes_dates(string language, string expected)
    {
        var lockedUntil = new DateTime(2030, 3, 7, 14, 5, 0, DateTimeKind.Utc);

        var mail = await SendAsync(service => service.SendAccountLockedEmailAsync(
            "marco@example.test", "Marco", 5, lockedUntil, null, language, Ct));

        mail.Body.Should().Contain(expected);
    }

    [Theory]
    [InlineData("it", "Indirizzo IP")]
    [InlineData("en", "IP Address")]
    public async Task A_lockout_without_an_address_does_not_list_one(string language, string label)
    {
        var mail = await SendAsync(service => service.SendAccountLockedEmailAsync(
            "marco@example.test", "Marco", 5, DateTime.UtcNow.AddMinutes(14.5), "   ", language, Ct));

        mail.Body.Should().NotContain(label);
    }

    [Theory]
    [InlineData("it", "Motivo della sospensione:")]
    [InlineData("en", "Reason for suspension:")]
    public async Task A_suspension_without_a_reason_does_not_print_the_reason_heading(string language, string heading)
    {
        var mail = await SendAsync(service => service.SendAccountSuspendedEmailAsync(
            "marco@example.test", "Marco", "  ", language, Ct));

        mail.Body.Should().NotContain(heading);
    }

    [Theory]
    [InlineData("it", true, "Un amministratore ha disattivato", "è stata disattivata sul tuo account")]
    [InlineData("it", false, "è stata disattivata sul tuo account", "Un amministratore ha disattivato")]
    [InlineData("en", true, "An administrator has disabled", "has been disabled on your account")]
    [InlineData("en", false, "has been disabled on your account", "An administrator has disabled")]
    public async Task The_two_factor_reason_follows_who_turned_it_off(string language, bool byAdmin, string expected, string notExpected)
    {
        var mail = await SendAsync(service => service.SendTwoFactorDisabledEmailAsync(
            "marco@example.test", "Marco", byAdmin, language, Ct));

        mail.Body.Should().Contain(expected);
        mail.Body.Should().NotContain(notExpected);
    }

    private static async Task<ReceivedEmail> SendAsync(Func<EmailService, Task> send, DateTimeOffset? now = null)
    {
        await using var smtp = new FakeSmtpServer();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FromAddress"] = "noreply@meepleai.app",
                ["Email:FromName"] = "MeepleAI",
                ["Email:SmtpHost"] = "127.0.0.1",
                ["Email:SmtpPort"] = smtp.Port.ToString(CultureInfo.InvariantCulture),
                ["Email:EnableSsl"] = "false",
                ["Frontend:BaseUrl"] = BaseUrl,
            })
            .Build();
        var clock = new FakeTimeProvider(now ?? new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero));
        var service = new EmailService(configuration, NullLogger<EmailService>.Instance, sender: null, clock);

        await send(service);

        return smtp.Received.Should().ContainSingle().Subject;
    }

    private static IReadOnlyList<string> EnglishWordsIn(string html)
    {
        var text = Markup.Replace(html, " ");
        return [.. EnglishWord.Matches(text).Select(m => m.Value.ToLowerInvariant()).Distinct()];
    }
}
