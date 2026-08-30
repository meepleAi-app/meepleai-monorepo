/**
 * BuilderTable.test.tsx — copertura minima per la migrazione react-table 8 -> 9.
 *
 * `BuilderTable` e' uno dei quattro consumatori di `DataTable` e fino a #3893
 * era l'unico senza alcun test: la migrazione sarebbe stata verificata su due
 * punti di contatto su quattro.
 *
 * Non duplica la meccanica della tabella (quella sta in
 * ui/data-display/__tests__/data-table.test.tsx): verifica che le colonne
 * dichiarate qui — celle custom, badge di stato, menu azioni — continuino a
 * renderizzare e a propagare i callback dopo il cambio di API.
 *
 * Refs: https://github.com/meepleAi-app/meepleai-monorepo/issues/3893
 */

import { describe, it, expect, vi } from 'vitest';

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import type { AgentDefinitionDto } from '@/lib/api/schemas/agent-definitions.schemas';

import { BuilderTable } from '../BuilderTable';

function agente(over: Partial<AgentDefinitionDto> = {}): AgentDefinitionDto {
  return {
    id: '11111111-1111-4111-8111-111111111111',
    name: 'Arbitro Catan',
    description: 'Risponde alle regole di Catan',
    type: '',
    config: { model: 'claude-opus-5', maxTokens: 4000, temperature: 0.2 },
    strategyName: '',
    strategyParameters: {},
    prompts: [],
    tools: [],
    kbCardIds: [],
    chatLanguage: 'auto',
    isActive: true,
    status: 0,
    createdAt: '2026-08-01T10:00:00+00:00',
    updatedAt: null,
    ...over,
  };
}

describe('BuilderTable (#3893)', () => {
  it('renderizza nome, descrizione e modello di ogni agente', () => {
    render(<BuilderTable data={[agente()]} onDelete={vi.fn()} />);

    expect(screen.getByText('Arbitro Catan')).toBeInTheDocument();
    expect(screen.getByText('Risponde alle regole di Catan')).toBeInTheDocument();
    expect(screen.getByText('claude-opus-5')).toBeInTheDocument();
  });

  it('usa il fallback quando la descrizione e vuota', () => {
    render(<BuilderTable data={[agente({ description: '' })]} onDelete={vi.fn()} />);

    expect(screen.getByText('Nessuna descrizione')).toBeInTheDocument();
  });

  it('mostra il badge di stato corrispondente', () => {
    const { rerender } = render(<BuilderTable data={[agente({ status: 0 })]} onDelete={vi.fn()} />);
    expect(screen.getByText('Bozza')).toBeInTheDocument();

    rerender(<BuilderTable data={[agente({ status: 1 })]} onDelete={vi.fn()} />);
    expect(screen.getByText('In Test')).toBeInTheDocument();

    rerender(<BuilderTable data={[agente({ status: 2, isActive: true })]} onDelete={vi.fn()} />);
    expect(screen.getByText('Pubblicato')).toBeInTheDocument();
    expect(screen.getByText('Attivo')).toBeInTheDocument();
  });

  it('renderizza una riga per agente', () => {
    render(
      <BuilderTable
        data={[
          agente(),
          agente({ id: '22222222-2222-4222-8222-222222222222', name: 'Arbitro Brass' }),
        ]}
        onDelete={vi.fn()}
      />
    );

    // header + 2 righe
    expect(screen.getAllByRole('row')).toHaveLength(3);
    expect(screen.getByText('Arbitro Brass')).toBeInTheDocument();
  });

  it('propaga onDelete dal menu azioni', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();
    render(<BuilderTable data={[agente()]} onDelete={onDelete} />);

    await user.click(screen.getByRole('button'));
    await user.click(await screen.findByText('Elimina'));

    expect(onDelete).toHaveBeenCalledWith('11111111-1111-4111-8111-111111111111');
  });

  it('offre Avvia Test solo sugli agenti in bozza', async () => {
    const user = userEvent.setup();
    const onStartTesting = vi.fn();
    render(
      <BuilderTable
        data={[agente({ status: 0 })]}
        onDelete={vi.fn()}
        onStartTesting={onStartTesting}
      />
    );

    await user.click(screen.getByRole('button'));
    await user.click(await screen.findByText('Avvia Test'));

    expect(onStartTesting).toHaveBeenCalledWith('11111111-1111-4111-8111-111111111111');
  });
});
