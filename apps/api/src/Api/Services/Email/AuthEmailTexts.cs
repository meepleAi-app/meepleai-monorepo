using Api.BoundedContexts.Authentication.Domain.Constants;

namespace Api.Services.Email;

/// <summary>
/// The words of the account emails (password reset, email verification, two-factor disabled, suspended,
/// reactivated, locked), in the language of the user they are sent to (#4812). Italian when the recipient's
/// <c>User.Language</c> is Italian, English otherwise — English being what every one of these said before.
/// </summary>
/// <remarks>
/// <para>
/// Unlike a game night invitation, which follows the organizer's language (#4754), these go to the person the
/// account belongs to: the language is the recipient's own.
/// </para>
/// <para>
/// Which languages exist is <see cref="UserLanguage"/>'s to say, so a stored <c>fr</c> or a malformed value
/// gets English, the same ripiego the interface bundle has. Strings that carry a name, a number or a date are
/// functions. Nothing here is HTML-encoded: values reach the template exactly as the caller passed them,
/// which is what the English templates always did.
/// </para>
/// </remarks>
internal sealed class AuthEmailTexts
{
    public required string HtmlLang { get; init; }

    // Shared
    public required Func<string, string> Greeting { get; init; }
    public required string CopyLinkPrompt { get; init; }
    public required string AutomatedFooter { get; init; }
    public required string AutomatedSecurityFooter { get; init; }
    public required Func<int, string> Copyright { get; init; }

    // Password reset
    public required string ResetSubject { get; init; }
    public required string ResetTitle { get; init; }
    public required string ResetHeading { get; init; }
    public required string ResetIntro { get; init; }
    public required string ResetPrompt { get; init; }
    public required string ResetButton { get; init; }
    public required string ResetExpiry { get; init; }
    public required string ResetSupport { get; init; }

    // Email verification
    public required string VerifySubject { get; init; }
    public required string VerifyTitle { get; init; }
    public required string VerifyBannerHeading { get; init; }
    public required string VerifyBannerText { get; init; }
    public required string VerifyBody { get; init; }
    public required string VerifyButton { get; init; }
    public required string VerifyExpiry { get; init; }
    public required string VerifyIgnore { get; init; }

    // Two-factor authentication disabled
    public required string TwoFactorSubject { get; init; }
    public required string TwoFactorBannerHeading { get; init; }
    public required string TwoFactorReasonByAdmin { get; init; }
    public required string TwoFactorReasonBySelf { get; init; }
    public required string TwoFactorWarningByAdmin { get; init; }
    public required string TwoFactorWarningBySelf { get; init; }
    public required string TwoFactorImportantLabel { get; init; }
    public required string TwoFactorReenablePrompt { get; init; }
    public required string TwoFactorReenableStepLogin { get; init; }
    public required string TwoFactorReenableStepSettings { get; init; }
    public required string TwoFactorReenableStepSetup { get; init; }
    public required string TwoFactorNotice { get; init; }
    public required string TwoFactorQuestions { get; init; }

    // Account suspended
    public required string SuspendedSubject { get; init; }
    public required string SuspendedTitle { get; init; }
    public required string SuspendedBannerHeading { get; init; }
    public required string SuspendedBannerText { get; init; }
    public required string SuspendedBody { get; init; }
    public required string SuspendedReasonLabel { get; init; }
    public required string SuspendedAppeal { get; init; }
    public required string SuspendedContactButton { get; init; }
    public required string SuspendedClosing { get; init; }

    // Account reactivated
    public required string ReactivatedSubject { get; init; }
    public required string ReactivatedTitle { get; init; }
    public required string ReactivatedBannerHeading { get; init; }
    public required string ReactivatedBannerText { get; init; }
    public required string ReactivatedBody { get; init; }
    public required string ReactivatedWelcome { get; init; }
    public required string ReactivatedNextStepsLabel { get; init; }
    public required string ReactivatedStepLogin { get; init; }
    public required string ReactivatedStepGuidelines { get; init; }
    public required string ReactivatedStepEnjoy { get; init; }
    public required string ReactivatedClosing { get; init; }

    // Account locked
    public required string LockedSubject { get; init; }
    public required string LockedTitle { get; init; }
    public required string LockedBannerHeading { get; init; }
    public required string LockedBannerText { get; init; }
    public required Func<int, string> LockedBody { get; init; }
    public required string LockedDetailsLabel { get; init; }
    public required Func<int, string> LockedFailedAttemptsItem { get; init; }
    public required Func<int, string> LockedDurationItem { get; init; }
    public required Func<string, string> LockedUnlocksAtItem { get; init; }
    public required string LockedUnlocksAtFormat { get; init; }
    public required Func<string, string> LockedIpItem { get; init; }
    public required string LockedWhatToDoLabel { get; init; }
    public required string LockedStepWait { get; init; }
    public required string LockedStepPassword { get; init; }
    public required string LockedStepForgotPassword { get; init; }
    public required string LockedSecurityLabel { get; init; }
    public required string LockedSecurityIntro { get; init; }
    public required string LockedAdviceChangePassword { get; init; }
    public required string LockedAdviceTwoFactor { get; init; }
    public required string LockedAdviceReviewActivity { get; init; }
    public required string LockedSupport { get; init; }

    /// <summary>The texts for a user's stored language: Italian for "it" (also "it-IT", "IT_it"), English for anything else.</summary>
    public static AuthEmailTexts For(string? language) =>
        string.Equals(UserLanguage.Resolve(language), UserLanguage.Italian, StringComparison.Ordinal) ? Italian : English;

    private static readonly AuthEmailTexts English = new()
    {
        HtmlLang = "en",

        Greeting = name => $"Hello {name},",
        CopyLinkPrompt = "Or copy and paste this link into your browser:",
        AutomatedFooter = "This is an automated message, please do not reply to this email.",
        AutomatedSecurityFooter = "This is an automated security notification, please do not reply to this email.",
        Copyright = year => $"&copy; {year} MeepleAI. All rights reserved.",

        ResetSubject = "Reset Your MeepleAI Password",
        ResetTitle = "Reset Your Password",
        ResetHeading = "Password Reset Request",
        ResetIntro = "We received a request to reset your password for your MeepleAI account. If you didn't make this request, you can safely ignore this email.",
        ResetPrompt = "To reset your password, click the button below:",
        ResetButton = "Reset Password",
        ResetExpiry = "This password reset link will expire in 30 minutes for security reasons.",
        ResetSupport = "If you continue to have problems, please contact our support team.",

        VerifySubject = "Verify Your MeepleAI Email",
        VerifyTitle = "Verify Your Email",
        VerifyBannerHeading = "Welcome to MeepleAI!",
        VerifyBannerText = "Please verify your email address to get started.",
        VerifyBody = "Thank you for registering with MeepleAI! To complete your registration and access all features, please verify your email address by clicking the button below:",
        VerifyButton = "Verify Email Address",
        VerifyExpiry = "This verification link will expire in 24 hours for security reasons.",
        VerifyIgnore = "If you didn't create a MeepleAI account, you can safely ignore this email.",

        TwoFactorSubject = "Two-Factor Authentication Disabled",
        TwoFactorBannerHeading = "Security Alert",
        TwoFactorReasonByAdmin = "An administrator has disabled two-factor authentication on your account, likely due to a support request for lost authenticator access.",
        TwoFactorReasonBySelf = "Two-factor authentication has been disabled on your account.",
        TwoFactorWarningByAdmin = "If you did not request this change, please contact our support team immediately and consider re-enabling two-factor authentication.",
        TwoFactorWarningBySelf = "If you did not make this change, please contact our support team immediately and secure your account.",
        TwoFactorImportantLabel = "Important:",
        TwoFactorReenablePrompt = "To re-enable two-factor authentication:",
        TwoFactorReenableStepLogin = "Log in to your MeepleAI account",
        TwoFactorReenableStepSettings = "Go to Settings &gt; Privacy &gt; Two-Factor Authentication",
        TwoFactorReenableStepSetup = "Follow the setup instructions",
        TwoFactorNotice = "This notification was sent for security purposes to keep you informed of changes to your account.",
        TwoFactorQuestions = "If you have any questions or concerns, please contact our support team.",

        SuspendedSubject = "Your MeepleAI Account Has Been Suspended",
        SuspendedTitle = "Account Suspended",
        SuspendedBannerHeading = "🚨 Account Suspended",
        SuspendedBannerText = "Your account access has been temporarily suspended",
        SuspendedBody = "Your MeepleAI account has been suspended by an administrator. You will not be able to log in until your account is reactivated.",
        SuspendedReasonLabel = "Reason for suspension:",
        SuspendedAppeal = "If you believe this suspension was made in error or would like to appeal, please contact our support team.",
        SuspendedContactButton = "Contact Support",
        SuspendedClosing = "We take account security and community standards seriously. Thank you for your understanding.",

        ReactivatedSubject = "Your MeepleAI Account Has Been Reactivated",
        ReactivatedTitle = "Account Reactivated",
        ReactivatedBannerHeading = "✅ Account Reactivated",
        ReactivatedBannerText = "Welcome back! Your account is now active",
        ReactivatedBody = "Good news! Your MeepleAI account has been reactivated by an administrator. You can now log in and access all features again.",
        ReactivatedWelcome = "We're glad to have you back in the MeepleAI community!",
        ReactivatedNextStepsLabel = "💡 Next Steps:",
        ReactivatedStepLogin = "Log in to your account",
        ReactivatedStepGuidelines = "Review our community guidelines",
        ReactivatedStepEnjoy = "Continue discovering and enjoying board games!",
        ReactivatedClosing = "Thank you for being part of the MeepleAI community!",

        LockedSubject = "Security Alert: Your MeepleAI Account Has Been Locked",
        LockedTitle = "Account Locked",
        LockedBannerHeading = "🔒 Account Temporarily Locked",
        LockedBannerText = "Too many failed login attempts detected",
        LockedBody = attempts => $"Your MeepleAI account has been temporarily locked due to <strong>{attempts} consecutive failed login attempts</strong>.",
        LockedDetailsLabel = "Details:",
        LockedFailedAttemptsItem = attempts => $"Failed attempts: {attempts}",
        LockedDurationItem = minutes => $"Account locked for: {minutes} minutes",
        LockedUnlocksAtItem = when => $"Unlocks at: {when} UTC",
        LockedUnlocksAtFormat = "yyyy-MM-dd HH:mm",
        LockedIpItem = ip => $"IP Address: {ip}",
        LockedWhatToDoLabel = "💡 What to do:",
        LockedStepWait = "Wait for the lockout period to expire, then try again",
        LockedStepPassword = "Make sure you're using the correct password",
        LockedStepForgotPassword = "If you've forgotten your password, use the \"Forgot Password\" link",
        LockedSecurityLabel = "⚠️ Security Notice:",
        LockedSecurityIntro = "If you did not attempt these logins, someone may be trying to access your account. We recommend:",
        LockedAdviceChangePassword = "Change your password immediately after regaining access",
        LockedAdviceTwoFactor = "Enable two-factor authentication if available",
        LockedAdviceReviewActivity = "Review your recent account activity",
        LockedSupport = "If you need immediate assistance, please contact our support team.",
    };

    private static readonly AuthEmailTexts Italian = new()
    {
        HtmlLang = "it",

        Greeting = name => $"Ciao {name},",
        CopyLinkPrompt = "Oppure copia e incolla questo link nel tuo browser:",
        AutomatedFooter = "Questo è un messaggio automatico, non rispondere a questa email.",
        AutomatedSecurityFooter = "Questa è una notifica di sicurezza automatica, non rispondere a questa email.",
        Copyright = year => $"&copy; {year} MeepleAI. Tutti i diritti riservati.",

        ResetSubject = "Reimposta la tua password di MeepleAI",
        ResetTitle = "Reimposta la tua password",
        ResetHeading = "Richiesta di reimpostazione della password",
        ResetIntro = "Abbiamo ricevuto una richiesta di reimpostazione della password per il tuo account MeepleAI. Se non l'hai fatta tu, puoi ignorare questa email.",
        ResetPrompt = "Per reimpostare la password, clicca sul pulsante qui sotto:",
        ResetButton = "Reimposta la password",
        ResetExpiry = "Questo link di reimpostazione scade tra 30 minuti, per motivi di sicurezza.",
        ResetSupport = "Se continui ad avere problemi, contatta il nostro supporto.",

        VerifySubject = "Verifica la tua email di MeepleAI",
        VerifyTitle = "Verifica la tua email",
        VerifyBannerHeading = "Benvenuto su MeepleAI!",
        VerifyBannerText = "Verifica il tuo indirizzo email per iniziare.",
        VerifyBody = "Grazie per esserti registrato su MeepleAI! Per completare la registrazione e usare tutte le funzionalità, verifica il tuo indirizzo email cliccando sul pulsante qui sotto:",
        VerifyButton = "Verifica l'indirizzo email",
        VerifyExpiry = "Questo link di verifica scade tra 24 ore, per motivi di sicurezza.",
        VerifyIgnore = "Se non hai creato un account MeepleAI, puoi ignorare questa email.",

        TwoFactorSubject = "Autenticazione a due fattori disattivata",
        TwoFactorBannerHeading = "Avviso di sicurezza",
        TwoFactorReasonByAdmin = "Un amministratore ha disattivato l'autenticazione a due fattori sul tuo account, probabilmente in seguito a una richiesta di assistenza per la perdita dell'accesso all'app di autenticazione.",
        TwoFactorReasonBySelf = "L'autenticazione a due fattori è stata disattivata sul tuo account.",
        TwoFactorWarningByAdmin = "Se non hai richiesto questa modifica, contatta subito il nostro supporto e valuta di riattivare l'autenticazione a due fattori.",
        TwoFactorWarningBySelf = "Se non hai fatto tu questa modifica, contatta subito il nostro supporto e metti in sicurezza il tuo account.",
        TwoFactorImportantLabel = "Importante:",
        TwoFactorReenablePrompt = "Per riattivare l'autenticazione a due fattori:",
        TwoFactorReenableStepLogin = "Accedi al tuo account MeepleAI",
        TwoFactorReenableStepSettings = "Vai su Impostazioni &gt; Privacy &gt; Autenticazione a due fattori",
        TwoFactorReenableStepSetup = "Segui le istruzioni di configurazione",
        TwoFactorNotice = "Questa notifica è stata inviata per motivi di sicurezza, per tenerti informato sulle modifiche al tuo account.",
        TwoFactorQuestions = "Per qualsiasi domanda o dubbio, contatta il nostro supporto.",

        SuspendedSubject = "Il tuo account MeepleAI è stato sospeso",
        SuspendedTitle = "Account sospeso",
        SuspendedBannerHeading = "🚨 Account sospeso",
        SuspendedBannerText = "L'accesso al tuo account è stato sospeso temporaneamente",
        SuspendedBody = "Il tuo account MeepleAI è stato sospeso da un amministratore. Non potrai accedere finché l'account non sarà riattivato.",
        SuspendedReasonLabel = "Motivo della sospensione:",
        SuspendedAppeal = "Se pensi che la sospensione sia un errore o vuoi presentare ricorso, contatta il nostro supporto.",
        SuspendedContactButton = "Contatta il supporto",
        SuspendedClosing = "Prendiamo sul serio la sicurezza degli account e le regole della community. Grazie per la comprensione.",

        ReactivatedSubject = "Il tuo account MeepleAI è stato riattivato",
        ReactivatedTitle = "Account riattivato",
        ReactivatedBannerHeading = "✅ Account riattivato",
        ReactivatedBannerText = "Bentornato! Il tuo account è di nuovo attivo",
        ReactivatedBody = "Buone notizie! Il tuo account MeepleAI è stato riattivato da un amministratore. Ora puoi accedere e usare di nuovo tutte le funzionalità.",
        ReactivatedWelcome = "Siamo felici di riaverti nella community di MeepleAI!",
        ReactivatedNextStepsLabel = "💡 Prossimi passi:",
        ReactivatedStepLogin = "Accedi al tuo account",
        ReactivatedStepGuidelines = "Rileggi le linee guida della community",
        ReactivatedStepEnjoy = "Continua a scoprire e a divertirti con i giochi da tavolo!",
        ReactivatedClosing = "Grazie di far parte della community di MeepleAI!",

        LockedSubject = "Avviso di sicurezza: il tuo account MeepleAI è stato bloccato",
        LockedTitle = "Account bloccato",
        LockedBannerHeading = "🔒 Account bloccato temporaneamente",
        LockedBannerText = "Rilevati troppi tentativi di accesso falliti",
        LockedBody = attempts => $"Il tuo account MeepleAI è stato bloccato temporaneamente a causa di <strong>{attempts} tentativi di accesso consecutivi falliti</strong>.",
        LockedDetailsLabel = "Dettagli:",
        LockedFailedAttemptsItem = attempts => $"Tentativi falliti: {attempts}",
        LockedDurationItem = minutes => minutes == 1 ? "Account bloccato per: 1 minuto" : $"Account bloccato per: {minutes} minuti",
        LockedUnlocksAtItem = when => $"Si sblocca alle: {when} UTC",
        LockedUnlocksAtFormat = "dd/MM/yyyy HH:mm",
        LockedIpItem = ip => $"Indirizzo IP: {ip}",
        LockedWhatToDoLabel = "💡 Cosa fare:",
        LockedStepWait = "Aspetta che il blocco termini, poi riprova",
        LockedStepPassword = "Controlla di usare la password giusta",
        LockedStepForgotPassword = "Se hai dimenticato la password, usa il link \"Password dimenticata?\"",
        LockedSecurityLabel = "⚠️ Avviso di sicurezza:",
        LockedSecurityIntro = "Se non hai provato tu questi accessi, qualcuno potrebbe star cercando di entrare nel tuo account. Ti consigliamo di:",
        LockedAdviceChangePassword = "Cambiare subito la password appena riottieni l'accesso",
        LockedAdviceTwoFactor = "Attivare l'autenticazione a due fattori, se disponibile",
        LockedAdviceReviewActivity = "Controllare l'attività recente del tuo account",
        LockedSupport = "Se hai bisogno di assistenza immediata, contatta il nostro supporto.",
    };
}
