/**
 * Tests for i18n Error Utilities (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Error localization, status code mapping, Italian messages
 */

import { getLocalizedError, validationMessages, successMessages, infoMessages } from '../errors';

describe('i18n errors', () => {
  describe('getLocalizedError', () => {
    it('should handle network errors (no status code)', () => {
      const result = getLocalizedError(undefined);

      expect(result).toEqual({
        type: 'network',
        message: 'Impossibile connettersi al server. Verifica la tua connessione.',
        field: undefined,
      });
    });

    it('should handle 400 validation errors', () => {
      const result = getLocalizedError(400);

      expect(result).toEqual({
        type: 'validation',
        message: 'Dati non validi. Controlla i campi inseriti.',
        field: undefined,
      });
    });

    it('should use custom message for 400', () => {
      const result = getLocalizedError(400, 'Custom validation error');

      expect(result.message).toBe('Custom validation error');
      expect(result.type).toBe('validation');
    });

    it('should handle 401 with invalid credentials', () => {
      const result = getLocalizedError(401);

      expect(result).toEqual({
        type: 'auth',
        message: 'Email o password non corretti.',
        field: undefined,
      });
    });

    it('should detect session expiration in 401', () => {
      const result = getLocalizedError(401, 'Session expired');

      expect(result).toMatchObject({
        type: 'auth',
        message: expect.stringContaining('Sessione scaduta'),
      });
    });

    it('should detect Italian session expiration in 401', () => {
      const result = getLocalizedError(401, 'Sessione scaduta');

      expect(result).toMatchObject({
        type: 'auth',
        message: expect.stringContaining('Sessione scaduta'),
      });
    });

    it('should handle 403 forbidden', () => {
      const result = getLocalizedError(403);

      expect(result).toEqual({
        type: 'forbidden',
        message: 'Accesso negato. Non hai i permessi necessari.',
        field: undefined,
      });
    });

    it('should handle 409 conflict', () => {
      const result = getLocalizedError(409);

      expect(result.type).toBe('conflict');
      expect(result.message).toContain('Conflitto');
    });

    it('should detect email conflict in 409', () => {
      const result = getLocalizedError(409, 'Email already exists', 'email');

      expect(result).toEqual({
        type: 'conflict',
        message: "Questa email è già registrata. Prova con un'altra email o effettua il login.",
        field: 'email',
      });
    });

    it('should detect email in error message for 409', () => {
      const result = getLocalizedError(409, 'User with email already exists');

      expect(result.message).toContain('email è già registrata');
      expect(result.field).toBe('email');
    });

    it('should handle 422 validation', () => {
      const result = getLocalizedError(422);

      expect(result).toEqual({
        type: 'validation',
        message: 'Dati non validi. Controlla il formato dei campi.',
        field: undefined,
      });
    });

    it('should handle 500 server error', () => {
      const result = getLocalizedError(500);

      expect(result).toEqual({
        type: 'server',
        message: 'Errore del server. Riprova più tardi.',
        field: undefined,
      });
    });

    it('should handle 502 bad gateway', () => {
      const result = getLocalizedError(502);

      expect(result.type).toBe('server');
      expect(result.message).toContain('Errore del server');
    });

    it('should handle 503 service unavailable', () => {
      const result = getLocalizedError(503);

      expect(result.type).toBe('server');
    });

    it('should handle 504 gateway timeout', () => {
      const result = getLocalizedError(504);

      expect(result.type).toBe('server');
    });

    it('should handle unknown status codes', () => {
      const result = getLocalizedError(418); // I'm a teapot

      expect(result.type).toBe('server');
      expect(result.message).toBe('Si è verificato un errore imprevisto.');
    });

    it('should use custom message for unknown codes', () => {
      const result = getLocalizedError(999, 'Custom error');

      expect(result.message).toBe('Custom error');
    });

    it('should preserve field parameter', () => {
      const result = getLocalizedError(400, 'Invalid email', 'email');

      expect(result.field).toBe('email');
    });
  });

  describe('validationMessages', () => {
    it('should have email validation messages', () => {
      expect(validationMessages.emailRequired).toBe("L'email è obbligatoria.");
      expect(validationMessages.emailInvalid).toBe('Inserisci un indirizzo email valido.');
    });

    it('should have password validation messages', () => {
      expect(validationMessages.passwordRequired).toBe('La password è obbligatoria.');
      expect(validationMessages.passwordTooShort).toBe(
        'La password deve contenere almeno 8 caratteri.'
      );
      expect(validationMessages.passwordTooWeak).toBeTruthy();
      expect(validationMessages.passwordsDoNotMatch).toBe('Le password non corrispondono.');
    });

    it('should have display name validation messages', () => {
      expect(validationMessages.displayNameRequired).toBeTruthy();
      expect(validationMessages.displayNameTooShort).toBeTruthy();
      expect(validationMessages.displayNameTooLong).toBeTruthy();
    });

    it('should have terms validation message', () => {
      expect(validationMessages.termsRequired).toBe('Devi accettare i termini e condizioni.');
    });
  });

  describe('successMessages', () => {
    it('should have auth success messages', () => {
      expect(successMessages.loginSuccess).toBe('Login effettuato con successo!');
      expect(successMessages.registrationSuccess).toContain('Benvenuto');
      expect(successMessages.logoutSuccess).toBe('Logout effettuato.');
    });

    it('should have profile success messages', () => {
      expect(successMessages.profileUpdated).toBeTruthy();
      expect(successMessages.passwordChanged).toBeTruthy();
    });

    it('should have session success message', () => {
      expect(successMessages.sessionExtended).toBeTruthy();
    });

    it('should have export success message', () => {
      expect(successMessages.exportSuccess).toBe('Esportazione completata!');
    });
  });

  describe('infoMessages', () => {
    it('should have auth info messages', () => {
      expect(infoMessages.loggingIn).toBe('Accesso in corso...');
      expect(infoMessages.registering).toBe('Registrazione in corso...');
      expect(infoMessages.loggingOut).toBe('Disconnessione in corso...');
    });

    it('should have export info message', () => {
      expect(infoMessages.exportingChat).toBe('Esportazione chat in corso...');
    });

    it('should have generic loading message', () => {
      expect(infoMessages.loading).toBe('Caricamento...');
    });
  });
});
