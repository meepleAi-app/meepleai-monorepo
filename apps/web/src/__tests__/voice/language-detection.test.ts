import { describe, expect, it } from 'vitest';

import { detectLanguage } from '@/lib/voice/utils/language-detection';

describe('detectLanguage', () => {
  // ---------------------------------------------------------------
  // Italian detection
  // ---------------------------------------------------------------

  it('detects Italian text with common function words', () => {
    const text = 'Il gioco è per 2-4 giocatori e il turno dura circa 30 minuti';
    expect(detectLanguage(text)).toBe('it-IT');
  });

  it('detects Italian text with diacritical characters', () => {
    const text = 'È un gioco molto più difficile di quello che sembra perché richiede strategia';
    expect(detectLanguage(text)).toBe('it-IT');
  });

  it('detects Italian game rules text', () => {
    const text =
      'Le regole del gioco sono semplici: ogni giocatore pesca una carta dal mazzo e la gioca sul tabellone';
    expect(detectLanguage(text)).toBe('it-IT');
  });

  // ---------------------------------------------------------------
  // English detection
  // ---------------------------------------------------------------

  it('detects English text with common function words', () => {
    const text = 'The game is designed for 2-4 players and each turn takes about 30 minutes';
    expect(detectLanguage(text)).toBe('en-US');
  });

  it('detects English game rules text', () => {
    const text =
      'Players take turns rolling the dice and moving their pieces around the board to score points';
    expect(detectLanguage(text)).toBe('en-US');
  });

  it('detects English text with technical terms', () => {
    const text =
      'This strategy game has been rated as one of the best games of the year by multiple reviewers';
    expect(detectLanguage(text)).toBe('en-US');
  });

  // ---------------------------------------------------------------
  // Edge cases
  // ---------------------------------------------------------------

  it('returns fallback for empty string', () => {
    expect(detectLanguage('')).toBe('it-IT');
  });

  it('returns fallback for whitespace-only string', () => {
    expect(detectLanguage('   ')).toBe('it-IT');
  });

  it('returns custom fallback when provided', () => {
    expect(detectLanguage('', 'en-US')).toBe('en-US');
  });

  it('returns fallback for very short text with no markers', () => {
    expect(detectLanguage('OK')).toBe('it-IT');
  });

  it('handles text with markdown code blocks (strips them)', () => {
    const text =
      'The game uses the following rules:\n```\nif (score > 10) win();\n```\nPlayers should follow these instructions carefully.';
    expect(detectLanguage(text)).toBe('en-US');
  });

  it('handles text with URLs (strips them)', () => {
    const text =
      'Il gioco è disponibile su https://example.com/giochi/strategia per tutti i giocatori';
    expect(detectLanguage(text)).toBe('it-IT');
  });

  it('handles mixed language text — returns dominant language', () => {
    // Predominantly Italian with one English word
    const text =
      'Il game design di questo gioco è molto interessante per i giocatori che amano la strategia';
    expect(detectLanguage(text)).toBe('it-IT');
  });
});
