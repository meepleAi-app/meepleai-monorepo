import { describe, it, expect } from 'vitest';
import { resolveCTA } from '@/lib/utils/nav-cta';

describe('resolveCTA', () => {
  it('returns aggiungi gioco CTA for /dashboard', () => {
    const cta = resolveCTA('/dashboard');
    expect(cta).toEqual({ label: '+ Aggiungi gioco', href: '/library?action=add' });
  });

  it('returns aggiungi CTA for /library', () => {
    expect(resolveCTA('/library')).toEqual({ label: '+ Aggiungi', href: '/library?action=add' });
  });

  it('returns aggiungi CTA for /library sub-path', () => {
    expect(resolveCTA('/library/123')).toEqual({
      label: '+ Aggiungi',
      href: '/library?action=add',
    });
  });

  it('returns nuova sessione CTA for /games/:id', () => {
    const cta = resolveCTA('/games/abc123');
    expect(cta).toEqual({ label: '▶ Nuova sessione', href: '/games/abc123/sessions/new' });
  });

  it('returns nuova sessione CTA for /games/:id/sessions', () => {
    const cta = resolveCTA('/games/abc123/sessions');
    expect(cta).toEqual({ label: '▶ Nuova sessione', href: '/games/abc123/sessions/new' });
  });

  it('returns carica PDF CTA for /games/:id/kb', () => {
    const cta = resolveCTA('/games/abc123/kb');
    expect(cta).toEqual({ label: '↑ Carica PDF', href: '/games/abc123/kb/upload' });
  });

  it('returns chat CTA for /agents/:id', () => {
    const cta = resolveCTA('/agents/abc123');
    expect(cta).toEqual({ label: '💬 Inizia chat', href: '/agents/abc123/chat' });
  });

  it('returns nuova sessione CTA for /sessions', () => {
    expect(resolveCTA('/sessions')).toEqual({ label: '▶ Nuova sessione', href: '/sessions/new' });
  });

  it('returns null for unknown routes', () => {
    expect(resolveCTA('/unknown')).toBeNull();
    expect(resolveCTA('/')).toBeNull();
    expect(resolveCTA('/toolkit')).toBeNull();
  });

  it('does not match /library-export or similar false positives', () => {
    expect(resolveCTA('/library-export')).toBeNull();
  });

  it('does not match /sessions-archive or similar false positives', () => {
    expect(resolveCTA('/sessions-archive')).toBeNull();
  });
});
