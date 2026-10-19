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
import { backgroundClasses } from '@/test-utils/background-classes';

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

/**
 * #4539 — il fondo passato dal consumatore vince in ogni tema e al passaggio del mouse.
 *
 * Le varianti piene davano il fondo con classi a modificatore (`dark:bg-primary/90`,
 * `hover:bg-primary/80`, `dark:hover:bg-primary`). tailwind-merge le lascia accanto a un
 * `bg-green-600` del consumatore, perché per lui un modificatore diverso è un altro gruppo, e
 * in CSS vincono per specificità: la shell admin, sempre scura, mostrava il fondo della variante.
 */
describe('Badge: fondo del consumatore (#4539)', () => {
  const FILLED = ['default', 'secondary', 'destructive'] as const;

  it.each(FILLED)('%s: in tema scuro resta solo il fondo del consumatore', variant => {
    render(
      <div data-admin-shell data-theme="dark" className="dark">
        <canonical.Badge variant={variant} className="bg-green-600 text-white">
          Pronto
        </canonical.Badge>
      </div>
    );

    expect(backgroundClasses(screen.getByText('Pronto'))).toEqual(['bg-green-600']);
  });

  it.each(FILLED)('%s: al passaggio del mouse resta solo il fondo del consumatore', variant => {
    render(
      <div data-theme="dark" className="dark">
        <canonical.Badge variant={variant} className="bg-green-600 hover:bg-green-700">
          Pronto
        </canonical.Badge>
      </div>
    );

    expect(backgroundClasses(screen.getByText('Pronto'))).toEqual([
      'bg-green-600',
      'hover:bg-green-700',
    ]);
  });

  it.each(FILLED)(
    '%s: un fondo con la sua coppia dark: perde anche lo hover della variante',
    variant => {
      render(
        <canonical.Badge
          variant={variant}
          className="bg-green-100 text-green-800 dark:bg-green-900/30"
        >
          Pronto
        </canonical.Badge>
      );

      expect(backgroundClasses(screen.getByText('Pronto'))).toEqual([
        'bg-green-100',
        'dark:bg-green-900/30',
      ]);
    }
  );

  it.each([
    ['default', 'bg-primary/(--badge-fill)'],
    ['secondary', 'bg-secondary/(--badge-fill)'],
    ['destructive', 'bg-destructive/(--badge-fill)'],
  ] as const)(
    '%s senza fondo del consumatore: le opacità di prima, ora sulla variabile',
    (variant, fill) => {
      render(<canonical.Badge variant={variant}>Stato</canonical.Badge>);

      const badge = screen.getByText('Stato');
      // Prima: bg-<token> · hover:bg-<token>/80 · dark:bg-<token>/90 · dark:hover:bg-<token>.
      expect(backgroundClasses(badge)).toEqual([fill]);
      expect(badge).toHaveClass(
        '[--badge-fill:100%]',
        'hover:[--badge-fill:80%]',
        'dark:[--badge-fill:90%]',
        'dark:hover:[--badge-fill:100%]'
      );
    }
  );
});
