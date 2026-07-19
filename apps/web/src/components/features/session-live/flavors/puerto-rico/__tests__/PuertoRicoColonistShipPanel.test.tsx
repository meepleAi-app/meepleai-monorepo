import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PuertoRicoColonistShipPanel } from '../PuertoRicoColonistShipPanel';

const labels = {
  heading: 'Nave coloni',
  onShipLabel: 'Sulla nave',
  supplyLabel: 'Riserva',
  incAria: '{field} +1',
  decAria: '{field} -1',
};

describe('PuertoRicoColonistShipPanel', () => {
  it('shows onShip + supply', () => {
    render(
      <PuertoRicoColonistShipPanel
        colonistShip={{ onShip: 3, supply: 20 }}
        editable={false}
        labels={labels}
      />
    );
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('20')).toBeInTheDocument();
  });

  it('read-only exposes no buttons', () => {
    render(
      <PuertoRicoColonistShipPanel
        colonistShip={{ onShip: 0, supply: 0 }}
        editable={false}
        labels={labels}
      />
    );
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: bumping onShip fires onBump', async () => {
    const onBump = vi.fn();
    render(
      <PuertoRicoColonistShipPanel
        colonistShip={{ onShip: 0, supply: 0 }}
        editable
        onBump={onBump}
        labels={labels}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Sulla nave +1' }));
    expect(onBump).toHaveBeenCalledWith('onShip', 1);
  });
});
