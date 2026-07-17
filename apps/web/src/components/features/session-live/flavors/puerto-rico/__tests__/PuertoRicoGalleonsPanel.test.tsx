import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PuertoRicoGalleonsPanel } from '../PuertoRicoGalleonsPanel';

const galleons = [
  { good: 'corn' as const, loaded: 2, cap: 5 },
  { good: null, loaded: 0, cap: 6 },
  { good: null, loaded: 0, cap: 7 },
];
const labels = {
  heading: 'Galeoni',
  emptyGood: '—',
  goodAria: 'Merce nave {n}',
  loadedAria: 'Carica nave {n}',
  unloadAria: 'Scarica nave {n}',
  capTemplate: '{loaded}/{cap}',
};

describe('PuertoRicoGalleonsPanel', () => {
  it('renders one row per galleon with loaded/cap', () => {
    const { container } = render(
      <PuertoRicoGalleonsPanel galleons={galleons} editable={false} labels={labels} />
    );
    const rows = container.querySelectorAll('[data-slot="pr-galleon"]');
    expect(rows).toHaveLength(3);
    expect(rows[0]?.textContent).toContain('2/5');
  });

  it('read-only exposes no controls', () => {
    const { container } = render(
      <PuertoRicoGalleonsPanel galleons={galleons} editable={false} labels={labels} />
    );
    expect(container.querySelector('button')).toBeNull();
    expect(container.querySelector('select')).toBeNull();
  });

  it('host: loading a galleon fires onBumpLoaded with its index', async () => {
    const onBumpLoaded = vi.fn();
    const { container } = render(
      <PuertoRicoGalleonsPanel
        galleons={galleons}
        editable
        onBumpLoaded={onBumpLoaded}
        labels={labels}
      />
    );
    const load0 = container.querySelector(
      '[data-slot="pr-galleon"][data-index="0"] [data-dir="inc"]'
    ) as HTMLElement;
    await userEvent.click(load0);
    expect(onBumpLoaded).toHaveBeenCalledWith(0, 1);
  });

  it('host: choosing a good fires onSetGood', async () => {
    const onSetGood = vi.fn();
    const { container } = render(
      <PuertoRicoGalleonsPanel galleons={galleons} editable onSetGood={onSetGood} labels={labels} />
    );
    const select = container.querySelector(
      '[data-slot="pr-galleon"][data-index="1"] select'
    ) as HTMLSelectElement;
    await userEvent.selectOptions(select, 'sugar');
    expect(onSetGood).toHaveBeenCalledWith(1, 'sugar');
  });

  it('host: each good select has an accessible name (per ship)', () => {
    render(<PuertoRicoGalleonsPanel galleons={galleons} editable labels={labels} />);
    expect(screen.getByRole('combobox', { name: 'Merce nave 1' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Merce nave 3' })).toBeInTheDocument();
  });
});
