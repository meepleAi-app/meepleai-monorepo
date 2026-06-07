/**
 * StatePreviewToggle — unit tests.
 *
 * Asse B WP5 T5 (Issue #1897). Validates the toggle UI + DEC-4 prod guard
 * (renders null in production, zero DOM impact).
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { StatePreviewProvider } from '../state-preview-provider';
import { StatePreviewToggle } from '../state-preview-toggle';

function renderToggle(pageId = 'library') {
  return render(
    <StatePreviewProvider>
      <StatePreviewToggle pageId={pageId} />
    </StatePreviewProvider>
  );
}

describe('StatePreviewToggle (dev mode)', () => {
  beforeEach(() => {
    vi.stubEnv('NODE_ENV', 'development');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('renders a select with all 5 STATE options', () => {
    renderToggle();
    const select = screen.getByRole('combobox', { name: /preview state/i });
    expect(select).toBeInTheDocument();

    const options = screen.getAllByRole('option');
    expect(options).toHaveLength(5);
    expect(options.map(o => o.textContent)).toEqual([
      'default',
      'empty',
      'loading',
      'error',
      'offline',
    ]);
  });

  it('onChange updates the displayed value via setStateFor', async () => {
    const user = userEvent.setup();
    renderToggle();
    const select = screen.getByRole('combobox', { name: /preview state/i }) as HTMLSelectElement;

    expect(select.value).toBe('default');

    await user.selectOptions(select, 'loading');
    expect(select.value).toBe('loading');

    await user.selectOptions(select, 'offline');
    expect(select.value).toBe('offline');
  });

  it('has a properly associated label (a11y)', () => {
    renderToggle('games');
    const label = screen.getByText(/preview state/i);
    const select = screen.getByRole('combobox', { name: /preview state/i });

    // htmlFor / id wiring
    expect(label).toHaveAttribute('id', 'state-preview-label-games');
    expect(label).toHaveAttribute('for', 'state-preview-select-games');
    expect(select).toHaveAttribute('id', 'state-preview-select-games');
    expect(select).toHaveAttribute('aria-labelledby', 'state-preview-label-games');
  });

  it('select is keyboard-focusable', async () => {
    const user = userEvent.setup();
    renderToggle();
    const select = screen.getByRole('combobox', { name: /preview state/i });

    await user.tab();
    expect(select).toHaveFocus();
  });
});

describe('StatePreviewToggle (production mode — DEC-4 prod guard)', () => {
  beforeEach(() => {
    vi.stubEnv('NODE_ENV', 'production');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('renders null — no DOM impact', () => {
    const { container } = renderToggle();
    expect(container.firstChild).toBeNull();
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.queryByTestId('state-preview-toggle')).not.toBeInTheDocument();
  });
});
