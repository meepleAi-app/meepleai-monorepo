using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// #4754: the four game night emails write the time on the night's own zone, name that zone, speak the
/// organizer's language, and do not tell a guest — an address, not an account — that they have an account.
///
/// Observed before the fix, reading the invitation an Italian organizer's guest receives: a night set for
/// 21:00 in Rome went out as "When Friday, October 30, 2026 at 8:00 PM" (UTC, unlabelled), in English, under
/// "You received this email because you have a MeepleAI account."
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightEmailServiceTests
{
    // 21:00 in Rome on 2026-10-30. Daylight saving ended on 2026-10-25, so the offset that day is +1.
    private static readonly DateTimeOffset RomeNineOclock = new(2026, 10, 30, 20, 0, 0, TimeSpan.Zero);

    private readonly Mock<IEmailService> _emailMock = new();
    private readonly Mock<ILogger<GameNightEmailService>> _loggerMock = new();
    private readonly GameNightEmailService _sut;

    private string _subject = string.Empty;
    private string _body = string.Empty;

    public GameNightEmailServiceTests()
    {
        _emailMock
            .Setup(e => e.SendRawEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, subject, body, _) =>
            {
                _subject = subject;
                _body = body;
            })
            .Returns(Task.CompletedTask);

        _sut = new GameNightEmailService(_emailMock.Object, _loggerMock.Object);
    }

    private Task SendInvitation(
        GameNightEmailLocale locale,
        DateTimeOffset? at = null,
        string organizer = "Marco",
        string title = "Venerdì Catan") =>
        _sut.SendGameNightInvitationEmailAsync(
            "guest@example.com", organizer, title, at ?? RomeNineOclock, location: "Casa mia",
            gameNames: ["Catan"], "https://meepleai.test/invites/t?action=accept",
            "https://meepleai.test/invites/t?action=decline", locale);

    // ── The time ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invitation_InTheNightsZone_WritesTheLocalTimeAndNamesTheZone()
    {
        await SendInvitation(new GameNightEmailLocale("en", "Europe/Rome"));

        _body.Should().Contain("Friday, October 30, 2026 at 9:00 PM (Europe/Rome, UTC+1)");
        _body.Should().NotContain("8:00 PM", "that was the UTC reading, written as if it were local");
    }

    [Theory]
    // Summer time in Rome: the offset is looked up for the night's own date, not the zone's standard offset.
    [InlineData("Europe/Rome", 2026, 7, 10, 19, 0, "21:00", "Europe/Rome, UTC+2")]
    // A half-hour offset keeps its minutes.
    [InlineData("Asia/Kolkata", 2026, 10, 30, 15, 30, "21:00", "Asia/Kolkata, UTC+5:30")]
    // West of Greenwich: the sign is part of the label.
    [InlineData("America/New_York", 2026, 10, 30, 20, 0, "16:00", "America/New_York, UTC-4")]
    public async Task Invitation_ItalianTime_FollowsTheZonesOffsetOnTheNightsDate(
        string zone, int year, int month, int day, int hourUtc, int minuteUtc, string localTime, string label)
    {
        var at = new DateTimeOffset(year, month, day, hourUtc, minuteUtc, 0, TimeSpan.Zero);

        await SendInvitation(new GameNightEmailLocale("it", zone), at);

        _body.Should().Contain($"alle {localTime} ({label})");
    }

    [Fact]
    public async Task Invitation_WithoutAZone_SaysUtcOutLoud()
    {
        await SendInvitation(new GameNightEmailLocale("en", TimeZoneId: null));

        _body.Should().Contain("Friday, October 30, 2026 at 8:00 PM (UTC)");
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("not a zone")]
    public async Task Invitation_WithAZoneThisHostCannotResolve_FallsBackToUtcWithoutThrowing(string zone)
    {
        var act = () => SendInvitation(new GameNightEmailLocale("it", zone));

        await act.Should().NotThrowAsync("an unusable zone must not stop an invitation from going out");
        _body.Should().Contain("alle 20:00 (UTC)");
        _emailMock.Verify(
            e => e.SendRawEmailAsync(
                "guest@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("could not be resolved", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once,
            "the fallback is a configuration problem or a client sending something odd: someone must be able to find it");
    }

    [Fact]
    public async Task Invitation_WithAKnownZoneAndNoWarning_LogsNoFallback()
    {
        await SendInvitation(new GameNightEmailLocale("en", "Europe/Rome"));

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Never);
    }

    // ── The language ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Invitation_Italian_WritesItalianTextAndItalianDate()
    {
        await SendInvitation(new GameNightEmailLocale("it", "Europe/Rome"), organizer: "Marco");

        _subject.Should().Be("Sei invitato: Venerdì Catan - MeepleAI");
        _body.Should().Contain("<html lang=\"it\">");
        _body.Should().Contain("Invito a una serata di gioco");
        _body.Should().Contain("Marco ti ha invitato a una serata di gioco!");
        _body.Should().Contain("venerdì 30 ottobre 2026 alle 21:00 (Europe/Rome, UTC+1)");
        _body.Should().Contain(">Accetta</a>").And.Contain(">Rifiuta</a>");
        _body.Should().NotContain("Game Night Invitation");
    }

    [Theory]
    [InlineData("it")]
    [InlineData("it-IT")]
    [InlineData("IT")]
    public async Task Invitation_AnItalianLanguageCode_IsItalian(string language)
    {
        await SendInvitation(new GameNightEmailLocale(language, "Europe/Rome"));

        _subject.Should().StartWith("Sei invitato:");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("itx")]
    public async Task Invitation_AnyOtherLanguageOrNone_IsEnglish(string? language)
    {
        await SendInvitation(new GameNightEmailLocale(language, "Europe/Rome"));

        _subject.Should().Be("You're Invited: Venerdì Catan - MeepleAI");
        _body.Should().Contain("<html lang=\"en\">");
    }

    [Fact]
    public async Task Invitation_EncodesTheOrganizersNameInTheSentenceItSitsIn()
    {
        await SendInvitation(new GameNightEmailLocale("en", "Europe/Rome"), organizer: "Mario & <Co>");

        _body.Should().Contain("Mario &amp; &lt;Co&gt; has invited you");
        _body.Should().NotContain("<Co>");
    }

    // ── The footer ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("en", "You received this email because Marco invited you to a game night on MeepleAI")]
    [InlineData("it", "Hai ricevuto questa email perché Marco ti ha invitato a una serata di gioco su MeepleAI")]
    public async Task Invitation_ToAGuest_DoesNotClaimAnAccountNorOfferPreferences(string language, string reason)
    {
        await SendInvitation(new GameNightEmailLocale(language, "Europe/Rome"));

        _body.Should().Contain(reason);
        _body.Should().NotContain("MeepleAI account").And.NotContain("account MeepleAI");
        _body.Should().NotContain("notification preferences").And.NotContain("preferenze di notifica");
        _body.Should().NotContain("unsubscribe", "the link it pointed at is not a page, and a guest has no profile");
    }

    [Fact]
    public async Task RsvpConfirmation_ToAGuest_WritesTheLocalTimeAndHasNoAccountNorLink()
    {
        await _sut.SendGameNightRsvpConfirmationEmailAsync(
            "guest@example.com", "Venerdì Catan", RomeNineOclock, "Casa mia",
            new GameNightEmailLocale("it", "Europe/Rome"));

        _subject.Should().Be("Partecipazione confermata: Venerdì Catan - MeepleAI");
        _body.Should().Contain("Ci sei!");
        _body.Should().Contain("alle 21:00 (Europe/Rome, UTC+1)");
        _body.Should().Contain("perché hai risposto a un invito a una serata di gioco su MeepleAI");
        _body.Should().NotContain("account MeepleAI");
        _body.Should().NotContain("unsubscribe").And.NotContain("preferenze di notifica");
    }

    [Fact]
    public async Task Reminder_ToAMember_WritesTheLocalTimeAndKeepsTheAccountFooterWithItsLink()
    {
        await _sut.SendGameNightReminder24hEmailAsync(
            "member@example.com", "Friday Catan", RomeNineOclock, "My place",
            confirmedCount: 3, isPending: true, "https://meepleai.test/unsub?t=1",
            new GameNightEmailLocale("en", "Europe/Rome"));

        _subject.Should().Be("Reminder: Friday Catan is tomorrow! - MeepleAI");
        _body.Should().Contain("When: <strong>Friday, October 30, 2026 at 9:00 PM (Europe/Rome, UTC+1)</strong>");
        _body.Should().Contain("You haven't responded yet. Will you be joining?");
        _body.Should().Contain("You received this email because you have a MeepleAI account.");
        _body.Should().Contain("href=\"https://meepleai.test/unsub?t=1\"");
        _body.Should().Contain("Manage notification preferences");
    }

    [Fact]
    public async Task Cancelled_ToAMember_InItalian_WritesTheLocalTimeAndTheItalianFooter()
    {
        await _sut.SendGameNightCancelledEmailAsync(
            "member@example.com", "Marco", "Venerdì Catan", RomeNineOclock,
            "https://meepleai.test/unsub?t=1", new GameNightEmailLocale("it", "Europe/Rome"));

        _subject.Should().Be("Annullata: Venerdì Catan - MeepleAI");
        _body.Should().Contain("Era prevista per: <strong>venerdì 30 ottobre 2026 alle 21:00 (Europe/Rome, UTC+1)</strong>");
        _body.Should().Contain("Hai ricevuto questa email perché hai un account MeepleAI.");
        _body.Should().Contain("Gestisci le preferenze di notifica");
    }

    [Fact]
    public async Task Reminder_WithoutAZone_SaysUtcOutLoud()
    {
        await _sut.SendGameNightReminder24hEmailAsync(
            "member@example.com", "Friday Catan", RomeNineOclock, location: null,
            confirmedCount: 0, isPending: false, "https://meepleai.test/unsub?t=1",
            GameNightEmailLocale.Unknown);

        _body.Should().Contain("Friday, October 30, 2026 at 8:00 PM (UTC)");
    }
}
