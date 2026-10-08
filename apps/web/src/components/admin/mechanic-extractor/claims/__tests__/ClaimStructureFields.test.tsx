import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ClaimStructureFields } from '../ClaimStructureFields';

describe('ClaimStructureFields', () => {
  const siblings = [
    { id: 'a', text: 'Regola generale' },
    { id: 'b', text: 'Altra regola' },
  ];

  it('renders proposed values and emits changes', () => {
    const onChange = vi.fn();
    render(
      <ClaimStructureFields
        value={{
          kind: 'Exception',
          priority: 'Card',
          overrides: ['a'],
          trigger: { phase: 'azione', action: null, component: null },
        }}
        onChange={onChange}
        siblings={siblings}
      />
    );
    expect(screen.getByLabelText('Kind')).toHaveValue('Exception');
    expect(screen.getByLabelText('Priority')).toHaveValue('Card');
    expect(screen.getByLabelText('Overrides: Regola generale')).toBeChecked();
    fireEvent.change(screen.getByLabelText('Trigger phase'), { target: { value: 'fine turno' } });
    expect(onChange).toHaveBeenLastCalledWith(
      expect.objectContaining({ trigger: expect.objectContaining({ phase: 'fine turno' }) })
    );
  });

  it('unchecking an override removes it', () => {
    const onChange = vi.fn();
    render(
      <ClaimStructureFields
        value={{ kind: 'Exception', priority: 'Card', overrides: ['a'], trigger: null }}
        onChange={onChange}
        siblings={siblings}
      />
    );
    fireEvent.click(screen.getByLabelText('Overrides: Regola generale'));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ overrides: [] }));
  });

  it('checking an override adds it and clearing all trigger fields emits null', () => {
    const onChange = vi.fn();
    render(
      <ClaimStructureFields
        value={{
          kind: 'Rule',
          priority: 'Base',
          overrides: [],
          trigger: { phase: 'x', action: null, component: null },
        }}
        onChange={onChange}
        siblings={siblings}
      />
    );
    fireEvent.click(screen.getByLabelText('Overrides: Altra regola'));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ overrides: ['b'] }));
    fireEvent.change(screen.getByLabelText('Trigger phase'), { target: { value: '' } });
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ trigger: null }));
  });
});
