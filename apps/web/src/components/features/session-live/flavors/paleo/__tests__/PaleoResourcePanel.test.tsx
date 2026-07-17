import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PaleoResourcePanel } from '../PaleoResourcePanel';

const resources = { wood: 3, stone: 1, food: 0, knowledge: 2 };
const labels = {
  heading: 'Risorse',
  wood: 'Legno',
  stone: 'Pietra',
  food: 'Cibo',
  knowledge: 'Conoscenza',
  incAria: '{field} +1',
  decAria: '{field} -1',
};

describe('PaleoResourcePanel', () => {
  it('renders all 4 resources with counts', () => {
    const { container } = render(
      <PaleoResourcePanel resources={resources} editable={false} labels={labels} />
    );
    expect(container.querySelectorAll('[data-resource]')).toHaveLength(4);
    expect(screen.getByText('Legno').closest('[data-resource]')?.textContent).toContain('3');
  });

  it('read-only exposes no steppers', () => {
    render(<PaleoResourcePanel resources={resources} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: wood +1 fires onBump', async () => {
    const onBump = vi.fn();
    render(<PaleoResourcePanel resources={resources} editable onBump={onBump} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Legno +1' }));
    expect(onBump).toHaveBeenCalledWith('wood', 1);
  });
});
