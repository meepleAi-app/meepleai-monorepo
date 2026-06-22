import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GameDetailHouseRulesList } from '../GameDetailHouseRulesList';

import type { HouseRuleDto } from '@/lib/api/clients/agentMemoryClient';

const labels = {
  title: 'House rules',
  addCta: '+ Aggiungi',
  addPlaceholder: 'Scrivi una regola…',
  addSubmit: 'Salva',
  editLabel: 'Modifica',
  editSubmit: 'Aggiorna',
  cancel: 'Annulla',
  deleteLabel: 'Elimina',
  deleteConfirmTitle: 'Elimina house rule?',
  deleteConfirmMessage: 'Questa azione non è reversibile.',
  deleteConfirm: 'Elimina',
  empty: 'Nessuna regola',
};

function rule(over: Partial<HouseRuleDto> & { id: string }): HouseRuleDto {
  return {
    id: over.id,
    description: over.description ?? 'Regola',
    addedAt: over.addedAt ?? '2026-05-25T00:00:00.000Z',
    source: over.source ?? 'UserAdded',
  };
}

const noop = () => {};

describe('GameDetailHouseRulesList (Issue #1464)', () => {
  it('renders title and one row per rule', () => {
    const rules = [
      rule({ id: '11111111-1111-1111-1111-111111111111', description: 'No backseat gaming' }),
      rule({ id: '22222222-2222-2222-2222-222222222222', description: 'Snacks ok' }),
    ];
    render(
      <GameDetailHouseRulesList
        rules={rules}
        labels={labels}
        onAdd={noop}
        onUpdate={noop}
        onDelete={noop}
      />
    );
    expect(screen.getByRole('heading', { name: 'House rules' })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(2);
    expect(screen.getByText('No backseat gaming')).toBeInTheDocument();
  });

  it('shows empty state when no rules and form closed', () => {
    render(
      <GameDetailHouseRulesList
        rules={[]}
        labels={labels}
        onAdd={noop}
        onUpdate={noop}
        onDelete={noop}
      />
    );
    expect(screen.getByText('Nessuna regola')).toBeInTheDocument();
  });

  it('opens the add form on CTA click and calls onAdd with the trimmed value', () => {
    const onAdd = vi.fn();
    render(
      <GameDetailHouseRulesList
        rules={[]}
        labels={labels}
        onAdd={onAdd}
        onUpdate={noop}
        onDelete={noop}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: '+ Aggiungi' }));
    const input = screen.getByPlaceholderText('Scrivi una regola…') as HTMLTextAreaElement;
    fireEvent.change(input, { target: { value: '  No phones at the table  ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Salva' }));
    expect(onAdd).toHaveBeenCalledWith('No phones at the table');
  });

  it('disables the add submit when description is empty/whitespace', () => {
    render(
      <GameDetailHouseRulesList
        rules={[]}
        labels={labels}
        onAdd={noop}
        onUpdate={noop}
        onDelete={noop}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: '+ Aggiungi' }));
    expect(screen.getByRole('button', { name: 'Salva' })).toBeDisabled();
  });

  it('enters edit mode and calls onUpdate', () => {
    const onUpdate = vi.fn();
    const rules = [rule({ id: '11111111-1111-1111-1111-111111111111', description: 'Old' })];
    render(
      <GameDetailHouseRulesList
        rules={rules}
        labels={labels}
        onAdd={noop}
        onUpdate={onUpdate}
        onDelete={noop}
      />
    );
    fireEvent.click(screen.getByLabelText('Modifica'));
    const input = screen.getByDisplayValue('Old') as HTMLTextAreaElement;
    fireEvent.change(input, { target: { value: 'New' } });
    fireEvent.click(screen.getByRole('button', { name: 'Aggiorna' }));
    expect(onUpdate).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111', 'New');
  });

  it('opens delete confirm and calls onDelete on confirm', () => {
    const onDelete = vi.fn();
    const rules = [rule({ id: 'aaaa1111-1111-1111-1111-111111111111', description: 'X' })];
    render(
      <GameDetailHouseRulesList
        rules={rules}
        labels={labels}
        onAdd={noop}
        onUpdate={noop}
        onDelete={onDelete}
      />
    );
    fireEvent.click(screen.getByLabelText('Elimina'));
    // Confirm modal becomes visible
    expect(screen.getByText('Elimina house rule?')).toBeInTheDocument();
    // The destructive "Elimina" inside the modal — disambiguate by role+name.
    const confirmBtns = screen.getAllByRole('button', { name: 'Elimina' });
    fireEvent.click(confirmBtns[confirmBtns.length - 1]); // the modal CTA is the last one rendered
    expect(onDelete).toHaveBeenCalledWith('aaaa1111-1111-1111-1111-111111111111');
  });
});
