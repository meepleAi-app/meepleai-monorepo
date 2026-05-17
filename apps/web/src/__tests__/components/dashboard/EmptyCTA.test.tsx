import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { EmptyCTA } from '@/components/dashboard/EmptyCTA';

describe('EmptyCTA', () => {
  it('renders icon, title, sub, primary action and secondary action', () => {
    render(
      <EmptyCTA
        entity="session"
        icon="🎯"
        title="Nessuna sessione"
        sub="Crea una nuova partita per iniziare."
        actions={[
          { label: 'Crea sessione', href: '/sessions/new', primary: true },
          { label: 'Sfoglia', href: '/sessions' },
        ]}
      />
    );
    expect(screen.getByText('Nessuna sessione')).toBeInTheDocument();
    expect(screen.getByText('Crea una nuova partita per iniziare.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Crea sessione/i })).toHaveAttribute(
      'href',
      '/sessions/new'
    );
    expect(screen.getByRole('link', { name: /Sfoglia/i })).toHaveAttribute('href', '/sessions');
  });

  it('icon has aria-hidden=true', () => {
    render(<EmptyCTA entity="game" icon="🎲" title="Empty" sub="x" actions={[]} />);
    const icon = screen.getByText('🎲');
    expect(icon).toHaveAttribute('aria-hidden', 'true');
  });

  it('wrapper has role=status', () => {
    render(<EmptyCTA entity="game" icon="🎲" title="Empty" sub="x" actions={[]} />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('does not contain hardcoded amber color classes (DS-15 regression guard)', () => {
    const { container } = render(
      <EmptyCTA
        entity="agent"
        icon="🤖"
        title="No agents"
        sub="x"
        actions={[{ label: 'Chat', href: '/chat', primary: true }]}
      />
    );
    const html = container.innerHTML;
    expect(html).not.toMatch(/bg-amber-\d/);
    expect(html).not.toMatch(/border-amber-\d/);
    expect(html).not.toMatch(/text-amber-\d/);
    expect(html).not.toMatch(/rgba\(180,130,80/);
  });

  it('applies entity data attribute on wrapper', () => {
    const { container } = render(
      <EmptyCTA entity="session" icon="🎯" title="x" sub="x" actions={[]} />
    );
    expect(container.querySelector('[data-entity="session"]')).toBeInTheDocument();
  });
});
