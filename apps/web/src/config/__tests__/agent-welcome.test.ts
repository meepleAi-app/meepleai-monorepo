/**
 * Agent Welcome Config Tests
 * Issue #4780: Welcome message per agent typology
 */

import { describe, it, expect } from 'vitest';

import {
  getAgentWelcome,
  buildWelcomeMessage,
  getWelcomeFollowUpQuestions,
} from '../agent-welcome';

describe('agent-welcome config', () => {
  describe('getAgentWelcome', () => {
    it('returns Tutor config', () => {
      const config = getAgentWelcome('Tutor');
      expect(config.greeting).toContain('{game}');
      expect(config.capabilities.length).toBeGreaterThan(0);
      expect(config.followUpQuestions.length).toBe(3);
    });

    it('returns Arbitro config', () => {
      const config = getAgentWelcome('Arbitro');
      expect(config.greeting).toContain('arbitro');
    });

    it('returns Stratega config', () => {
      const config = getAgentWelcome('Stratega');
      expect(config.greeting).toContain('stratega');
    });

    it('returns Narratore config', () => {
      const config = getAgentWelcome('Narratore');
      expect(config.greeting).toContain('Benvenuto');
    });

    it('returns default config for unknown typology', () => {
      const config = getAgentWelcome('unknown');
      expect(config.greeting).toContain('assistente');
    });

    it('returns default config for null', () => {
      const config = getAgentWelcome(null);
      expect(config.greeting).toContain('assistente');
    });

    it('returns default config for undefined', () => {
      const config = getAgentWelcome(undefined);
      expect(config.greeting).toContain('assistente');
    });
  });

  describe('buildWelcomeMessage', () => {
    it('replaces {game} with game name', () => {
      const message = buildWelcomeMessage('Tutor', 'Catan');
      expect(message).toContain('Catan');
      expect(message).not.toContain('{game}');
    });

    it('includes capabilities list', () => {
      const message = buildWelcomeMessage('Tutor', 'Catan');
      expect(message).toContain('Cosa posso fare');
      expect(message).toContain('•');
    });

    it('builds correct Tutor message', () => {
      const message = buildWelcomeMessage('Tutor', 'Catan');
      expect(message).toContain('Ho studiato il regolamento di Catan');
    });

    it('builds correct Arbitro message', () => {
      const message = buildWelcomeMessage('Arbitro', 'Catan');
      expect(message).toContain('arbitro per Catan');
    });

    it('builds correct Stratega message', () => {
      const message = buildWelcomeMessage('Stratega', 'Catan');
      expect(message).toContain('mosse a Catan');
    });

    it('builds correct Narratore message', () => {
      const message = buildWelcomeMessage('Narratore', 'Catan');
      expect(message).toContain('mondo di Catan');
    });
  });

  describe('getWelcomeFollowUpQuestions', () => {
    it('returns 3 questions per typology', () => {
      expect(getWelcomeFollowUpQuestions('Tutor', 'Catan')).toHaveLength(3);
      expect(getWelcomeFollowUpQuestions('Arbitro', 'Catan')).toHaveLength(3);
      expect(getWelcomeFollowUpQuestions('Stratega', 'Catan')).toHaveLength(3);
      expect(getWelcomeFollowUpQuestions('Narratore', 'Catan')).toHaveLength(3);
    });

    it('replaces {game} in questions', () => {
      const questions = getWelcomeFollowUpQuestions('Tutor', 'Catan');
      for (const q of questions) {
        expect(q).not.toContain('{game}');
      }
      expect(questions.some(q => q.includes('Catan'))).toBe(true);
    });

    it('returns default questions for null typology', () => {
      const questions = getWelcomeFollowUpQuestions(null, 'Catan');
      expect(questions).toHaveLength(3);
      expect(questions.some(q => q.includes('Catan'))).toBe(true);
    });
  });
});
