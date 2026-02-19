/**
 * Demo Agent Suggestions Tests
 * Issue #4781: Verify agent typology suggestions per game
 */

import { describe, it, expect } from 'vitest';

import {
  DEMO_AGENT_SUGGESTIONS,
  getAgentSuggestionsForGame,
  getPrimarySuggestion,
} from '../demo-agent-suggestions';
import { DEMO_GAME_IDS } from '../demo-games';

const VALID_TYPOLOGIES = ['Tutor', 'Arbitro', 'Stratega', 'Narratore'];

describe('demo-agent-suggestions', () => {
  // --------------------------------------------------------------------------
  // Structure
  // --------------------------------------------------------------------------

  it('has suggestions for all 4 demo games', () => {
    expect(DEMO_AGENT_SUGGESTIONS).toHaveLength(4);
    const gameIds = DEMO_AGENT_SUGGESTIONS.map(s => s.gameId);
    expect(gameIds).toContain(DEMO_GAME_IDS.catan);
    expect(gameIds).toContain(DEMO_GAME_IDS.descent);
    expect(gameIds).toContain(DEMO_GAME_IDS.ticketToRide);
    expect(gameIds).toContain(DEMO_GAME_IDS.pandemic);
  });

  it('each game has at least 2 suggestions', () => {
    for (const entry of DEMO_AGENT_SUGGESTIONS) {
      expect(entry.suggestions.length).toBeGreaterThanOrEqual(2);
    }
  });

  it('each game has exactly 1 primary suggestion', () => {
    for (const entry of DEMO_AGENT_SUGGESTIONS) {
      const primaryCount = entry.suggestions.filter(s => s.isPrimary).length;
      expect(primaryCount).toBe(1);
    }
  });

  it('all typologies are valid', () => {
    for (const entry of DEMO_AGENT_SUGGESTIONS) {
      for (const suggestion of entry.suggestions) {
        expect(VALID_TYPOLOGIES).toContain(suggestion.typology);
      }
    }
  });

  it('all suggestions have non-empty reasons', () => {
    for (const entry of DEMO_AGENT_SUGGESTIONS) {
      for (const suggestion of entry.suggestions) {
        expect(suggestion.reason.length).toBeGreaterThan(10);
      }
    }
  });

  // --------------------------------------------------------------------------
  // Game-Specific
  // --------------------------------------------------------------------------

  it('Catan primary is Tutor', () => {
    expect(getPrimarySuggestion(DEMO_GAME_IDS.catan)).toBe('Tutor');
  });

  it('Descent primary is Arbitro', () => {
    expect(getPrimarySuggestion(DEMO_GAME_IDS.descent)).toBe('Arbitro');
  });

  it('Ticket to Ride primary is Tutor', () => {
    expect(getPrimarySuggestion(DEMO_GAME_IDS.ticketToRide)).toBe('Tutor');
  });

  it('Pandemic primary is Stratega', () => {
    expect(getPrimarySuggestion(DEMO_GAME_IDS.pandemic)).toBe('Stratega');
  });

  // --------------------------------------------------------------------------
  // Helpers
  // --------------------------------------------------------------------------

  it('getAgentSuggestionsForGame returns correct data', () => {
    const result = getAgentSuggestionsForGame(DEMO_GAME_IDS.catan);
    expect(result).toBeDefined();
    expect(result!.gameTitle).toBe('Catan');
    expect(result!.suggestions.length).toBeGreaterThanOrEqual(2);
  });

  it('getAgentSuggestionsForGame returns undefined for unknown game', () => {
    expect(getAgentSuggestionsForGame('unknown-id')).toBeUndefined();
  });

  it('getPrimarySuggestion returns undefined for unknown game', () => {
    expect(getPrimarySuggestion('unknown-id')).toBeUndefined();
  });
});
