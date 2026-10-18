import { describe, it, expect } from 'vitest';
import { getCtaForPathname } from '../contextual-cta';

describe('getCtaForPathname', () => {
  it('returns library CTA for /library', () => {
    const cta = getCtaForPathname('/library');
    expect(cta).not.toBeNull();
    expect(cta!.label).toBe('Esplora Catalogo');
    // #3938: was '/catalog', a route that never existed.
    expect(cta!.href).toBe('/games');
  });

  it('returns library CTA for /library/some-game-id', () => {
    const cta = getCtaForPathname('/library/abc-123');
    expect(cta).not.toBeNull();
    expect(cta!.label).toBe('Esplora Catalogo');
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

  // Issue #4138: the "+ Nuovo agente" CTA is gone with the /agents section, so
  // its test is deleted rather than skipped — a skipped test for a feature that
  // no longer exists is dead weight, not a record. The absence is covered by
  // the null case below and by the static-hrefs gate, which caught that
  // `/agents/new` had never had a route of its own: it only ever resolved by
  // being swallowed into `/agents/[id]`.
  it('returns null for /agents, whose section was retired', () => {
    expect(getCtaForPathname('/agents')).toBeNull();
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
