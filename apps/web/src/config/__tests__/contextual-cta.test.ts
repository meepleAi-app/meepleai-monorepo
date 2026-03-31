import { describe, it, expect } from 'vitest';
import { getCtaForPathname } from '../contextual-cta';

describe('getCtaForPathname', () => {
  it('returns library CTA for /library', () => {
    const cta = getCtaForPathname('/library');
    expect(cta).not.toBeNull();
    expect(cta!.label).toBe('+ Aggiungi gioco');
    expect(cta!.href).toBe('/catalog');
  });

  it('returns library CTA for /library/some-game-id', () => {
    const cta = getCtaForPathname('/library/abc-123');
    expect(cta).not.toBeNull();
    expect(cta!.label).toBe('+ Aggiungi gioco');
  });

  it('returns sessions CTA for /sessions', () => {
    const cta = getCtaForPathname('/sessions');
    expect(cta).not.toBeNull();
    expect(cta!.label).toBe('+ Nuova sessione');
    expect(cta!.href).toBe('/sessions/new');
  });

  it('returns sessions CTA for /sessions/new', () => {
    const cta = getCtaForPathname('/sessions/new');
    expect(cta).not.toBeNull();
    expect(cta!.label).toBe('+ Nuova sessione');
  });

  it('returns chat CTA for /chat', () => {
    const cta = getCtaForPathname('/chat');
    expect(cta).not.toBeNull();
    expect(cta!.label).toBe('+ Nuova chat');
    expect(cta!.href).toBe('/chat');
  });

  it('returns game-nights CTA for /game-nights', () => {
    const cta = getCtaForPathname('/game-nights');
    expect(cta).not.toBeNull();
    expect(cta!.label).toBe('+ Organizza serata');
    expect(cta!.href).toBe('/game-nights/new');
  });

  it('returns agents CTA for /agents', () => {
    const cta = getCtaForPathname('/agents');
    expect(cta).not.toBeNull();
    expect(cta!.label).toBe('+ Nuovo agente');
    expect(cta!.href).toBe('/agents/new');
  });

  it('returns null for /dashboard', () => {
    expect(getCtaForPathname('/dashboard')).toBeNull();
  });

  it('returns null for /settings', () => {
    expect(getCtaForPathname('/settings')).toBeNull();
  });

  it('returns null for /profile', () => {
    expect(getCtaForPathname('/profile')).toBeNull();
  });

  it('returns null for /players', () => {
    expect(getCtaForPathname('/players')).toBeNull();
  });

  it('returns null for /play-records', () => {
    expect(getCtaForPathname('/play-records')).toBeNull();
  });
});
