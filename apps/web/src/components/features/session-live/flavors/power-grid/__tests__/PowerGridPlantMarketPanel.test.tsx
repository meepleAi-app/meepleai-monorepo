import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PowerGridPlantMarketPanel } from '../PowerGridPlantMarketPanel';

const plants = { current: [3, 4, null, 6], future: [null, null, null, null] };
const labels = {
  heading: 'Centrali',
  currentBank: 'Attuali',
  futureBank: 'Future',
  emptySlot: '—',
  slotAria: '{bank} slot {n}',
};

describe('PowerGridPlantMarketPanel', () => {
  it('renders 8 slots across two banks', () => {
    const { container } = render(
      <PowerGridPlantMarketPanel plants={plants} editable={false} labels={labels} />
    );
    expect(container.querySelectorAll('[data-slot="pg-plant-slot"]')).toHaveLength(8);
  });

  it('read-only shows numbers / em-dash and no inputs', () => {
    const { container } = render(
      <PowerGridPlantMarketPanel plants={plants} editable={false} labels={labels} />
    );
    expect(container.querySelector('input')).toBeNull();
    const slots = container.querySelectorAll('[data-slot="pg-plant-slot"]');
    expect(slots[0]?.textContent).toContain('3');
    expect(slots[2]?.textContent).toContain('—');
  });

  it('host: typing a number fires onSetPlant with parsed value', async () => {
    const onSetPlant = vi.fn();
    const { container } = render(
      <PowerGridPlantMarketPanel
        plants={{ current: [null, null, null, null], future: [null, null, null, null] }}
        editable
        onSetPlant={onSetPlant}
        labels={labels}
      />
    );
    const input = container.querySelector(
      '[data-slot="pg-plant-slot"][data-bank="current"][data-index="0"] input'
    ) as HTMLInputElement;
    await userEvent.type(input, '15');
    // last change event carries the full value
    expect(onSetPlant).toHaveBeenLastCalledWith('current', 0, 15);
  });

  it('host: clearing the input fires onSetPlant with null', async () => {
    const onSetPlant = vi.fn();
    const { container } = render(
      <PowerGridPlantMarketPanel
        plants={{ current: [7, null, null, null], future: [null, null, null, null] }}
        editable
        onSetPlant={onSetPlant}
        labels={labels}
      />
    );
    const input = container.querySelector(
      '[data-slot="pg-plant-slot"][data-bank="current"][data-index="0"] input'
    ) as HTMLInputElement;
    await userEvent.clear(input);
    expect(onSetPlant).toHaveBeenLastCalledWith('current', 0, null);
  });
});
