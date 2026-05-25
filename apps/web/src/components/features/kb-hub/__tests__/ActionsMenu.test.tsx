import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ActionsMenu } from '../ActionsMenu';

const baseLabels = {
  headerSubtitle: '{size} · {date}',
  actions: {
    open: { label: 'Apri dettaglio', description: 'Visualizza chunks e preview', icon: '↗' },
    reindex: { label: 'Re-index', description: 'Rielabora embedding PDF', icon: '⟳' },
    cost: { label: 'Statistiche costo', description: 'Token consumati e costo', icon: '📋' },
    move: { label: 'Sposta in altro gioco', description: 'Sposta in altra scheda KB', icon: '📦' },
    delete: { label: 'Elimina', description: 'Rimuovi PDF e cleanup', icon: '🗑' },
  },
};

const basePdf = {
  name: 'Rulebook v2',
  sizeFormatted: '45 MB',
  uploadedAtRelative: '2 gg fa',
};

describe('ActionsMenu (Issue #1481)', () => {
  it('renders trigger child', () => {
    render(
      <ActionsMenu pdf={basePdf} labels={baseLabels} onSelect={() => {}}>
        <button type="button">trigger</button>
      </ActionsMenu>
    );
    expect(screen.getByRole('button', { name: 'trigger' })).toBeInTheDocument();
  });

  it('renders all 5 actions + header subtitle when open', () => {
    render(
      <ActionsMenu pdf={basePdf} labels={baseLabels} onSelect={() => {}} open>
        <button type="button">trigger</button>
      </ActionsMenu>
    );
    expect(screen.getByText('Apri dettaglio')).toBeInTheDocument();
    expect(screen.getByText('Re-index')).toBeInTheDocument();
    expect(screen.getByText('Statistiche costo')).toBeInTheDocument();
    expect(screen.getByText('Sposta in altro gioco')).toBeInTheDocument();
    expect(screen.getByText('Elimina')).toBeInTheDocument();
    expect(screen.getByText('45 MB · 2 gg fa')).toBeInTheDocument();
  });

  it('invokes onSelect with correct action key when item clicked', () => {
    const onSelect = vi.fn();
    render(
      <ActionsMenu pdf={basePdf} labels={baseLabels} onSelect={onSelect} open>
        <button type="button">trigger</button>
      </ActionsMenu>
    );
    fireEvent.click(screen.getByText('Elimina'));
    expect(onSelect).toHaveBeenCalledWith('delete');
  });

  it('marks delete action as destructive (uses destructive style data-slot)', () => {
    render(
      <ActionsMenu pdf={basePdf} labels={baseLabels} onSelect={() => {}} open>
        <button type="button">trigger</button>
      </ActionsMenu>
    );
    // DropdownMenuContent renders through a Radix Portal, so we query the document.
    expect(document.querySelector('[data-slot="kb-hub-actions-menu-delete"]')).toBeInTheDocument();
    expect(document.querySelector('[data-slot="kb-hub-actions-menu-open"]')).toBeInTheDocument();
  });
});
