import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PuertoRicoTradingHousePanel } from '../PuertoRicoTradingHousePanel';

const labels = { heading: 'Casa commerciale', emptyGood: '—', slotAria: 'Slot {n}' };

describe('PuertoRicoTradingHousePanel', () => {
  it('renders 4 slots', () => {
    const { container } = render(
      <PuertoRicoTradingHousePanel
        slots={['corn', null, null, null]}
        editable={false}
        labels={labels}
      />
    );
    expect(container.querySelectorAll('[data-slot="pr-trade-slot"]')).toHaveLength(4);
  });

  it('read-only exposes no selects', () => {
    const { container } = render(
      <PuertoRicoTradingHousePanel
        slots={[null, null, null, null]}
        editable={false}
        labels={labels}
      />
    );
    expect(container.querySelector('select')).toBeNull();
  });

  it('host: setting a slot fires onSetSlot', async () => {
    const onSetSlot = vi.fn();
    const { container } = render(
      <PuertoRicoTradingHousePanel
        slots={[null, null, null, null]}
        editable
        onSetSlot={onSetSlot}
        labels={labels}
      />
    );
    const select = container.querySelector(
      '[data-slot="pr-trade-slot"][data-index="0"] select'
    ) as HTMLSelectElement;
    await userEvent.selectOptions(select, 'tobacco');
    expect(onSetSlot).toHaveBeenCalledWith(0, 'tobacco');
  });
});
