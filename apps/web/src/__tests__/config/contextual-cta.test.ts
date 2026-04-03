/**
 * Contextual CTA Config Tests
 * Verifica label e href per ogni sezione, con focus su /library.
 */

import { describe, it, expect } from 'vitest';

import { getCtaForPathname } from '@/config/contextual-cta';

describe('getCtaForPathname', () => {
  describe('/library', () => {
    it('label è "Esplora Catalogo" (disambiguata da "+ Aggiungi gioco" del page header)', () => {
      expect(getCtaForPathname('/library')?.label).toBe('Esplora Catalogo');
    });

    it('href è "/catalog"', () => {
      expect(getCtaForPathname('/library')?.href).toBe('/catalog');
    });

    it('gradient è definito', () => {
      expect(getCtaForPathname('/library')?.gradient).toBeTruthy();
    });

    it('match anche per sotto-percorsi come /library?tab=wishlist', () => {
      expect(getCtaForPathname('/library?tab=wishlist')?.label).toBe('Esplora Catalogo');
    });
  });

  describe('altre sezioni invariate', () => {
    it('/sessions → label "+ Nuova sessione"', () => {
      expect(getCtaForPathname('/sessions')?.label).toBe('+ Nuova sessione');
    });

    it('/chat → label "+ Nuova chat"', () => {
      expect(getCtaForPathname('/chat')?.label).toBe('+ Nuova chat');
    });

    it('/game-nights → label "+ Organizza serata"', () => {
      expect(getCtaForPathname('/game-nights')?.label).toBe('+ Organizza serata');
    });

    it('/agents → label "+ Nuovo agente"', () => {
      expect(getCtaForPathname('/agents')?.label).toBe('+ Nuovo agente');
    });

    it('/settings → null (nessun CTA definito)', () => {
      expect(getCtaForPathname('/settings')).toBeNull();
    });

    it('/dashboard → null', () => {
      expect(getCtaForPathname('/dashboard')).toBeNull();
    });
  });
});
