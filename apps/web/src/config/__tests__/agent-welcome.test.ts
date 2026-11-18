/**
 * Agent Welcome Config Tests
 * Issue #4780: Welcome message
 *
 * #5001 (ADR-098 §5): one welcome for every intent, about the rules. The same for `auto`, `tutor`, `secca` and no
 * intent is checked through the chat page in `ChatThreadView.test.tsx`; here, what it says.
 */

import { describe, it, expect } from 'vitest';

import {
  getAgentWelcome,
  buildWelcomeMessage,
  getWelcomeFollowUpQuestions,
} from '../agent-welcome';

describe('agent-welcome config', () => {
  describe('the welcome (#5001)', () => {
    it('promises neither strategy nor arbitration, in any of its parts', () => {
      const config = getAgentWelcome();
      for (const text of [config.greeting, ...config.capabilities, ...config.followUpQuestions]) {
        expect(text).not.toMatch(/strateg|arbitr|disput|mosse/i);
      }
    });

    it('talks about the rules of the game', () => {
      const config = getAgentWelcome();
      expect(config.greeting).toContain('{game}');
      expect(config.greeting).toMatch(/regol/i);
      expect(config.capabilities.length).toBeGreaterThan(0);
      expect(config.followUpQuestions).toHaveLength(3);
    });
  });

  describe('buildWelcomeMessage', () => {
    it('replaces {game} with game name', () => {
      const message = buildWelcomeMessage('Catan');
      expect(message).toContain('Catan');
      expect(message).not.toContain('{game}');
    });

    it('inserts a game title containing $ sequences verbatim', () => {
      // #3226 regression: gameName was passed as the replacement string, so $$ / $& were interpreted.
      const message = buildWelcomeMessage('A$$B $& Co');
      expect(message).toContain('A$$B $& Co');
      expect(message).not.toContain('{game}');
    });

    it('includes capabilities list', () => {
      const message = buildWelcomeMessage('Catan');
      expect(message).toContain('Cosa posso fare');
      expect(message).toContain('•');
    });
  });

  describe('getWelcomeFollowUpQuestions', () => {
    it('replaces {game} in questions', () => {
      const questions = getWelcomeFollowUpQuestions('Catan');
      expect(questions).toHaveLength(3);
      for (const q of questions) {
        expect(q).not.toContain('{game}');
      }
      expect(questions.some(q => q.includes('Catan'))).toBe(true);
    });

    it('inserts a game title containing $ sequences verbatim', () => {
      // #3226 regression.
      const questions = getWelcomeFollowUpQuestions('A$$B $& Co');
      for (const q of questions) {
        expect(q).not.toContain('{game}');
      }
      expect(questions.some(q => q.includes('A$$B $& Co'))).toBe(true);
    });
  });
});
