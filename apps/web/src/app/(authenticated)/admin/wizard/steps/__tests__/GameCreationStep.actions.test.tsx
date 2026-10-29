/**
 * @vitest-environment jsdom
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GameCreationStep } from '../GameCreationStep';

// The action row is [← Indietro] [Salta PDF →] [Crea e Continua →]. On a 390px phone it measured 512px, and the page's
// main column clips sideways overflow, so the primary button ("Crea e Continua →") sat mostly off screen with no way
// to reach it. The measurement was taken in a browser (jsdom has no layout); what is pinned here is the class contract
// that lets the row wrap instead.

vi.mock('@/components/layout', () => ({ toast: { error: vi.fn(), success: vi.fn() } }));
vi.mock('@/components/loading', () => ({ Spinner: () => <span data-testid="spinner" /> }));
vi.mock('@/lib/api', () => ({ api: {} }));

function renderStep(props: { allowSkipPdf: boolean }) {
  return render(
    <GameCreationStep
      mode="user"
      allowSkipPdf={props.allowSkipPdf}
      onSkipPdf={props.allowSkipPdf ? vi.fn() : undefined}
      onComplete={vi.fn()}
      onBack={vi.fn()}
    />
  );
}

describe('GameCreationStep — action row at phone width', () => {
  it('lets the row and the button group wrap so the primary button stays on screen', () => {
    renderStep({ allowSkipPdf: true });
    const primary = screen.getByRole('button', { name: /Crea e Continua/ });
    const group = primary.parentElement as HTMLElement;
    const row = group.parentElement as HTMLElement;

    expect(group).toHaveClass('flex', 'flex-wrap');
    expect(row).toHaveClass('flex', 'flex-wrap', 'justify-between');
    // Both the back button and the group live in that row.
    expect(row).toContainElement(screen.getByRole('button', { name: /Indietro/ }));
    expect(group).toContainElement(screen.getByRole('button', { name: /Salta PDF/ }));
  });

  it('wraps the same way when there is no "Salta PDF" button', () => {
    renderStep({ allowSkipPdf: false });
    const primary = screen.getByRole('button', { name: /Crea Gioco/ });
    const group = primary.parentElement as HTMLElement;
    expect(group).toHaveClass('flex-wrap');
    expect(group.parentElement).toHaveClass('flex-wrap');
  });
});
