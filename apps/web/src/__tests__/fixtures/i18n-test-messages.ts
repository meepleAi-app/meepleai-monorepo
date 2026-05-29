/**
 * i18n Test Messages Constants
 *
 * Centralized translations for test environment.
 * Use these constants in both testMessages and test assertions
 * to avoid magic strings and ensure consistency.
 *
 * Issue #3077: 2FA component test support
 */

// =============================================================================
// OAuth Messages
// =============================================================================

export const oauthMessages = {
  'auth.oauth.separator': 'Or continue with',
  'auth.oauth.google': 'Continue with Google',
  'auth.oauth.discord': 'Continue with Discord',
  'auth.oauth.github': 'Continue with GitHub',
} as const;

// =============================================================================
// 2FA Verification Messages
// =============================================================================

export const twoFactorVerificationMessages = {
  'auth.2fa.verificationTitle': 'Two-Factor Authentication',
  'auth.2fa.verificationSubtitle': 'Enter the 6-digit code from your authenticator app',
  'auth.2fa.codeLabel': 'Verification Code',
  'auth.2fa.rememberDevice': 'Trust this device for 30 days',
  'auth.2fa.verifying': 'Verifying...',
  'auth.2fa.verify': 'Verify',
  'auth.2fa.useBackupCode': 'Use a backup code instead',
} as const;

// =============================================================================
// 2FA Setup Messages
// =============================================================================

export const twoFactorSetupMessages = {
  'auth.2fa.setupTitle': 'Set Up Two-Factor Authentication',
  'auth.2fa.setupSubtitle':
    'Scan this QR code with your authenticator app (Google Authenticator, Authy, etc.)',
  'auth.2fa.step1': 'Step 1: Scan QR Code',
  'auth.2fa.step2': 'Step 2: Verify & Enable',
  'auth.2fa.cantScan': "Can't scan? Enter manually",
  'auth.2fa.secretKey': 'Secret Key',
  'auth.2fa.enterCode': 'Enter the 6-digit code from your app',
  'auth.2fa.verifyAndEnable': 'Verify & Enable',
} as const;

// =============================================================================
// 2FA Disable Messages
// =============================================================================

export const twoFactorDisableMessages = {
  'auth.2fa.disableTitle': 'Disable Two-Factor Authentication',
  'auth.2fa.disableSubtitle': 'Enter your password and a code to disable 2FA',
  'auth.2fa.disableWarningTitle': 'Warning: Your account will be less secure',
  'auth.2fa.disableWarning':
    'Disabling two-factor authentication removes an important security layer from your account. Anyone with your password will be able to access your account.',
  'auth.2fa.currentPassword': 'Current Password',
  'auth.2fa.enterPassword': 'Enter your password',
  'auth.2fa.codeOrBackup': 'TOTP Code or Backup Code',
  'auth.2fa.enterCodeOrBackup': '000000 or XXXX-XXXX',
  'auth.2fa.disableButton': 'Disable 2FA',
  'auth.2fa.disabling': 'Disabling...',
  'auth.2fa.codeRequired': 'Please enter a 6-digit code or backup code',
  'auth.2fa.codeInvalid': 'Code is too long',
} as const;

// =============================================================================
// 2FA Backup/Recovery Codes Messages
// =============================================================================

export const twoFactorBackupCodesMessages = {
  'auth.2fa.backupCodesTitle': 'Save Your Backup Codes',
  'auth.2fa.backupCodesWarningTitle': 'Important: Save these codes now!',
  'auth.2fa.backupCodesWarning':
    'These codes are your only way to access your account if you lose your authenticator device. Each code can only be used once.',
  'auth.2fa.backupCodesList': 'Backup codes',
  'auth.2fa.copyCodes': 'Copy Codes',
  'auth.2fa.downloadCodes': 'Download',
  'auth.2fa.savedCodes': "I've Saved My Codes",
} as const;

// =============================================================================
// Common Messages
// =============================================================================

export const commonMessages = {
  'common.cancel': 'Cancel',
  'common.confirm': 'Confirm',
  'common.close': 'Close',
  'common.copy': 'Copy',
  'common.copied': 'Copied!',
  'common.downloaded': 'Downloaded!',
} as const;

// =============================================================================
// Validation Messages
// =============================================================================

export const validationMessages = {
  'validation.passwordRequired': 'Password is required',
  'validation.required': 'This field is required',
} as const;

// =============================================================================
// Play Records Index (Task 1: #1488)
// =============================================================================

export const playRecordsIndexMessages = {
  // Hero section
  'playRecords.index.hero.title': 'Le tue partite',
  'playRecords.index.hero.subtitle': 'Storico, esiti e classifiche delle partite registrate.',
  'playRecords.index.hero.label': 'Play records',
  'playRecords.index.hero.stats.games': 'partite',
  'playRecords.index.hero.stats.wins': 'vittorie',
  'playRecords.index.hero.stats.count': 'giochi',
  'playRecords.index.hero.stats.hours': 'totali',
  'playRecords.index.hero.cta': '+ Registra partita',

  // Filters
  'playRecords.index.filters.searchPlaceholder': 'Cerca partita o gioco...',
  'playRecords.index.filters.statusAll': 'Tutte',
  'playRecords.index.filters.statusInProgress': '● In corso',
  'playRecords.index.filters.statusCompleted': '✓ Completate',
  'playRecords.index.filters.statusPlanned': '📅 Pianificate',
  'playRecords.index.filters.dropdownGame': 'GIOCO',
  'playRecords.index.filters.dropdownDate': 'DATA',
  'playRecords.index.filters.dropdownOutcome': 'ESITO',
  'playRecords.index.filters.dropdownSort': 'SORT',
  'playRecords.index.filters.viewList': 'List · default',
  'playRecords.index.filters.viewGrid': 'Grid',
  'playRecords.index.filters.toggleArchivedLabel': 'Mostra archiviate',

  // Card list
  'playRecords.index.card.duration': 'Durata',
  'playRecords.index.card.players': 'giocatori',
  'playRecords.index.card.winner': 'Vincitore',
  'playRecords.index.card.resume': '▶ Riprendi',
  'playRecords.index.card.launch': 'Avvia ora',

  // Empty states
  'playRecords.index.empty.firstRunTitle': 'Nessuna partita registrata',
  'playRecords.index.empty.firstRunMessage':
    'Registra la tua prima partita per tracciare punteggi, esiti e classifiche del gruppo.',
  'playRecords.index.empty.firstRunCta': '+ Registra prima partita',
  'playRecords.index.empty.filterTitle': 'Nessuna partita per questi filtri',
  'playRecords.index.empty.filterMessage': 'Prova a rimuovere alcuni vincoli o cambiare periodo.',
  'playRecords.index.empty.resetButton': '↻ Reset filtri',

  // Error
  'playRecords.index.error.title': 'Errore caricamento',
  'playRecords.index.error.message': 'Impossibile recuperare le partite. Verifica la connessione.',
  'playRecords.index.error.retry': '↻ Riprova',

  // Accessibility
  'playRecords.index.a11y.searchRole': 'search',
  'playRecords.index.a11y.resultsLive': 'polite',
} as const;

// =============================================================================
// Play Records — New Wizard i18n messages (Italian)
// =============================================================================

export const playRecordsNewMessages = {
  pageTitle: 'Registra partita',
  pageSubtitle: 'Wizard passo-passo per registrare una partita',
  stepIndicatorLabel: 'Step {current} di 3',
  step1Label: 'Gioco',
  step2Label: 'Quando',
  step3Label: 'Punteggi',

  // Step 1 keys
  'step1.title': 'Quale gioco avete giocato?',
  'step1.subtitle': 'Scegli dalla tua libreria.',
  'step1.searchPlaceholder': 'Cerca nella libreria…',
  'step1.emptyLibraryHint': '↑ Seleziona un gioco per continuare',
  'step1.freeformLabel': 'Nome gioco (testo libero)',
  'step1.freeformPlaceholder': 'Es. Catan, Puerto Rico…',
  'step1.freeformHint': 'Inserisci il nome del gioco se non è in catalogo',

  // Step 2 keys
  'step2.title': "Quando l'avete giocata?",
  'step2.subtitle': 'Data, ora di inizio e durata.',
  'step2.dateLabel': 'Data',
  'step2.timeLabel': 'Ora di inizio',
  'step2.durationLabel': 'Durata (opzionale)',
  'step2.locationLabel': 'Luogo (opzionale)',
  'step2.locationPlaceholder': 'Es. Casa di Marco',
  'step2.notesLabel': 'Note (opzionale)',
  'step2.notesPlaceholder': 'Aggiungi un commento sulla partita…',
  'step2.durationHint': 'Approssimativa',

  // Step 3 keys
  'step3.title': 'Chi ha giocato e con che punteggio?',
  'step3.subtitle': 'Inserisci i punteggi.',
  'step3.playersLabel': 'Giocatori e punteggi ({count})',
  'step3.addPlayerPlaceholder': 'Nome giocatore…',
  'step3.addPlayerButton': 'Aggiungi giocatore',
  'step3.winnerBadge': 'Vincitore',
  'step3.noPlayersHint': 'Aggiungi almeno un giocatore',

  // Preview
  'preview.sectionLabel': 'Anteprima live · Record partita',
  'preview.noGameSelected': 'Seleziona un gioco',
  'preview.rankingLabel': 'Classifica · {count} giocatori',
  'preview.tip': 'Il vincitore è calcolato dal punteggio più alto.',

  // Actions
  'actions.next': 'Avanti →',
  'actions.back': '← Indietro',
  'actions.save': '✓ Salva partita',
  'actions.saving': 'Salvataggio…',
  'actions.cancel': 'Annulla',
  'actions.draft': 'Bozza',

  // Success / error
  'success.toast': 'Partita registrata',
  'success.toastDescription': 'La partita è stata registrata correttamente',
  'error.saveFailed': 'Salvataggio non riuscito',
  'error.saveFailedDescription': 'Impossibile salvare la partita.',
  'error.retry': '↻ Riprova salvataggio',
  'loading.library': 'Caricamento libreria…',

  // A11y
  'a11y.wizardLabel': 'Wizard registrazione partita',
  'a11y.stepIndicatorLabel': 'Avanzamento registrazione',
  'a11y.backButton': 'Torna al passo precedente',
  'a11y.closeButton': 'Chiudi',
  'a11y.previewLabel': 'Anteprima live record partita',
} as const;

// =============================================================================
// Play Records Edit Page Messages
// =============================================================================

export const playRecordsEditMessages = {
  pageTitle: 'Modifica partita',
  pageSubtitle: 'Aggiorna i dettagli della partita',

  // K5 gate banner
  'banner.gateTitle': 'Modifica limitata',
  'banner.gateDescription': 'Per modificare giocatori o punteggi, elimina e ricrea la partita',
  'banner.deleteAction': 'Cancella partita',

  // Editable fields
  'fields.sessionDate': 'Data partita',
  'fields.location': 'Luogo (opzionale)',
  'fields.notes': 'Note (opzionale)',

  // Readonly fields
  'fields.game': 'Gioco',
  'fields.players': 'Giocatori',
  'fields.scores': 'Punteggi',

  // Delete confirmation
  'delete.title': 'Elimina partita?',
  'delete.description':
    'Questa azione non può essere annullata. La partita e tutti i dati associati verranno eliminati.',
  'delete.confirm': 'Elimina',
  'delete.cancel': 'Annulla',

  // Success / error
  'success.toast': 'Partita aggiornata',
  'success.toastDescription': 'Le modifiche sono state salvate',
  'success.deleteToast': 'Partita eliminata',
  'success.deleteToastDescription': 'La partita è stata eliminata correttamente',

  'error.loadFailed': 'Impossibile caricare la partita',
  'error.updateFailed': 'Aggiornamento non riuscito',
  'error.deleteFailed': 'Eliminazione non riuscita',

  // A11y
  'a11y.formLabel': 'Modulo modifica partita',
  'a11y.readonlyFieldsLabel': 'Campi non modificabili (K5 gate)',
  'a11y.focusSessionDate': 'Focus iniziale sul campo data',
  'a11y.ariaReadonly': 'Questo campo non è modificabile',

  // Actions
  'actions.save': 'Salva modifiche',
  'actions.saving': 'Salvataggio…',
  'actions.delete': 'Elimina',
  'actions.cancel': 'Annulla',
  'actions.back': '← Indietro',
} as const;

// =============================================================================
// Settings Page Labels (Italian - hardcoded in component)
// =============================================================================

export const settingsLabels = {
  // Tab labels
  'settings.tabs.profile': 'Profilo',
  'settings.tabs.preferences': 'Preferenze',
  'settings.tabs.privacy': 'Privacy',
  'settings.tabs.account': 'Account',

  // Profile tab
  'settings.profile.displayName': 'Nome visualizzato',
  'settings.profile.email': 'Email',

  // Preferences tab
  'settings.preferences.language': 'Lingua',
  'settings.preferences.theme': 'Tema',
  'settings.preferences.emailNotifications': 'Notifiche email',
  'settings.preferences.animations': 'Animazioni',

  // Privacy tab
  'settings.privacy.publicProfile': 'Profilo visibile',
  'settings.privacy.showLibrary': 'Mostra libreria',
  'settings.privacy.showActivity': 'Mostra attività',
  'settings.privacy.showBadges': 'Mostra badge',
  'settings.privacy.dataRetention': 'Periodo di conservazione',

  // Account tab
  'settings.account.currentPassword': 'Password attuale',
  'settings.account.newPassword': 'Nuova password',
  'settings.account.confirmPassword': 'Conferma nuova password',
  'settings.account.disableCode': 'Codice TOTP o codice di backup',
  'settings.account.apiKey': 'API Key',
} as const;

// =============================================================================
// All Test Messages (combined)
// =============================================================================

export const allTestMessages: Record<string, string> = {
  ...oauthMessages,
  ...twoFactorVerificationMessages,
  ...twoFactorSetupMessages,
  ...twoFactorDisableMessages,
  ...twoFactorBackupCodesMessages,
  ...commonMessages,
  ...validationMessages,
  ...settingsLabels,
  ...playRecordsIndexMessages,
};

// =============================================================================
// Type-safe message getter
// =============================================================================

type AllMessageKeys =
  | keyof typeof oauthMessages
  | keyof typeof twoFactorVerificationMessages
  | keyof typeof twoFactorSetupMessages
  | keyof typeof twoFactorDisableMessages
  | keyof typeof twoFactorBackupCodesMessages
  | keyof typeof commonMessages
  | keyof typeof validationMessages
  | keyof typeof settingsLabels;

/**
 * Get a translated message by key (type-safe)
 * Use this in tests to avoid magic strings
 *
 * @example
 * expect(screen.getByText(msg('auth.2fa.disableTitle'))).toBeInTheDocument();
 */
export function msg(key: AllMessageKeys): string {
  return allTestMessages[key];
}

// =============================================================================
// Settings Page Test Patterns (for getByRole, getByLabelText with regex)
// =============================================================================

/**
 * Regex patterns for settings page tests.
 * These patterns match the Italian labels in the settings component.
 * Use with screen.getByRole('tab', { name: settingsPatterns.tabs.profile })
 */
export const settingsPatterns = {
  tabs: {
    profile: /profil/i, // Matches 'Profilo' (IT) and 'Profile' (EN)
    preferences: /prefer/i, // Matches 'Preferenze' (IT) and 'Preferences' (EN)
    privacy: /privacy/i, // Same in both languages
    account: /account/i, // Same in both languages
  },
  labels: {
    displayName: /nome visualizzato/i,
    email: /email/i,
    language: /lingua/i,
    theme: /tema/i,
    currentPassword: /password attuale/i,
    newPassword: /^nuova password$|^new password$/i,
    confirmPassword: /conferma.*password|confirm.*password/i,
  },
  buttons: {
    changePassword: /change password|cambia password/i,
    unlink: /unlink|scollega/i,
    revoke: /revoke|revoca/i,
    save: /save|salva/i,
    cancel: /cancel|annulla/i,
    delete: /delete|elimina/i,
    enable: /enable|abilita/i,
    disable: /disable|disabilita/i,
  },
  switches: {
    emailNotification: /email notification|notification|notifiche/i,
  },
  options: {
    italiano: /italiano/i,
    english: /english|inglese/i,
    dark: /dark|scuro/i,
    light: /light|chiaro/i,
    system: /system|sistema/i,
  },
  twoFactor: {
    savedCodes: /i've saved my codes|ho salvato i miei codici/i,
  },
  text: {
    error: /error|failed|errore/i,
    linkedAccounts: /linked accounts|account collegati/i,
    google: /google/i,
    apiKeyAuth: /api key authentication|autenticazione api key/i,
    activeSessions: /active sessions|sessioni attive/i,
    dangerZone: /danger zone|zona pericolosa/i,
    failedToLoad: /failed to load profile|error|errore/i,
    failedToSetup2fa: /failed to setup 2fa|error|errore/i,
    networkError: /network|connection|error|rete|connessione|errore/i,
  },
  // Admin users table patterns
  menu: {
    openMenu: /open menu|apri menu/i,
    edit: /^edit$|^modifica$/i,
    delete: /^delete$|^elimina$/i,
    suspend: /suspend|sospendi/i,
    activate: /activate|attiva/i,
    viewDetails: /view details|visualizza dettagli/i,
  },
} as const;
