/**
 * Demo Chat Responses Tests
 * Issue #4781: Verify mock responses cover keywords and are complete
 */

import { describe, it, expect } from 'vitest';

import {
  DEMO_GAME_CHATS,
  getDemoGameChat,
  findDemoResponse,
} from '../demo-chat-responses';
import { DEMO_GAME_IDS } from '../demo-games';

describe('demo-chat-responses', () => {
  // --------------------------------------------------------------------------
  // Structure
  // --------------------------------------------------------------------------

  it('has chat data for all 4 demo games', () => {
    expect(DEMO_GAME_CHATS).toHaveLength(4);
    const gameIds = DEMO_GAME_CHATS.map(c => c.gameId);
    expect(gameIds).toContain(DEMO_GAME_IDS.catan);
    expect(gameIds).toContain(DEMO_GAME_IDS.descent);
    expect(gameIds).toContain(DEMO_GAME_IDS.ticketToRide);
    expect(gameIds).toContain(DEMO_GAME_IDS.pandemic);
  });

  it('each game has 5-10 Q&A responses', () => {
    for (const chat of DEMO_GAME_CHATS) {
      expect(chat.responses.length).toBeGreaterThanOrEqual(5);
      expect(chat.responses.length).toBeLessThanOrEqual(10);
    }
  });

  it('each game has 5 follow-up questions', () => {
    for (const chat of DEMO_GAME_CHATS) {
      expect(chat.followUpQuestions).toHaveLength(5);
    }
  });

  // --------------------------------------------------------------------------
  // Data Quality
  // --------------------------------------------------------------------------

  it('all responses have non-empty question and answer', () => {
    for (const chat of DEMO_GAME_CHATS) {
      for (const r of chat.responses) {
        expect(r.question.length).toBeGreaterThan(10);
        expect(r.answer.length).toBeGreaterThan(50);
      }
    }
  });

  it('all responses have at least 1 keyword', () => {
    for (const chat of DEMO_GAME_CHATS) {
      for (const r of chat.responses) {
        expect(r.keywords.length).toBeGreaterThanOrEqual(1);
      }
    }
  });

  it('all follow-up questions are non-empty', () => {
    for (const chat of DEMO_GAME_CHATS) {
      for (const q of chat.followUpQuestions) {
        expect(q.length).toBeGreaterThan(5);
      }
    }
  });

  // --------------------------------------------------------------------------
  // Keyword Coverage per Game
  // --------------------------------------------------------------------------

  it('Catan covers key topics: commercio, brigante, setup', () => {
    const chat = getDemoGameChat(DEMO_GAME_IDS.catan)!;
    const allKeywords = chat.responses.flatMap(r => r.keywords);
    expect(allKeywords).toContain('commercio');
    expect(allKeywords).toContain('brigante');
    expect(allKeywords).toContain('setup');
    expect(allKeywords).toContain('costruire');
  });

  it('Descent covers key topics: combattimento, movimento, abilità', () => {
    const chat = getDemoGameChat(DEMO_GAME_IDS.descent)!;
    const allKeywords = chat.responses.flatMap(r => r.keywords);
    expect(allKeywords).toContain('combattimento');
    expect(allKeywords).toContain('movimento');
    expect(allKeywords).toContain('abilità');
    expect(allKeywords).toContain('overlord');
  });

  it('Ticket to Ride covers key topics: destinazione, jolly, punti', () => {
    const chat = getDemoGameChat(DEMO_GAME_IDS.ticketToRide)!;
    const allKeywords = chat.responses.flatMap(r => r.keywords);
    expect(allKeywords).toContain('destinazione');
    expect(allKeywords).toContain('jolly');
    expect(allKeywords).toContain('punti');
    expect(allKeywords).toContain('turno');
  });

  it('Pandemic covers key topics: epidemia, ruolo, cura, focolaio', () => {
    const chat = getDemoGameChat(DEMO_GAME_IDS.pandemic)!;
    const allKeywords = chat.responses.flatMap(r => r.keywords);
    expect(allKeywords).toContain('epidemia');
    expect(allKeywords).toContain('ruolo');
    expect(allKeywords).toContain('cura');
    expect(allKeywords).toContain('focolaio');
  });

  // --------------------------------------------------------------------------
  // Helper Functions
  // --------------------------------------------------------------------------

  describe('getDemoGameChat', () => {
    it('returns chat for valid game ID', () => {
      const chat = getDemoGameChat(DEMO_GAME_IDS.catan);
      expect(chat).toBeDefined();
      expect(chat!.gameTitle).toBe('Catan');
    });

    it('returns undefined for unknown game ID', () => {
      expect(getDemoGameChat('unknown-id')).toBeUndefined();
    });
  });

  describe('findDemoResponse', () => {
    it('finds response by keyword match', () => {
      const response = findDemoResponse(DEMO_GAME_IDS.catan, 'come funziona il commercio?');
      expect(response).toBeDefined();
      expect(response!.answer).toContain('Commercio');
    });

    it('finds response with partial keyword', () => {
      const response = findDemoResponse(DEMO_GAME_IDS.pandemic, 'parlami delle epidemie');
      expect(response).toBeDefined();
      expect(response!.answer).toContain('Epidemia');
    });

    it('returns undefined for unmatched query', () => {
      const response = findDemoResponse(DEMO_GAME_IDS.catan, 'xyz123 nonsense query');
      expect(response).toBeUndefined();
    });

    it('returns undefined for unknown game', () => {
      const response = findDemoResponse('unknown-id', 'commercio');
      expect(response).toBeUndefined();
    });

    it('is case-insensitive', () => {
      const response = findDemoResponse(DEMO_GAME_IDS.catan, 'COMMERCIO');
      expect(response).toBeDefined();
    });
  });
});
