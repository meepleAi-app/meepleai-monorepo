/**
 * Button component tests - coverage increment (frontend)
 */

import type { ReactNode } from 'react';

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { backgroundClasses } from '@/test-utils/background-classes';

import { Button, buttonVariants } from '../primitives/button';

describe('Button', () => {
  it('renders a native button by default with default sizing', () => {
    render(<Button>Click me</Button>);

    const btn = screen.getByRole('button', { name: 'Click me' });
    expect(btn).toBeInTheDocument();
    expect(btn).toHaveClass('h-11', 'px-6');
    expect(btn).toHaveClass('bg-primary/(--btn-fill)');
  });

  it('applies variant and size classes', () => {
    render(
      <Button variant="destructive" size="sm">
        Delete
      </Button>
    );

    const btn = screen.getByRole('button', { name: 'Delete' });
    expect(btn).toHaveClass('bg-destructive/(--btn-fill)');
    expect(btn).toHaveClass('h-9');
  });

  it('renders as child element when asChild is true', () => {
    render(
      <Button asChild>
        <a href="/docs">Docs</a>
      </Button>
    );

    const link = screen.getByRole('link', { name: 'Docs' });
    expect(link).toHaveAttribute('href', '/docs');
    expect(link.tagName.toLowerCase()).toBe('a');
  });

  it('merges custom className with variant styles', () => {
    const className = 'custom-shadow';
    render(
      <Button className={className} variant="secondary">
        Custom
      </Button>
    );

    const btn = screen.getByRole('button', { name: 'Custom' });
    expect(btn).toHaveClass('custom-shadow');
    expect(btn).toHaveClass('bg-secondary/(--btn-fill)');
  });

  it('buttonVariants produces expected classes', () => {
    const classes = buttonVariants({ variant: 'link', size: 'icon' });
    expect(classes).toContain('underline-offset-4');
    expect(classes).toContain('h-11');
    expect(classes).toContain('w-11');
  });
});

/**
 * #4548 — il fondo (e il testo di `link`) passato dal consumatore vince in ogni tema e al
 * passaggio del mouse.
 *
 * Le varianti davano il colore anche con classi a modificatore (`dark:bg-primary/90`,
 * `hover:bg-primary/90`, `dark:hover:bg-primary`, `dark:bg-card`, `dark:hover:bg-muted/70`,
 * `dark:text-primary/90`, …). tailwind-merge le lascia accanto a un `bg-green-600` del
 * consumatore, perché per lui un modificatore diverso è un altro gruppo, e in CSS vincono per
 * specificità: col tema scuro, e sempre nella console admin, si vedeva il colore della variante.
 */
describe('Button: colore del consumatore (#4548)', () => {
  const FILLED = ['default', 'destructive', 'secondary', 'outline', 'ghost'] as const;

  /** Le classi di colore del testo rimaste, con qualsiasi modificatore (non le dimensioni). */
  function textColorClasses(element: Element): string[] {
    return Array.from(element.classList)
      .filter(token => /(?:^|:)text-/.test(token))
      .filter(token => !/(?:^|:)text-(xs|sm|base|lg|[2-9]?xl)$/.test(token))
      .sort();
  }

  function renderDark(node: ReactNode) {
    return render(
      <div data-admin-shell data-theme="dark" className="dark">
        {node}
      </div>
    );
  }

  it.each(FILLED)('%s: in tema scuro resta solo il fondo del consumatore', variant => {
    renderDark(
      <Button variant={variant} className="bg-green-600 text-white">
        Salva
      </Button>
    );

    expect(backgroundClasses(screen.getByRole('button', { name: 'Salva' }))).toEqual([
      'bg-green-600',
    ]);
  });

  it.each(FILLED)('%s: fondo e hover del consumatore, nessun altro fondo', variant => {
    renderDark(
      <Button variant={variant} className="bg-green-600 hover:bg-green-700 text-white">
        Salva
      </Button>
    );

    expect(backgroundClasses(screen.getByRole('button', { name: 'Salva' }))).toEqual([
      'bg-green-600',
      'hover:bg-green-700',
    ]);
  });

  it.each(FILLED)('%s: uno hover proprio, senza fondo, non è battuto in tema scuro', variant => {
    renderDark(
      <Button variant={variant} className="hover:bg-red-50 hover:text-red-700">
        Elimina
      </Button>
    );

    // Il fondo a riposo della variante resta (è senza modificatori, quindi perde contro lo
    // hover del consumatore); nessuna classe di fondo con modificatore deve venire dalla variante.
    const withModifier = backgroundClasses(screen.getByRole('button', { name: 'Elimina' })).filter(
      token => token.includes(':')
    );
    expect(withModifier).toEqual(['hover:bg-red-50']);
  });

  it('link: il testo del consumatore vince in ogni tema e in hover', () => {
    renderDark(
      <Button variant="link" className="text-red-600 hover:text-red-700">
        Dettagli
      </Button>
    );

    expect(textColorClasses(screen.getByRole('button', { name: 'Dettagli' }))).toEqual([
      'hover:text-red-700',
      'text-red-600',
    ]);
  });

  it.each([
    [
      'default',
      'bg-primary/(--btn-fill)',
      [
        '[--btn-fill:100%]',
        'hover:[--btn-fill:90%]',
        'dark:[--btn-fill:90%]',
        'dark:hover:[--btn-fill:100%]',
      ],
    ],
    [
      'destructive',
      'bg-destructive/(--btn-fill)',
      [
        '[--btn-fill:100%]',
        'hover:[--btn-fill:90%]',
        'dark:[--btn-fill:90%]',
        'dark:hover:[--btn-fill:100%]',
      ],
    ],
    [
      'secondary',
      'bg-secondary/(--btn-fill)',
      [
        '[--btn-fill:100%]',
        'hover:[--btn-fill:85%]',
        'dark:[--btn-fill:90%]',
        'dark:hover:[--btn-fill:100%]',
      ],
    ],
    [
      'outline',
      'bg-(--btn-tone)/(--btn-fill)',
      [
        '[--btn-tone:hsl(var(--card))]',
        '[--btn-fill:90%]',
        'hover:[--btn-tone:hsl(var(--muted))]',
        'hover:[--btn-fill:100%]',
        'dark:[--btn-fill:100%]',
        'dark:hover:[--btn-tone:hsl(var(--card))]',
      ],
    ],
    [
      'ghost',
      'bg-(--btn-tone)/(--btn-fill)',
      [
        '[--btn-tone:transparent]',
        '[--btn-fill:100%]',
        'hover:[--btn-tone:hsl(var(--muted))]',
        'dark:hover:[--btn-fill:70%]',
      ],
    ],
  ] as const)(
    '%s senza fondo del consumatore: gli stessi fondi di prima, ora sulle variabili',
    (variant, fill, vars) => {
      render(<Button variant={variant}>Azione</Button>);

      const button = screen.getByRole('button', { name: 'Azione' });
      expect(backgroundClasses(button)).toEqual([fill]);
      expect(button).toHaveClass(...vars);
    }
  );

  it('link senza testo del consumatore: le opacità di prima, ora sulla variabile', () => {
    render(<Button variant="link">Dettagli</Button>);

    const button = screen.getByRole('button', { name: 'Dettagli' });
    expect(textColorClasses(button)).toEqual(['text-primary/(--btn-ink)']);
    expect(button).toHaveClass(
      '[--btn-ink:100%]',
      'hover:[--btn-ink:80%]',
      'dark:[--btn-ink:90%]',
      'dark:hover:[--btn-ink:100%]'
    );
  });

  it.each([...FILLED, 'link'] as const)(
    '%s: focus, active, disabled e lo hover senza colore restano, anche col fondo del consumatore',
    variant => {
      render(
        <Button variant={variant} className="bg-green-600 hover:bg-green-700 text-white">
          Salva
        </Button>
      );

      expect(screen.getByRole('button', { name: 'Salva' })).toHaveClass(
        'focus-visible:ring-2',
        'focus-visible:ring-ring',
        'focus-visible:ring-offset-2',
        'disabled:opacity-50',
        'disabled:pointer-events-none',
        'active:translate-y-0',
        'hover:-translate-y-0.5',
        'hover:shadow-md'
      );
    }
  );
});
