/**
 * i18n Test Helper for E2E Tests
 *
 * Purpose: Provides language-agnostic text matching for E2E tests
 * to handle both English and Italian (or any other language) UI text.
 *
 * Problem: Tests were written in English but UI is in Italian,
 * causing 60% of test failures due to text mismatches.
 *
 * Solution: Define text mappings for multiple languages and provide
 * flexible matchers that work with any configured language.
 *
 * Usage:
 * ```typescript
 * import { t, getTextMatcher } from './fixtures/i18n';
 *
 * // Option 1: Use translation keys
 * await page.getByRole('button', { name: t('auth.login') });
 *
 * // Option 2: Use flexible matchers (regex that matches any language)
 * const matcher = getTextMatcher('auth.login');
 * await page.getByRole('button', { name: matcher });
 * ```
 */

export interface TranslationMap {
  [key: string]: {
    en: string;
    it: string;
  };
}

/**
 * Translation mappings for common UI text
 * Add new translations here as needed
 */
export const translations: TranslationMap = {
  // Authentication
  'auth.login': { en: 'Login', it: 'Accedi' },
  'auth.logout': { en: 'Logout', it: 'Esci' },
  'auth.register': { en: 'Register', it: 'Registrati' },
  'auth.email': { en: 'Email', it: 'Email' },
  'auth.password': { en: 'Password', it: 'Password' },
  'auth.confirmPassword': { en: 'Confirm Password', it: 'Conferma Password' },
  'auth.forgotPassword': { en: 'Forgot Password?', it: 'Password dimenticata?' },
  'auth.rememberMe': { en: 'Remember me', it: 'Ricordami' },

  // OAuth
  'auth.oauth.google': { en: 'Continue with Google', it: 'Continua con Google' },
  'auth.oauth.discord': { en: 'Continue with Discord', it: 'Continua con Discord' },
  'auth.oauth.github': { en: 'Continue with GitHub', it: 'Continua con GitHub' },
  'auth.oauth.separator': { en: 'Or continue with', it: 'Oppure continua con' },

  // Navigation
  'nav.home': { en: 'Home', it: 'Home' },
  'nav.chat': { en: 'Chat', it: 'Chat' },
  'nav.upload': { en: 'Upload', it: 'Carica' },
  'nav.admin': { en: 'Admin', it: 'Admin' },
  'nav.profile': { en: 'Profile', it: 'Profilo' },
  'nav.settings': { en: 'Settings', it: 'Impostazioni' },

  // Common actions
  'common.save': { en: 'Save', it: 'Salva' },
  'common.cancel': { en: 'Cancel', it: 'Annulla' },
  'common.delete': { en: 'Delete', it: 'Elimina' },
  'common.edit': { en: 'Edit', it: 'Modifica' },
  'common.create': { en: 'Create', it: 'Crea' },
  'common.confirm': { en: 'Confirm', it: 'Conferma' },
  'common.close': { en: 'Close', it: 'Chiudi' },
  'common.back': { en: 'Back', it: 'Indietro' },
  'common.next': { en: 'Next', it: 'Avanti' },
  'common.submit': { en: 'Submit', it: 'Invia' },
  'common.search': { en: 'Search', it: 'Cerca' },
  'common.filter': { en: 'Filter', it: 'Filtra' },
  'common.loading': { en: 'Loading...', it: 'Caricamento...' },

  // Admin
  'admin.users': { en: 'Users', it: 'Utenti' },
  'admin.analytics': { en: 'Analytics', it: 'Analisi' },
  'admin.configuration': { en: 'Configuration', it: 'Configurazione' },
  'admin.createUser': { en: 'Create User', it: 'Crea Utente' },
  'admin.editUser': { en: 'Edit User', it: 'Modifica Utente' },
  'admin.deleteUser': { en: 'Delete User', it: 'Elimina Utente' },

  // Chat
  'chat.typeMessage': { en: 'Type a message...', it: 'Scrivi un messaggio...' },
  'chat.send': { en: 'Send', it: 'Invia' },
  'chat.newChat': { en: 'New Chat', it: 'Nuova Chat' },
  'chat.clearHistory': { en: 'Clear History', it: 'Cancella Cronologia' },

  // Upload
  'upload.dragDrop': { en: 'Drag and drop file here', it: 'Trascina e rilascia il file qui' },
  'upload.selectFile': { en: 'Select File', it: 'Seleziona File' },
  'upload.uploading': { en: 'Uploading...', it: 'Caricamento...' },
  'upload.success': { en: 'Upload successful', it: 'Caricamento riuscito' },
  'upload.error': { en: 'Upload failed', it: 'Caricamento fallito' },

  // Errors
  'error.generic': { en: 'An error occurred', it: 'Si è verificato un errore' },
  'error.network': { en: 'Network error', it: 'Errore di rete' },
  'error.unauthorized': { en: 'Unauthorized', it: 'Non autorizzato' },
  'error.notFound': { en: 'Not found', it: 'Non trovato' },
  'error.validation': { en: 'Validation error', it: 'Errore di validazione' },

  // Home Page
  'home.title': { en: 'Your AI-Powered Board Game Rules Assistant', it: 'Il Tuo Assistente AI per le Regole dei Giochi da Tavolo' },
  'home.chatLink': { en: 'Chat', it: 'Chat' },
  'home.uploadLink': { en: 'Upload', it: 'Upload' },
  'home.logsLink': { en: 'API Logs', it: 'Log API' },
  'home.editorLink': { en: 'RuleSpec Editor', it: 'Editor RuleSpec' },
  'home.registerModalTitle': { en: 'Create Your Account', it: 'Crea il Tuo Account' },
  'home.loginModalTitle': { en: 'Login to MeepleAI', it: 'Accedi a MeepleAI' },
  'home.registerEmail': { en: 'Email', it: 'Email' },
  'home.registerPassword': { en: 'Password', it: 'Password' },
  'home.registerDisplayName': { en: 'Display Name', it: 'Nome Visualizzato' },
  'home.registerRole': { en: 'Role', it: 'Ruolo' },
  'home.registerButton': { en: 'Create Account', it: 'Crea Account' },
  'home.loginEmail': { en: 'Email', it: 'Email' },
  'home.loginPassword': { en: 'Password', it: 'Password' },
  'home.loginButton': { en: 'Login', it: 'Login' },
  'home.logoutButton': { en: 'Logout', it: 'Logout' },
  'home.registerTab': { en: 'Register', it: 'Registrati' },
  'home.loginTab': { en: 'Login', it: 'Login' },
  'home.getStartedButton': { en: 'Get Started', it: 'Inizia' },

  // Admin Analytics Dashboard
  'admin.analytics.dashboard': { en: 'Analytics Dashboard', it: 'Dashboard Analisi' },
  'admin.analytics.totalUsers': { en: 'Total Users', it: 'Utenti Totali' },
  'admin.analytics.activeSessions': { en: 'Active Sessions', it: 'Sessioni Attive' },
  'admin.analytics.apiRequestsToday': { en: 'API Requests Today', it: 'Richieste API Oggi' },
  'admin.analytics.totalPdfDocuments': { en: 'Total PDF Documents', it: 'Documenti PDF Totali' },
  'admin.analytics.totalChatMessages': { en: 'Total Chat Messages', it: 'Messaggi Chat Totali' },
  'admin.analytics.avgConfidenceScore': { en: 'Avg Confidence Score', it: 'Punteggio Confidenza Medio' },
  'admin.analytics.totalRagRequests': { en: 'Total RAG Requests', it: 'Richieste RAG Totali' },
  'admin.analytics.totalTokensUsed': { en: 'Total Tokens Used', it: 'Token Totali Usati' },
  'admin.analytics.userRegistrations': { en: 'User Registrations', it: 'Registrazioni Utenti' },
  'admin.analytics.sessionCreations': { en: 'Session Creations', it: 'Creazioni Sessioni' },
  'admin.analytics.apiRequests': { en: 'API Requests', it: 'Richieste API' },
  'admin.analytics.pdfUploads': { en: 'PDF Uploads', it: 'Caricamenti PDF' },
  'admin.analytics.chatMessages': { en: 'Chat Messages', it: 'Messaggi Chat' },
  'admin.analytics.autoRefreshOn': { en: 'Auto-refresh ON', it: 'Aggiornamento automatico ON' },
  'admin.analytics.autoRefreshOff': { en: 'Auto-refresh OFF', it: 'Aggiornamento automatico OFF' },
  'admin.analytics.refresh': { en: 'Refresh', it: 'Aggiorna' },
  'admin.analytics.lastUpdated': { en: 'Last updated:', it: 'Ultimo aggiornamento:' },
  'admin.analytics.exportCsv': { en: 'Export CSV', it: 'Esporta CSV' },
  'admin.analytics.exportJson': { en: 'Export JSON', it: 'Esporta JSON' },
  'admin.analytics.exportedCsv': { en: 'Analytics exported as CSV', it: 'Analisi esportate come CSV' },
  'admin.analytics.exportedJson': { en: 'Analytics exported as JSON', it: 'Analisi esportate come JSON' },
  'admin.analytics.backToUsers': { en: 'Back to Users', it: 'Torna agli Utenti' },

  // Admin User Management
  'admin.users.management': { en: 'User Management', it: 'Gestione Utenti' },
  'admin.users.createUser': { en: 'Create User', it: 'Crea Utente' },
  'admin.users.editUser': { en: 'Edit User', it: 'Modifica Utente' },
  'admin.users.deleteUser': { en: 'Delete User', it: 'Elimina Utente' },
  'admin.users.email': { en: 'Email *', it: 'Email *' },
  'admin.users.password': { en: 'Password *', it: 'Password *' },
  'admin.users.displayName': { en: 'Display Name *', it: 'Nome Visualizzato *' },
  'admin.users.role': { en: 'Role *', it: 'Ruolo *' },
  'admin.users.createdSuccess': { en: 'created successfully', it: 'creato con successo' },
  'admin.users.updatedSuccess': { en: 'updated successfully', it: 'aggiornato con successo' },
  'admin.users.deletedSuccess': { en: 'deleted successfully', it: 'eliminato con successo' },
  'admin.users.saveChanges': { en: 'Save Changes', it: 'Salva Modifiche' },
  'admin.users.searchPlaceholder': { en: 'Search by email or name...', it: 'Cerca per email o nome...' },
  'admin.users.roleAll': { en: 'All', it: 'Tutti' },
  'admin.users.roleUser': { en: 'User', it: 'Utente' },
  'admin.users.roleEditor': { en: 'Editor', it: 'Editor' },
  'admin.users.roleAdmin': { en: 'Admin', it: 'Admin' },
  'admin.users.deleteConfirm': { en: 'Are you sure you want to delete', it: 'Sei sicuro di voler eliminare' },
  'admin.users.deleteMultiple': { en: 'Delete Multiple Users', it: 'Elimina Più Utenti' },
  'admin.users.deleteSelected': { en: 'Delete Selected', it: 'Elimina Selezionati' },
  'admin.users.deletedMultipleSuccess': { en: 'user(s) deleted successfully', it: 'utente(i) eliminato(i) con successo' },
  'admin.users.page': { en: 'Page', it: 'Pagina' },
  'admin.users.showing': { en: 'Showing', it: 'Mostrando' },
  'admin.users.to': { en: 'to', it: 'a' },
  'admin.users.of': { en: 'of', it: 'di' },
  'admin.users.users': { en: 'users', it: 'utenti' },
  'admin.users.validationEmail': { en: 'Valid email is required', it: 'Email valida richiesta' },
  'admin.users.validationPassword': { en: 'Password must be at least 8 characters', it: 'La password deve essere di almeno 8 caratteri' },
  'admin.users.validationDisplayName': { en: 'Display name is required', it: 'Nome visualizzato richiesto' },

  // Setup Guide
  'setup.loginRequired': { en: 'Login Required', it: 'Accesso Richiesto' },
  'setup.loginRequiredMessage': { en: 'You must be logged in to use the setup guide.', it: 'Devi effettuare l\'accesso per usare la guida di setup.' },
  'setup.goToLogin': { en: 'Go to Login', it: 'Vai al Login' },
  'setup.backToHome': { en: '← Back to Home', it: '← Torna alla Home' },
  'setup.heading': { en: 'Game Setup Guide', it: 'Guida Setup Gioco' },
  'setup.selectGame': { en: 'Select Game', it: 'Seleziona Gioco' },
  'setup.gameLabel': { en: 'Game:', it: 'Gioco:' },
  'setup.selectGamePlaceholder': { en: 'Select a game...', it: 'Seleziona un gioco...' },
  'setup.generateButton': { en: 'Generate Setup Guide', it: 'Genera Guida Setup' },
  'setup.generating': { en: 'Generating...', it: 'Generazione...' },
  'setup.generatingMessage': { en: 'Generating your setup guide...', it: 'Generazione della guida di setup...' },
  'setup.generatingSubMessage': { en: 'This may take a few moments', it: 'Potrebbe richiedere alcuni istanti' },
  'setup.progress': { en: 'Progress:', it: 'Progresso:' },
  'setup.estimatedTime': { en: 'Estimated setup time:', it: 'Tempo stimato di setup:' },
  'setup.setupComplete': { en: 'Setup Complete!', it: 'Setup Completato!' },
  'setup.readyToPlay': { en: 'Your game is ready to play', it: 'Il tuo gioco è pronto per giocare' },
  'setup.references': { en: 'References', it: 'Riferimenti' },
  'setup.viewButton': { en: 'View', it: 'Visualizza' },
  'setup.closeButton': { en: 'Close', it: 'Chiudi' },
  'setup.resetProgress': { en: 'Reset Progress', it: 'Reimposta Progresso' },
  'setup.noGuideYet': { en: 'No Setup Guide Yet', it: 'Nessuna Guida di Setup' },
  'setup.noGuideMessage': { en: 'Select a game and click "Generate Setup Guide"', it: 'Seleziona un gioco e clicca "Genera Guida Setup"' },
  'setup.optional': { en: 'OPTIONAL', it: 'OPZIONALE' },
  'setup.aiConfidence': { en: 'AI Confidence:', it: 'Confidenza AI:' },

  // Timeline RAG
  'timeline.heading': { en: 'RAG Events Timeline', it: 'Timeline Eventi RAG' },
  'timeline.filters': { en: 'Filters', it: 'Filtri' },
  'timeline.events': { en: 'Events', it: 'Eventi' },
  'timeline.noEvents': { en: 'No events to display', it: 'Nessun evento da visualizzare' },
  'timeline.eventType': { en: 'Event Type', it: 'Tipo Evento' },
  'timeline.status': { en: 'Status', it: 'Stato' },
  'timeline.search': { en: 'Search', it: 'Cerca' },
  'timeline.searchPlaceholder': { en: 'Search...', it: 'Cerca...' },
  'timeline.message': { en: 'Message', it: 'Messaggio' },
  'timeline.ragSearch': { en: 'RAG Search', it: 'Ricerca RAG' },
  'timeline.reset': { en: 'Reset', it: 'Reimposta' },
  'timeline.eventDetails': { en: 'Event Details', it: 'Dettagli Evento' },
  'timeline.details': { en: 'Details', it: 'Dettagli' },
  'timeline.total': { en: 'Total', it: 'Totale' },
  'timeline.completed': { en: 'Completed', it: 'Completati' },

  // Chat
  'chat.loginRequired': { en: 'Login required', it: 'Accesso richiesto' },
  'chat.loginRequiredMessage': { en: 'You must be logged in to use the chat', it: 'Devi effettuare l\'accesso per utilizzare la chat' },
  'chat.goToLogin': { en: 'Go to Login', it: 'Vai al Login' },
  'chat.heading': { en: 'MeepleAI Chat', it: 'Chat MeepleAI' },
  'chat.writeMessage': { en: 'Write', it: 'Scrivi' },
  'chat.sources': { en: 'Sources:', it: 'Fonti:' },
};

/**
 * Detect current UI language from environment or defaults to English
 * Can be overridden with TEST_LANG environment variable
 */
export function getCurrentLanguage(): 'en' | 'it' {
  const envLang = process.env.TEST_LANG?.toLowerCase();
  if (envLang === 'it' || envLang === 'italian') return 'it';
  if (envLang === 'en' || envLang === 'english') return 'en';

  // Default to English for tests unless explicitly set
  return 'en';
}

/**
 * Translate a key to the current language
 * @param key Translation key (e.g., 'auth.login')
 * @param lang Optional language override
 * @returns Translated text
 *
 * @example
 * t('auth.login') // => 'Login' (en) or 'Accedi' (it)
 * t('auth.login', 'it') // => 'Accedi'
 */
export function t(key: string, lang?: 'en' | 'it'): string {
  const targetLang = lang || getCurrentLanguage();
  const translation = translations[key];

  if (!translation) {
    console.warn(`⚠️  Missing translation for key: ${key}`);
    return key;
  }

  return translation[targetLang];
}

/**
 * Create a regex matcher that matches text in ANY language
 * Useful for getByRole() and similar Playwright matchers that accept regex
 *
 * @param key Translation key
 * @returns RegExp that matches any language variant
 *
 * @example
 * const matcher = getTextMatcher('auth.login');
 * await page.getByRole('button', { name: matcher });
 * // Matches both "Login" and "Accedi"
 */
export function getTextMatcher(key: string): RegExp {
  const translation = translations[key];

  if (!translation) {
    console.warn(`⚠️  Missing translation for key: ${key}`);
    return new RegExp(key, 'i');
  }

  // Create regex that matches any language variant (case-insensitive)
  const variants = Object.values(translation).map(text =>
    // Escape regex special characters
    text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  );

  return new RegExp(`(${variants.join('|')})`, 'i');
}

/**
 * Create a flexible text matcher that matches partial text in any language
 * Useful for text that might be embedded in longer strings
 *
 * @param key Translation key
 * @returns RegExp with flexible matching
 *
 * @example
 * const matcher = getFlexibleMatcher('auth.login');
 * await page.getByText(matcher);
 * // Matches "Click here to Login" or "Clicca qui per Accedi"
 */
export function getFlexibleMatcher(key: string): RegExp {
  const translation = translations[key];

  if (!translation) {
    console.warn(`⚠️  Missing translation for key: ${key}`);
    return new RegExp(key, 'i');
  }

  // Create regex with word boundaries for partial matching
  const variants = Object.values(translation).map(text =>
    text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  );

  return new RegExp(`.*?(${variants.join('|')}).*?`, 'i');
}

/**
 * Check if a string matches a translation key in any language
 * @param text Text to check
 * @param key Translation key
 * @returns true if text matches any language variant
 */
export function matchesTranslation(text: string, key: string): boolean {
  const matcher = getTextMatcher(key);
  return matcher.test(text);
}

/**
 * Get all language variants for a key
 * @param key Translation key
 * @returns Object with all language variants
 */
export function getAllVariants(key: string): { en: string; it: string } | null {
  return translations[key] || null;
}

/**
 * Add or update a translation dynamically (for test-specific text)
 * @param key Translation key
 * @param en English text
 * @param it Italian text
 */
export function addTranslation(key: string, en: string, it: string): void {
  translations[key] = { en, it };
}