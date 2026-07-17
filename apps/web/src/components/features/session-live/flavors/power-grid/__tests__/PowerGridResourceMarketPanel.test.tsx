import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PowerGridResourceMarketPanel } from '../PowerGridResourceMarketPanel';

const resources = { coal: 5, oil: 2, garbage: 0, uranium: 1 };
const labels = {
  heading: 'Mercato risorse',
  coal: 'Carbone',
  oil: 'Petrolio',
  garbage: 'Rifiuti',
  uranium: 'Uranio',
  incAria: '{field} +1',
  decAria: '{field} -1',
};

describe('PowerGridResourceMarketPanel', () => {
  it('renders all 4 resources with counts', () => {
    const { container } = render(
      <PowerGridResourceMarketPanel resources={resources} editable={false} labels={labels} />
    );
    expect(container.querySelectorAll('[data-resource]')).toHaveLength(4);
    expect(screen.getByText('Carbone').closest('[data-resource]')?.textContent).toContain('5');
  });

  it('read-only exposes no steppers', () => {
    render(<PowerGridResourceMarketPanel resources={resources} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: coal +1 fires onBump', async () => {
    const onBump = vi.fn();
    render(
      <PowerGridResourceMarketPanel
        resources={resources}
        editable
        onBump={onBump}
        labels={labels}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Carbone +1' }));
    expect(onBump).toHaveBeenCalledWith('coal', 1);
  });
});
