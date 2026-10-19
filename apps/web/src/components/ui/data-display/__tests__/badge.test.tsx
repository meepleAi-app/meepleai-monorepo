/**
 * #4513 — un solo primitivo Badge, con i token semantici.
 *
 * `@/components/ui/badge` aveva una sua copia del componente con colori Tailwind fissi: la
 * variante `outline` scriveva testo scuro senza sfondo, e dentro la shell admin scura (o in una
 * pagina col tema scuro) non si leggeva. Ora quel modulo riesporta il primitivo canonico.
 *
 * jsdom non applica il CSS di Tailwind, quindi qui si verificano le classi, non il colore
 * calcolato. Il rapporto di contrasto dei token è calcolato nella PR di #4513.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import * as legacy from '@/components/ui/badge';

import * as canonical from '../badge';

const VARIANTS = ['default', 'secondary', 'destructive', 'outline'] as const;

// Colori della palette Tailwind scritti nel primitivo: neutri (quelli del gate
// `local/no-hardcoded-color-utility`) e le tinte che il Badge legacy usava.
const PALETTE_COLOR =
  /(?:^|\s)(?:[a-z-]+:)*(?:bg|text|border)-(?:white|black|(?:slate|gray|zinc|neutral|stone|amber|red)-\d{2,3})(?=\s|$|\/)/;

describe('Badge (#4513)', () => {
  it('il modulo legacy riesporta il primitivo canonico, senza una seconda copia', () => {
    expect(legacy.Badge).toBe(canonical.Badge);
    expect(legacy.badgeVariants).toBe(canonical.badgeVariants);
  });

  it.each(VARIANTS)('la variante %s usa solo token semantici', variant => {
    const classes = canonical.badgeVariants({ variant });

    expect(classes).not.toMatch(PALETTE_COLOR);
  });

  it('outline dentro un contenitore scuro prende colore di testo e bordo dai token', () => {
    render(
      <div data-admin-shell data-theme="dark" className="dark">
        <legacy.Badge variant="outline">Read-only</legacy.Badge>
      </div>
    );

    const badge = screen.getByText('Read-only');
    expect(badge).toHaveClass('text-foreground', 'border-border/50');
    expect(badge.className).not.toMatch(PALETTE_COLOR);
  });
});
