/**
 * AgentConfigFields unit tests (Issue #2732)
 *
 * Value-flow assertions go ONLY through the number input and the textarea:
 * Radix Select/Slider rely on pointer capture + portals that are flaky in jsdom.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AgentConfigFields, type AgentConfigFieldsValue } from '../AgentConfigFields';

const VALUE: AgentConfigFieldsValue = {
  llmModel: 'llama-3.3-70b-free',
  temperature: 0.7,
  maxTokens: 4096,
  personality: 'Amichevole',
  detailLevel: 'Normale',
  personalNotes: 'ciao',
};

describe('AgentConfigFields', () => {
  it('renders the six config fields', () => {
    const { container } = render(<AgentConfigFields value={VALUE} onChange={vi.fn()} />);

    // Model select (combobox), temperature slider, maxTokens spinbutton, notes textbox
    expect(container.querySelector('#model')).toBeTruthy();
    expect(container.querySelector('#temperature')).toBeTruthy();
    expect(container.querySelector('#maxTokens')).toBeTruthy();
    expect(container.querySelector('#instructions')).toBeTruthy();

    // Personality (5) + Detail level (4) = 9 radio inputs
    expect(screen.getAllByRole('radio')).toHaveLength(9);

    // Notes counter reflects the current value length
    expect(screen.getByText(/caratteri rimanenti/i)).toBeInTheDocument();
  });

  it('emits onChange with the updated maxTokens when the number input changes', () => {
    const onChange = vi.fn();
    const { container } = render(<AgentConfigFields value={VALUE} onChange={onChange} />);

    const input = container.querySelector('#maxTokens') as HTMLInputElement;
    fireEvent.change(input, { target: { value: '2048' } });

    expect(onChange).toHaveBeenCalledWith({ ...VALUE, maxTokens: 2048 });
  });

  it('emits onChange with the updated personalNotes when the textarea changes', () => {
    const onChange = vi.fn();
    const { container } = render(<AgentConfigFields value={VALUE} onChange={onChange} />);

    const textarea = container.querySelector('#instructions') as HTMLTextAreaElement;
    fireEvent.change(textarea, { target: { value: 'nuove note' } });

    expect(onChange).toHaveBeenLastCalledWith({ ...VALUE, personalNotes: 'nuove note' });
  });

  it('disables the inputs when disabled=true', () => {
    const { container } = render(<AgentConfigFields value={VALUE} onChange={vi.fn()} disabled />);

    expect(container.querySelector('#maxTokens')).toBeDisabled();
    expect(container.querySelector('#instructions')).toBeDisabled();
    // Every radio is disabled too
    screen.getAllByRole('radio').forEach(radio => expect(radio).toBeDisabled());
  });

  it('prefixes element ids with idPrefix', () => {
    const { container } = render(
      <AgentConfigFields value={VALUE} onChange={vi.fn()} idPrefix="pfx-" />
    );

    expect(container.querySelector('#pfx-model')).toBeTruthy();
    expect(container.querySelector('#pfx-temperature')).toBeTruthy();
    expect(container.querySelector('#pfx-maxTokens')).toBeTruthy();
    expect(container.querySelector('#pfx-instructions')).toBeTruthy();
    // Non-prefixed ids must NOT exist
    expect(container.querySelector('#maxTokens')).toBeNull();
  });
});
