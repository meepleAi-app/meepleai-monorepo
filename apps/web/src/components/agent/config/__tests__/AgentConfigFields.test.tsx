/**
 * AgentConfigFields — shared per-game AI config field group (Issue #2732).
 *
 * Controlled presentational component reused by AgentConfigModal (library) and
 * the /agents/[id] Settings tab. Tests cover: all six fields render, onChange
 * emits the right patch for native inputs, and `disabled` propagates.
 */
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { AgentConfigFields, type AgentConfigFieldsValue } from '../AgentConfigFields';

const baseValue: AgentConfigFieldsValue = {
  llmModel: 'llama-3.3-70b-free',
  temperature: 0.7,
  maxTokens: 4096,
  personality: 'Amichevole',
  detailLevel: 'Normale',
  personalNotes: 'ciao',
};

describe('AgentConfigFields', () => {
  it('renders all six config field groups', () => {
    render(<AgentConfigFields value={baseValue} onChange={vi.fn()} />);

    expect(screen.getByText(/Modello AI/)).toBeInTheDocument();
    expect(screen.getByText(/Temperatura/)).toBeInTheDocument();
    expect(screen.getByText(/Max Tokens/)).toBeInTheDocument();
    expect(screen.getByText(/Personalità Agente/)).toBeInTheDocument();
    expect(screen.getByText(/Livello Dettaglio/)).toBeInTheDocument();
    expect(screen.getByText(/Note Personali/)).toBeInTheDocument();
    expect(screen.getByDisplayValue('ciao')).toBeInTheDocument();
  });

  it('emits onChange patch when maxTokens changes', () => {
    const onChange = vi.fn();
    render(<AgentConfigFields value={baseValue} onChange={onChange} />);

    fireEvent.change(screen.getByLabelText(/Max Tokens/), { target: { value: '2048' } });

    expect(onChange).toHaveBeenCalledWith({ maxTokens: 2048 });
  });

  it('emits onChange patch when personalNotes changes', () => {
    const onChange = vi.fn();
    render(<AgentConfigFields value={baseValue} onChange={onChange} />);

    fireEvent.change(screen.getByDisplayValue('ciao'), { target: { value: 'nuove note' } });

    expect(onChange).toHaveBeenCalledWith({ personalNotes: 'nuove note' });
  });

  it('emits onChange patch when a personality radio is selected', () => {
    const onChange = vi.fn();
    render(<AgentConfigFields value={baseValue} onChange={onChange} />);

    fireEvent.click(screen.getByRole('radio', { name: /Professionale/ }));

    expect(onChange).toHaveBeenCalledWith({ personality: 'Professionale' });
  });

  it('emits onChange patch when a detailLevel radio is selected', () => {
    const onChange = vi.fn();
    render(<AgentConfigFields value={baseValue} onChange={onChange} />);

    fireEvent.click(screen.getByRole('radio', { name: /Esaustivo/ }));

    expect(onChange).toHaveBeenCalledWith({ detailLevel: 'Esaustivo' });
  });

  it('disables ALL controls when disabled (native inputs + radios)', () => {
    render(<AgentConfigFields value={baseValue} onChange={vi.fn()} disabled />);

    expect(screen.getByLabelText(/Max Tokens/)).toBeDisabled();
    expect(screen.getByDisplayValue('ciao')).toBeDisabled();
    expect(screen.getByRole('radio', { name: /Amichevole/ })).toBeDisabled();
    expect(screen.getByRole('radio', { name: /Breve/ })).toBeDisabled();
  });
});
