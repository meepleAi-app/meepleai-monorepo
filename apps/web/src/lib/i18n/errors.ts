/**
 * Error Localization for MeepleAI
 * Italian-first error messages for authentication and application errors
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 */

export type ErrorType = 'validation' | 'auth' | 'server' | 'network' | 'forbidden' | 'conflict';

export interface LocalizedError {
  type: ErrorType;
  message: string;
  field?: string;
}

/**
 * Map HTTP status codes to Italian error messages
 */
export function getLocalizedError(
  statusCode: number | undefined,
  errorMessage?: string,
  field?: string
): LocalizedError {
  // Network errors (no status code)
  if (!statusCode) {
    return {
      type: 'network',
      message: 'Impossibile connettersi al server. Verifica la tua connessione.',
      field
    };
  }

  // Map status codes to localized messages
  switch (statusCode) {
    case 400:
      return {
        type: 'validation',
        message: errorMessage || 'Dati non validi. Controlla i campi inseriti.',
        field
      };

    case 401:
      // Distinguish between invalid credentials and expired session
      if (errorMessage?.toLowerCase().includes('session') ||
          errorMessage?.toLowerCase().includes('sessione')) {
        return {
          type: 'auth',
          message: 'Sessione scaduta. Effettua nuovamente il login.',
          field
        };
      }
      return {
        type: 'auth',
        message: 'Email o password non corretti.',
        field
      };

    case 403:
      return {
        type: 'forbidden',
        message: 'Accesso negato. Non hai i permessi necessari.',
        field
      };

    case 409:
      // Typically email already exists during registration
      if (field === 'email' || errorMessage?.toLowerCase().includes('email')) {
        return {
          type: 'conflict',
          message: 'Questa email è già registrata. Prova con un\'altra email o effettua il login.',
          field: 'email'
        };
      }
      return {
        type: 'conflict',
        message: errorMessage || 'Conflitto: risorsa già esistente.',
        field
      };

    case 422:
      return {
        type: 'validation',
        message: errorMessage || 'Dati non validi. Controlla il formato dei campi.',
        field
      };

    case 500:
    case 502:
    case 503:
    case 504:
      return {
        type: 'server',
        message: 'Errore del server. Riprova più tardi.',
        field
      };

    default:
      return {
        type: 'server',
        message: errorMessage || 'Si è verificato un errore imprevisto.',
        field
      };
  }
}

/**
 * Validation error messages (Italian)
 */
export const validationMessages = {
  emailRequired: 'L\'email è obbligatoria.',
  emailInvalid: 'Inserisci un indirizzo email valido.',
  passwordRequired: 'La password è obbligatoria.',
  passwordTooShort: 'La password deve contenere almeno 8 caratteri.',
  passwordTooWeak: 'La password deve contenere lettere maiuscole, minuscole, numeri e caratteri speciali.',
  displayNameRequired: 'Il nome visualizzato è obbligatorio.',
  displayNameTooShort: 'Il nome deve contenere almeno 2 caratteri.',
  displayNameTooLong: 'Il nome non può superare i 50 caratteri.',
  passwordsDoNotMatch: 'Le password non corrispondono.',
  termsRequired: 'Devi accettare i termini e condizioni.',
} as const;

/**
 * Success messages (Italian)
 */
export const successMessages = {
  loginSuccess: 'Login effettuato con successo!',
  registrationSuccess: 'Registrazione completata! Benvenuto su MeepleAI.',
  logoutSuccess: 'Logout effettuato.',
  sessionExtended: 'Sessione estesa con successo.',
  exportSuccess: 'Esportazione completata!',
  profileUpdated: 'Profilo aggiornato con successo.',
  passwordChanged: 'Password modificata con successo.',
} as const;

/**
 * Info messages (Italian)
 */
export const infoMessages = {
  exportingChat: 'Esportazione chat in corso...',
  loggingIn: 'Accesso in corso...',
  registering: 'Registrazione in corso...',
  loggingOut: 'Disconnessione in corso...',
  loading: 'Caricamento...',
} as const;
