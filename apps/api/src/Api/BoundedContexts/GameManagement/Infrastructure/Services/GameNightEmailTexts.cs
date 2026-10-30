namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// The words of the four game night emails, in the organizer's language (#4754). Italian when the
/// organizer's language is Italian, English otherwise — English being what every template said before.
/// </summary>
/// <remarks>
/// Strings that carry a name or a title are functions, so the caller passes them through
/// <c>Encode</c> exactly once, at the point of use. Nothing here is HTML-encoded.
/// </remarks>
internal sealed class GameNightEmailTexts
{
    public required string HtmlLang { get; init; }
    public required bool IsItalian { get; init; }

    // Shared labels
    public required string WhenLabel { get; init; }
    public required string LocationLabel { get; init; }
    public required string GamesLabel { get; init; }

    // Invitation (a guest: reached by address, with no account implied)
    public required Func<string, string> InvitationSubject { get; init; }
    public required string InvitationHeading { get; init; }
    public required Func<string, string> InvitationIntro { get; init; }
    public required string AcceptLabel { get; init; }
    public required string DeclineLabel { get; init; }
    public required Func<string, string> InvitationGuestFooter { get; init; }

    // 24h reminder (a member: it reaches people who answered from their account)
    public required Func<string, string> ReminderSubject { get; init; }
    public required string ReminderHeading { get; init; }
    public required string ReminderIntro { get; init; }
    public required string ConfirmedAttendeesLabel { get; init; }
    public required string ReminderPendingPrompt { get; init; }

    // Cancellation
    public required Func<string, string> CancelledSubject { get; init; }
    public required string CancelledHeading { get; init; }
    public required Func<string, string> CancelledIntro { get; init; }
    public required string WasScheduledForLabel { get; init; }
    public required string CancelledClosing { get; init; }

    // RSVP confirmation (a guest again: the token flow, no account implied)
    public required Func<string, string> RsvpSubject { get; init; }
    public required string RsvpHeading { get; init; }
    public required string RsvpIntro { get; init; }
    public required string RsvpClosing { get; init; }
    public required string RsvpGuestFooter { get; init; }

    // Footer for the emails whose recipient does have a MeepleAI account
    public required string MemberFooter { get; init; }
    public required string ManagePreferencesLabel { get; init; }

    public static GameNightEmailTexts For(string? language) => IsItalianLanguage(language) ? Italian : English;

    /// <summary>"it", "it-IT", "IT_it": the language part decides; anything else is English.</summary>
    internal static bool IsItalianLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return false;

        var trimmed = language.Trim();
        var end = trimmed.IndexOfAny(['-', '_']);
        var primary = end < 0 ? trimmed : trimmed[..end];
        return string.Equals(primary, "it", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly GameNightEmailTexts English = new()
    {
        HtmlLang = "en",
        IsItalian = false,
        WhenLabel = "When",
        LocationLabel = "Location",
        GamesLabel = "Games",
        InvitationSubject = title => $"You're Invited: {title} - MeepleAI",
        InvitationHeading = "Game Night Invitation",
        InvitationIntro = organizer => $"{organizer} has invited you to a game night!",
        AcceptLabel = "Accept",
        DeclineLabel = "Decline",
        InvitationGuestFooter = organizer =>
            $"You received this email because {organizer} invited you to a game night on MeepleAI. If it's not for you, you can ignore it or decline the invitation.",
        ReminderSubject = title => $"Reminder: {title} is tomorrow! - MeepleAI",
        ReminderHeading = "Game Night Tomorrow!",
        ReminderIntro = "Your game night is coming up in 24 hours.",
        ConfirmedAttendeesLabel = "Confirmed attendees",
        ReminderPendingPrompt = "You haven't responded yet. Will you be joining?",
        CancelledSubject = title => $"Cancelled: {title} - MeepleAI",
        CancelledHeading = "Game Night Cancelled",
        CancelledIntro = organizer => $"{organizer} has cancelled the following game night.",
        WasScheduledForLabel = "Was scheduled for",
        CancelledClosing = "We hope to see you at the next one!",
        RsvpSubject = title => $"RSVP Confirmed: {title} - MeepleAI",
        RsvpHeading = "You're In!",
        RsvpIntro = "Your RSVP has been confirmed for the game night.",
        RsvpClosing = "See you there!",
        RsvpGuestFooter = "You received this email because you responded to a game night invitation on MeepleAI.",
        MemberFooter = "You received this email because you have a MeepleAI account.",
        ManagePreferencesLabel = "Manage notification preferences",
    };

    private static readonly GameNightEmailTexts Italian = new()
    {
        HtmlLang = "it",
        IsItalian = true,
        WhenLabel = "Quando",
        LocationLabel = "Dove",
        GamesLabel = "Giochi",
        InvitationSubject = title => $"Sei invitato: {title} - MeepleAI",
        InvitationHeading = "Invito a una serata di gioco",
        InvitationIntro = organizer => $"{organizer} ti ha invitato a una serata di gioco!",
        AcceptLabel = "Accetta",
        DeclineLabel = "Rifiuta",
        InvitationGuestFooter = organizer =>
            $"Hai ricevuto questa email perché {organizer} ti ha invitato a una serata di gioco su MeepleAI. Se non ti interessa, puoi ignorarla o rifiutare l'invito.",
        ReminderSubject = title => $"Promemoria: {title} è domani! - MeepleAI",
        ReminderHeading = "Serata di gioco domani!",
        ReminderIntro = "La tua serata di gioco è tra 24 ore.",
        ConfirmedAttendeesLabel = "Partecipanti confermati",
        ReminderPendingPrompt = "Non hai ancora risposto. Ci sarai?",
        CancelledSubject = title => $"Annullata: {title} - MeepleAI",
        CancelledHeading = "Serata di gioco annullata",
        CancelledIntro = organizer => $"{organizer} ha annullato questa serata di gioco.",
        WasScheduledForLabel = "Era prevista per",
        CancelledClosing = "Speriamo di vederti alla prossima!",
        RsvpSubject = title => $"Partecipazione confermata: {title} - MeepleAI",
        RsvpHeading = "Ci sei!",
        RsvpIntro = "La tua partecipazione alla serata di gioco è confermata.",
        RsvpClosing = "A presto!",
        RsvpGuestFooter = "Hai ricevuto questa email perché hai risposto a un invito a una serata di gioco su MeepleAI.",
        MemberFooter = "Hai ricevuto questa email perché hai un account MeepleAI.",
        ManagePreferencesLabel = "Gestisci le preferenze di notifica",
    };
}
